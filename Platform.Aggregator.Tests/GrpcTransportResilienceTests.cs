using System.Net;
using System.Net.Http.Headers;
using Accounts.Grpc;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Platform.Aggregator.Clients;
using Platform.ServiceDefaults;
using Xunit;

namespace Platform.Aggregator.Tests;

public sealed class GrpcTransportResilienceTests
{
    [Fact]
    public async Task Persistent_transport_failures_open_the_circuit_without_sending_more_requests()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddServiceDefaults();
        var exporter = new PollyEventExporter();
        builder.Services.AddOpenTelemetry().WithMetrics(metrics => metrics
            .AddReader(new PeriodicExportingMetricReader(exporter, exportIntervalMilliseconds: 60000)));
        builder.Services.AddGrpcDashboardClients();
        var transport = new UnavailableTransport();
        builder.Services.AddGrpcClient<AccountsReader.AccountsReaderClient>()
            .ConfigurePrimaryHttpMessageHandler(() => transport);
        using var host = builder.Build();
        var metrics = host.Services.GetRequiredService<MeterProvider>();
        var client = host.Services.GetRequiredService<AccountsReader.AccountsReaderClient>();

        await Assert.ThrowsAsync<RpcException>(async () =>
            await client.GetUserAsync(new UserRequest { UserId = 1 }, deadline: DateTime.UtcNow.AddSeconds(5)));
        Assert.Equal(4, transport.Attempts);

        await Assert.ThrowsAsync<RpcException>(async () =>
            await client.GetUserAsync(new UserRequest { UserId = 1 }, deadline: DateTime.UtcNow.AddSeconds(5)));
        Assert.Equal(4, transport.Attempts);
        Assert.True(metrics.ForceFlush());
        Assert.Contains("OnCircuitOpened", exporter.Events);
    }

    private sealed class PollyEventExporter : BaseExporter<Metric>
    {
        public List<string> Events { get; } = [];

        public override ExportResult Export(in Batch<Metric> batch)
        {
            foreach (var metric in batch)
                if (metric.MeterName == "Polly" && metric.Name == "resilience.polly.strategy.events")
                    foreach (var point in metric.GetMetricPoints())
                        foreach (var tag in point.Tags)
                            if (tag.Key == "event.name" && tag.Value is string name)
                                Events.Add(name);
            return ExportResult.Success;
        }
    }

    private sealed class UnavailableTransport : HttpMessageHandler
    {
        public int Attempts { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Attempts++;
            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Version = HttpVersion.Version20,
                Content = new ByteArrayContent([])
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/grpc");
            return Task.FromResult(response);
        }
    }
}
