using Microsoft.Extensions.Logging;

namespace Platform.Caching;

public sealed class CacheInvalidator(CacheGroupState states, CacheVersions versions,
    CacheMetrics metrics, ILogger<CacheInvalidator> logger)
{
    public async Task InvalidateAsync(IReadOnlyList<string> groups, string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        foreach (var group in groups) CacheKeys.ValidateGroup(group);
        if (groups.Count == 0) return;
        // The owning database write has committed; caller cancellation must not skip this work.
        var affected = groups.Append("dashboard").Distinct().ToArray();
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        foreach (var group in affected)
        {
            var state = states.Get(group);
            var advanced = false;
            var entered = false;
            CacheGroupState.Snapshot? pending = null;
            try
            {
                await state.Gate.WaitAsync(budget.Token);
                entered = true;
                pending = state.MarkDirty();
                advanced = await versions.AdvanceAsync(group, budget.Token);
            }
            catch (OperationCanceledException) when (budget.IsCancellationRequested)
            {
                logger.LogWarning("Data committed; cache invalidation budget expired for {CacheGroup}", group);
            }
            finally
            {
                // Even an unsuccessful Redis write must cancel every old local fill.
                pending ??= state.MarkDirty();
                state.Rotate(!advanced, pending);
                if (entered) state.Gate.Release();
                metrics.Invalidated(group);
                logger.LogInformation("Cache invalidation {CacheGroup}: {Reason}, Redis advanced {Advanced}",
                    group, reason, advanced);
            }
        }
    }
}
