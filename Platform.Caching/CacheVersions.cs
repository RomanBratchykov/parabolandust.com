using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Platform.Caching;

public sealed class CacheVersions(IDistributedCache redis, CacheGroupState states,
    CacheMetrics metrics, ILogger<CacheVersions> logger)
{
    private static readonly TimeSpan MarkerLifetime = TimeSpan.FromHours(24);

    public sealed record Generation(string Value, CacheGroupState.Snapshot Local, bool Available);

    public async Task<Generation> CaptureAsync(string group, CancellationToken cancellationToken = default)
    {
        var state = states.Get(group);
        await state.Gate.WaitAsync(cancellationToken);
        try
        {
            if (state.Current.Dirty)
            {
                var pending = state.Current;
                if (!await AdvanceAsync(group, cancellationToken))
                    return new("initial", state.Current, false);
                state.Rotate(false, pending);
                if (state.Current.Dirty) return new("initial", state.Current, false);
            }
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budget.CancelAfter(CachePolicy.RedisTimeout);
            try
            {
                var bytes = await redis.GetAsync(MarkerKey(group), budget.Token).WaitAsync(budget.Token);
                cancellationToken.ThrowIfCancellationRequested();
                var value = bytes is null ? "initial" : Encoding.UTF8.GetString(bytes);
                if (value != "initial" && !Guid.TryParseExact(value, "N", out _))
                {
                    state.MarkDirty();
                    state.Rotate(true);
                    logger.LogWarning("Invalid cache generation for {CacheGroup}; bypassing Redis", group);
                    return new("initial", state.Current, false);
                }
                var local = state.Observe(value);
                return new(value, local, !local.Dirty);
            }
            catch (Exception exception) when (IsRedisFailure(exception, cancellationToken))
            {
                LogFailure(group, exception);
                return new("initial", state.Current, false);
            }
        }
        finally { state.Gate.Release(); }
    }

    public async Task<bool> IsCurrentAsync(string group, Generation captured,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!captured.Available || captured.Local.Token.IsCancellationRequested) return false;
        var current = await CaptureAsync(group, cancellationToken);
        return current.Available && current.Value == captured.Value &&
            !captured.Local.Token.IsCancellationRequested;
    }

    internal async Task<bool> AdvanceAsync(string group, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(CachePolicy.RedisTimeout);
        try
        {
            await redis.SetAsync(MarkerKey(group), Encoding.UTF8.GetBytes(Guid.NewGuid().ToString("N")),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = MarkerLifetime },
                budget.Token).WaitAsync(budget.Token);
            cancellationToken.ThrowIfCancellationRequested();
            return true;
        }
        catch (Exception exception) when (IsRedisFailure(exception, cancellationToken))
        {
            LogFailure(group, exception);
            return false;
        }
    }

    private static string MarkerKey(string group) => $"platform:v1:generation:{group}";

    private static bool IsRedisFailure(Exception exception, CancellationToken caller) =>
        exception is RedisException or TimeoutException ||
        exception is OperationCanceledException && !caller.IsCancellationRequested;

    private void LogFailure(string group, Exception exception)
    {
        metrics.Error(group, "L2");
        logger.LogWarning(exception, "Cache generation unavailable for {CacheGroup}; using source or memory", group);
    }
}
