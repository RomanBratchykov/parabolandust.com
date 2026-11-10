using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Platform.Caching;
using StackExchange.Redis;

namespace Platform.ServiceDefaults.Caching;

public sealed class CacheHealthCheck(IServiceProvider services) : IHealthCheck
{
    public const string Name = "cache";
    private static readonly string[] Groups = ["accounts", "newsletters", "gamestats", "dashboard"];

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!services.GetRequiredService<IServiceProviderIsService>().IsService(typeof(ICacheAside)))
            return HealthCheckResult.Healthy("Caching is not configured.");

        var metrics = services.GetRequiredService<CacheMetrics>();
        var data = new Dictionary<string, object>
        {
            ["caches"] = Groups.ToDictionary(group => group, metrics.Snapshot)
        };
        var redis = services.GetRequiredService<IDistributedCache>();
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(CachePolicy.RedisTimeout);
        try
        {
            // Probe the provider without creating entries or querying a business database.
            await redis.GetAsync("platform:v1:health", budget.Token).WaitAsync(budget.Token);
            return HealthCheckResult.Healthy("Redis is available.", data);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException ||
            exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Redis is unavailable; reads can fall back to the source.",
                exception, data);
        }
    }
}
