using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Platform.ServiceDefaults;
using Xunit;

namespace Platform.Aggregator.Tests;

public sealed class HealthEndpointTests
{
    [Theory]
    [InlineData(HealthStatus.Healthy, HttpStatusCode.OK)]
    [InlineData(HealthStatus.Degraded, HttpStatusCode.OK)]
    [InlineData(HealthStatus.Unhealthy, HttpStatusCode.ServiceUnavailable)]
    public async Task Production_endpoints_separate_liveness_from_readiness(
        HealthStatus status, HttpStatusCode expectedStatus)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.AddServiceDefaults();
        var dependencyCalls = 0;
        builder.Services.AddHealthChecks().AddCheck("dependency", () =>
        {
            Interlocked.Increment(ref dependencyCalls);
            return new HealthCheckResult(status, "Dependency probe.");
        }, tags: ["ready"]);
        builder.Services.AddHealthChecks().AddCheck("excluded", () => HealthCheckResult.Unhealthy());
        await using var app = builder.Build();
        app.MapDefaultEndpoints();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };

        using var live = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(0, dependencyCalls);
        using var ready = await client.GetAsync("/health/ready");
        Assert.Equal(expectedStatus, ready.StatusCode);
        var body = await ready.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(status.ToString(), body.GetProperty("status").GetString());
        Assert.Equal(status.ToString(), body.GetProperty("checks").GetProperty("dependency").GetProperty("status").GetString());
        Assert.False(body.GetProperty("checks").TryGetProperty("excluded", out _));
        Assert.Equal(1, dependencyCalls);
    }
}
