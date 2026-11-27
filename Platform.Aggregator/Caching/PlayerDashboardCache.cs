using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Platform.Aggregator.Contracts;
using Platform.Aggregator.Services;
using Platform.Caching;
using StackExchange.Redis;

namespace Platform.Aggregator.Caching;

public sealed class PlayerDashboardCache(PlayerDashboardService source, IDistributedCache redis,
    CacheVersions versions, CacheMetrics metrics, ILogger<PlayerDashboardCache> logger, TimeProvider clock)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true
    };

    public async Task<PlayerDashboardDto?> GetDashboardAsync(long userId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        cancellationToken.ThrowIfCancellationRequested();
        using var activity = CacheMetrics.Activities.StartActivity("cache.dashboard");
        activity?.SetTag("cache.name", "dashboard");
        var started = Stopwatch.GetTimestamp();
        var generation = await versions.CaptureAsync("dashboard", cancellationToken);
        var key = CacheKeys.ForRead("dashboard", generation.Value, "player", userId.ToString(CultureInfo.InvariantCulture));
        try
        {
            var bytes = generation.Available ? await ReadAsync(key, cancellationToken) : null;
            cancellationToken.ThrowIfCancellationRequested();
            var payload = Deserialize(bytes, key);
            if (payload is { Version: 1 } && payload.ExpiresAt > clock.GetUtcNow() && Complete(payload.Value, userId))
            {
                metrics.Hit("dashboard", "L2");
                logger.LogDebug("Cache hit {CacheKey} {CacheLevel}, remaining {RemainingMs} ms, {SizeBytes} bytes, elapsed {ElapsedMs} ms",
                    key, "L2", (payload.ExpiresAt - clock.GetUtcNow()).TotalMilliseconds, bytes!.Length,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                return payload.Value;
            }
            metrics.Miss("dashboard", "L2");
            logger.LogDebug("Cache miss {CacheKey} {CacheLevel}, elapsed {ElapsedMs} ms",
                key, "L2", Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            var value = await source.GetDashboardAsync(userId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (Complete(value, userId) && await versions.IsCurrentAsync("dashboard", generation, cancellationToken))
            {
                payload = new Payload(1, clock.GetUtcNow().Add(CachePolicy.DashboardExpiration), value!);
                bytes = JsonSerializer.SerializeToUtf8Bytes(payload, Json);
                await WriteAsync(key, payload.ExpiresAt, bytes, cancellationToken);
                logger.LogDebug("Cache fill {CacheKey}, TTL {TtlMs} ms, {SizeBytes} bytes, elapsed {ElapsedMs} ms",
                    key, CachePolicy.DashboardExpiration.TotalMilliseconds, bytes.Length,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return value;
        }
        finally { metrics.Duration("dashboard", "L2", Stopwatch.GetElapsedTime(started).TotalMilliseconds); }
    }

    private static bool Complete(PlayerDashboardDto? value, long userId) =>
        value is { Player: not null, Subscriptions: not null, Statistics: not null, Warnings.Count: 0 } &&
        value.Player.UserId == userId && !string.IsNullOrWhiteSpace(value.Player.UserName);

    private Payload? Deserialize(byte[]? bytes, string key)
    {
        if (bytes is null) return null;
        try { return JsonSerializer.Deserialize<Payload>(bytes, Json); }
        catch (JsonException exception)
        {
            metrics.Error("dashboard", "L2");
            logger.LogWarning(exception, "Invalid dashboard payload for {CacheKey}", key);
            return null;
        }
    }

    private async Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(CachePolicy.RedisTimeout);
        try
        {
            return await redis.GetAsync(key, budget.Token).WaitAsync(budget.Token);
        }
        catch (Exception exception) when (RedisFailure(exception, cancellationToken))
        {
            LogFailure(exception, key);
            return null;
        }
    }

    private async Task WriteAsync(string key, DateTimeOffset expiresAt, byte[] bytes, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(CachePolicy.RedisTimeout);
        try
        {
            await redis.SetAsync(key, bytes, new DistributedCacheEntryOptions { AbsoluteExpiration = expiresAt },
                budget.Token).WaitAsync(budget.Token);
        }
        catch (Exception exception) when (RedisFailure(exception, cancellationToken)) { LogFailure(exception, key); }
    }

    private static bool RedisFailure(Exception exception, CancellationToken caller) =>
        exception is RedisException or TimeoutException ||
        exception is OperationCanceledException && !caller.IsCancellationRequested;

    private void LogFailure(Exception exception, string key)
    {
        metrics.Error("dashboard", "L2");
        logger.LogWarning(exception, "Redis unavailable for {CacheKey}; composing dashboard", key);
    }

    private sealed record Payload(int Version, DateTimeOffset ExpiresAt, PlayerDashboardDto Value);
}
