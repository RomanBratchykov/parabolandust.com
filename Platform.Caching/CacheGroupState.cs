using System.Collections.Concurrent;

namespace Platform.Caching;

public sealed class CacheGroupState : IDisposable
{
    private readonly ConcurrentDictionary<string, Group> groups = new();

    public Snapshot Capture(string group) => Get(group).Current;

    internal Group Get(string group)
    {
        CacheKeys.ValidateGroup(group);
        return groups.GetOrAdd(group, _ => new Group());
    }

    public void Dispose()
    {
        foreach (var group in groups.Values) group.Dispose();
    }

    public sealed record Snapshot(string Generation, CancellationToken Token, bool Dirty);

    internal sealed class Group : IDisposable
    {
        private CancellationTokenSource source = new();
        private Snapshot current;
        private readonly object sync = new();
        private string? remoteGeneration;
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public Snapshot Current => Volatile.Read(ref current);

        public Group() => current = new(Guid.NewGuid().ToString("N"), source.Token, false);

        public Snapshot MarkDirty()
        {
            lock (sync)
            {
                var pending = Current with { Dirty = true };
                Volatile.Write(ref current, pending);
                return pending;
            }
        }

        public void Rotate(bool dirty, Snapshot? expected = null)
        {
            lock (sync)
            {
                // A timed-out invalidator can mark this group dirty while another owns Gate.
                if (expected is not null && !ReferenceEquals(Current, expected)) return;
                var previous = source;
                source = new CancellationTokenSource();
                Volatile.Write(ref current, new(Guid.NewGuid().ToString("N"), source.Token, dirty));
                previous.Cancel();
                previous.Dispose();
            }
        }

        public Snapshot Observe(string generation)
        {
            lock (sync)
            {
                if (remoteGeneration is not null && remoteGeneration != generation)
                    Rotate(Current.Dirty);
                remoteGeneration = generation;
                return Current;
            }
        }

        public void Dispose()
        {
            source.Cancel();
            source.Dispose();
            Gate.Dispose();
        }
    }
}
