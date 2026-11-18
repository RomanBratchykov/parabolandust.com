using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using Google.Protobuf;
using Grpc.Health.V1;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Platform.Aggregator.Clients;
using Platform.ServiceDefaults;
using Xunit;

namespace Platform.Aggregator.Tests;

public sealed class GrpcReadinessTests
{
    [Theory]
    [InlineData("serving", HealthStatus.Healthy)]
    [InlineData("not-serving", HealthStatus.Unhealthy)]
    [InlineData("unavailable", HealthStatus.Unhealthy)]
    [InlineData("timeout", HealthStatus.Unhealthy)]
    public async Task Readiness_probes_all_three_dependencies_with_bounded_grpc_calls(string mode, HealthStatus expected)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddServiceDefaults();
        builder.Services.AddGrpcDashboardClients();
        foreach (var service in new[] { "accounts-api", "newsletters-api", "gamestats-api" })
            builder.Services.AddGrpcClient<Health.HealthClient>(service)
                .ConfigurePrimaryHttpMessageHandler(() => new HealthTransport(mode));
        using var host = builder.Build();

        var report = await host.Services.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(check => check.Tags.Contains("downstream"));

        Assert.Equal(3, report.Entries.Count);
        Assert.All(report.Entries, entry => Assert.Equal(expected, entry.Value.Status));
    }

    private sealed class HealthTransport(string mode) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (mode == "timeout") await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            var message = new HealthCheckResponse
            {
                Status = mode == "serving" ? HealthCheckResponse.Types.ServingStatus.Serving
                    : HealthCheckResponse.Types.ServingStatus.NotServing
            }.ToByteArray();
            var frame = new byte[5 + message.Length];
            BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(1, 4), message.Length);
            message.CopyTo(frame, 5);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Version = HttpVersion.Version20,
                Content = new ByteArrayContent(mode == "unavailable" ? [] : frame)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/grpc");
            if (mode == "unavailable") response.Headers.Add("grpc-status", "14");
            else response.TrailingHeaders.Add("grpc-status", "0");
            return response;
        }
    }
}
