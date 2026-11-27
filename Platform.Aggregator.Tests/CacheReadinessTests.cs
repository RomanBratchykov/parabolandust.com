using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Platform.Caching;
using Platform.ServiceDefaults;
using StackExchange.Redis;
using Xunit;

namespace Platform.Aggregator.Tests;

public sealed class CacheReadinessTests
{
    [Fact]
    public async Task Redis_outage_degrades_readiness_while_source_reads_and_recovery_work()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddServiceDefaults();
        builder.Services.AddPlatformCaching();
        var redis = new ToggleCache();
        builder.Services.AddSingleton<IDistributedCache>(redis);
        using var host = builder.Build();
        var health = host.Services.GetRequiredService<HealthCheckService>();

        var report = await health.CheckHealthAsync(check => check.Tags.Contains("ready"));
        Assert.Equal(HealthStatus.Degraded, report.Status);
        Assert.Contains("cache", report.Entries.Keys);
        var sourceReads = 0;
        var value = await host.Services.GetRequiredService<ICacheAside>().GetOrCreateAsync(
            "gamestats", "statistics", "1", CachePolicy.GameStats, _ =>
            {
                sourceReads++;
                return Task.FromResult<string?>("source value");
            });
        Assert.Equal("source value", value);
        Assert.Equal(1, sourceReads);

        redis.Available = true;
        Assert.Equal(HealthStatus.Healthy, (await health.CheckHealthAsync(check => check.Tags.Contains("ready"))).Status);
    }

    private sealed class ToggleCache : IDistributedCache
    {
        public bool Available { get; set; }
        public byte[]? Get(string key) => throw new NotSupportedException();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Available
            ? Task.FromResult<byte[]?>(null)
            : Task.FromException<byte[]?>(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis is offline."));
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new NotSupportedException();
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => Task.CompletedTask;
        public void Refresh(string key) => throw new NotSupportedException();
        public Task RefreshAsync(string key, CancellationToken token = default) => throw new NotSupportedException();
        public void Remove(string key) => throw new NotSupportedException();
        public Task RemoveAsync(string key, CancellationToken token = default) => throw new NotSupportedException();
    }
}
