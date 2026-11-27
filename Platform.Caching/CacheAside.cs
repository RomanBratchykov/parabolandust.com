using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using StackExchange.Redis;

namespace Platform.Caching;

public sealed class CacheAside([FromKeyedServices(CacheRegistration.MemoryCacheKey)] IMemoryCache memory, IDistributedCache redis,
    CacheMetrics metrics, ILogger<CacheAside> logger, TimeProvider clock,
    CacheGroupState states, CacheVersions versions) : ICacheAside
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true
    };

    public async Task<T?> GetOrCreateAsync<T>(string group, string operation, string identity,
        CachePolicy policy, Func<CancellationToken, Task<T?>> factory,
        CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = states.Capture(group);
        var memoryKey = CacheKeys.ForRead(group, snapshot.Generation, operation, identity);
        var key = memoryKey;
        using var activity = CacheMetrics.Activities.StartActivity("cache.read");
        activity?.SetTag("cache.name", group);
        activity?.SetTag("cache.operation", operation);
        var started = Stopwatch.GetTimestamp();
        if (!snapshot.Dirty && !snapshot.Token.IsCancellationRequested &&
            memory.TryGetValue<MemoryValue<T>>(memoryKey, out var local) &&
            local is not null && local.Payload.ExpiresAt > clock.GetUtcNow())
        {
            metrics.Hit(group, "L1");
            LogHit(key, "L1", local.Payload.ExpiresAt, local.Bytes, started);
            metrics.Duration(group, "L1", Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return local.Payload.Value;
        }
        metrics.Miss(group, "L1");
        logger.LogDebug("Cache miss {CacheKey} {CacheLevel}, elapsed {ElapsedMs} ms", key, "L1",
            Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        metrics.Duration(group, "L1", Stopwatch.GetElapsedTime(started).TotalMilliseconds);

        var generation = await versions.CaptureAsync(group, cancellationToken);
        snapshot = generation.Local;
        memoryKey = CacheKeys.ForRead(group, snapshot.Generation, operation, identity);
        key = CacheKeys.ForRead(group, generation.Value, operation, identity);
        var (bytes, available) = generation.Available
            ? await ReadRedisAsync(key, group, cancellationToken) : (null, false);
        var payload = Deserialize<T>(bytes, key, group);
        if (payload is not null && payload.Version == 1 && payload.Value is not null &&
            payload.ExpiresAt > clock.GetUtcNow())
        {
            metrics.Hit(group, "L2");
            StoreMemory(memoryKey, group, policy, payload, bytes!.Length, snapshot);
            LogHit(key, "L2", payload.ExpiresAt, bytes.Length, started);
            return payload.Value;
        }
        metrics.Miss(group, "L2");
        logger.LogDebug("Cache miss {CacheKey} {CacheLevel}, elapsed {ElapsedMs} ms", key, "L2",
            Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        var value = await factory(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (value is null) return null;

        payload = new CachePayload<T>(1, clock.GetUtcNow().Add(policy.DistributedExpiration), value);
        bytes = JsonSerializer.SerializeToUtf8Bytes(payload, Json);
        if (available)
            await WriteRedisAsync(key, group, payload.ExpiresAt, bytes, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        StoreMemory(memoryKey, group, policy, payload, bytes.Length, snapshot);
        logger.LogDebug("Cache fill {CacheKey}, TTL {TtlMs} ms, {SizeBytes} bytes, elapsed {ElapsedMs} ms",
            key, policy.DistributedExpiration.TotalMilliseconds, bytes.Length,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return value;
    }

    private async Task<(byte[]? Bytes, bool Available)> ReadRedisAsync(
        string key, string group, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(CachePolicy.RedisTimeout);
        try
        {
            var bytes = await redis.GetAsync(key, budget.Token).WaitAsync(budget.Token);
            cancellationToken.ThrowIfCancellationRequested();
            return (bytes, true);
        }
        catch (Exception exception) when (IsRedisFailure(exception, cancellationToken))
        {
            LogFailure(exception, key, group);
            return (null, false);
        }
        finally
        {
            metrics.Duration(group, "L2", Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    private async Task WriteRedisAsync(string key, string group, DateTimeOffset expiresAt,
        byte[] bytes, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(CachePolicy.RedisTimeout);
        try
        {
            await redis.SetAsync(key, bytes, new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = expiresAt
            }, budget.Token).WaitAsync(budget.Token);
        }
        catch (Exception exception) when (IsRedisFailure(exception, cancellationToken))
        {
            LogFailure(exception, key, group);
        }
    }

    private CachePayload<T>? Deserialize<T>(byte[]? bytes, string key, string group)
    {
        if (bytes is null) return null;
        try
        {
            return JsonSerializer.Deserialize<CachePayload<T>>(bytes, Json);
        }
        catch (JsonException exception)
        {
            metrics.Error(group, "L2");
            logger.LogWarning(exception, "Invalid cache payload for {CacheKey}", key);
            return null;
        }
    }

    private void StoreMemory<T>(string key, string group, CachePolicy policy,
        CachePayload<T> payload, int bytes, CacheGroupState.Snapshot snapshot)
    {
        if (snapshot.Dirty || snapshot.Token.IsCancellationRequested) return;
        var absolute = clock.GetUtcNow().Add(policy.MemoryExpiration);
        if (payload.ExpiresAt < absolute) absolute = payload.ExpiresAt;
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = absolute,
            SlidingExpiration = policy.SlidingExpiration,
            Size = policy.Size
        }.AddExpirationToken(new CancellationChangeToken(snapshot.Token))
        .RegisterPostEvictionCallback((_, _, reason, _) =>
        {
            metrics.MemoryRemoved(group, bytes);
            logger.LogDebug("Cache eviction {CacheKey}: {Reason}", key, reason);
        });
        metrics.MemoryStored(group, bytes);
        memory.Set(key, new MemoryValue<T>(payload, bytes), options);
    }

    private static bool IsRedisFailure(Exception exception, CancellationToken caller) =>
        exception is RedisException or TimeoutException ||
        exception is OperationCanceledException && !caller.IsCancellationRequested;

    private void LogFailure(Exception exception, string key, string group)
    {
        metrics.Error(group, "L2");
        logger.LogWarning(exception, "Redis unavailable for {CacheKey}; using source or memory", key);
    }

    private void LogHit(string key, string level, DateTimeOffset expiresAt, int bytes, long started) =>
        logger.LogDebug("Cache hit {CacheKey} {CacheLevel}, remaining {RemainingMs} ms, {SizeBytes} bytes, elapsed {ElapsedMs} ms",
            key, level, (expiresAt - clock.GetUtcNow()).TotalMilliseconds, bytes,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds);
}
