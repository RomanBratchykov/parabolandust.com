using Accounts.Grpc;
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
        using var host = CreateHost();
        var dashboard = host.Services.GetRequiredService<PlayerDashboardService>();

        var exception = await Assert.ThrowsAsync<DashboardUnavailableException>(() =>
            dashboard.GetDashboardAsync(1, CancellationToken.None));

        Assert.True(exception.TimedOut);
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
        var builder = Host.CreateApplicationBuilder();
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
