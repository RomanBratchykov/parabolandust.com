using Accounts.Grpc;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Services;
using Platform.ServiceDefaults;
using Xunit;

namespace Platform.Aggregator.Tests;

public sealed class GrpcTimeoutTests
{
    [Fact]
    public async Task Transport_attempt_timeout_preserves_dashboard_timeout_classification()
    {
        var outcomes = new List<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, source) =>
        {
            if (instrument.Meter.Name == "Platform.Aggregator") source.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) =>
        {
            if (instrument.Name == "dashboard.operation.duration") outcomes.Add((string)tags[1].Value!);
        });
        listener.Start();
        using var host = CreateHost();
        var dashboard = host.Services.GetRequiredService<PlayerDashboardService>();

        var exception = await Assert.ThrowsAsync<DashboardUnavailableException>(() =>
            dashboard.GetDashboardAsync(1, CancellationToken.None));

        Assert.True(exception.TimedOut);
        Assert.Equal(new[] { "failure" }, outcomes);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_reported_as_a_downstream_failure()
    {
        using var host = CreateHost();
        var dashboard = host.Services.GetRequiredService<PlayerDashboardService>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            dashboard.GetDashboardAsync(1, cancellation.Token));
    }

    private static IHost CreateHost()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { ApplicationName = "Platform.Aggregator" });
        builder.AddServiceDefaults();
        builder.Services.AddGrpcDashboardClients();
        builder.Services.AddDashboardClients();
        builder.Services.AddScoped<PlayerDashboardService>();
        builder.Services.AddGrpcClient<AccountsReader.AccountsReaderClient>()
            .ConfigurePrimaryHttpMessageHandler(() => new SlowTransport());
        return builder.Build();
    }

    private sealed class SlowTransport : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new InvalidOperationException("The transport must be cancelled before it returns.");
        }
    }
}
