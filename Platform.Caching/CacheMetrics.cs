using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Platform.Caching;

public sealed class CacheMetrics
{
    public const string MeterName = "Platform.Cache";
    public static ActivitySource Activities { get; } = new(MeterName);
    private readonly Counter<long> _hits, _misses, _errors, _evictions, _invalidations;
    private readonly Histogram<double> _duration;
    private readonly ConcurrentDictionary<string, Statistics> _statistics = new();

    public CacheMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _hits = meter.CreateCounter<long>("cache.hits");
        _misses = meter.CreateCounter<long>("cache.misses");
        _errors = meter.CreateCounter<long>("cache.errors");
        _evictions = meter.CreateCounter<long>("cache.evictions");
        _invalidations = meter.CreateCounter<long>("cache.invalidations");
        _duration = meter.CreateHistogram<double>("cache.operation.duration", "ms");
        meter.CreateObservableGauge("cache.entries", () => Measurements(statistics => Interlocked.Read(ref statistics.Entries)));
        meter.CreateObservableGauge("cache.size.bytes", () => Measurements(statistics => Interlocked.Read(ref statistics.Bytes)), "By");
    }

    public void Hit(string group, string level)
    {
        var tags = Tags(group, level);
        Interlocked.Increment(ref GetStatistics(group).Hits);
        _hits.Add(1, tags);
    }

    public void Miss(string group, string level)
    {
        var tags = Tags(group, level);
        Interlocked.Increment(ref GetStatistics(group).Misses);
        _misses.Add(1, tags);
    }

    public void Error(string group, string level) => _errors.Add(1, Tags(group, level));
    public void Duration(string group, string level, double milliseconds) =>
        _duration.Record(milliseconds, Tags(group, level));

    public void Invalidated(string group)
    {
        var tags = Tags(group, "L1");
        Interlocked.Increment(ref GetStatistics(group).Invalidations);
        _invalidations.Add(1, tags);
    }

    internal void MemoryStored(string group, int bytes)
    {
        var statistics = GetStatistics(group);
        Interlocked.Increment(ref statistics.Entries);
        Interlocked.Add(ref statistics.Bytes, bytes);
    }

    internal void MemoryRemoved(string group, int bytes)
    {
        var statistics = GetStatistics(group);
        Interlocked.Decrement(ref statistics.Entries);
        Interlocked.Add(ref statistics.Bytes, -bytes);
        _evictions.Add(1, Tags(group, "L1"));
    }

    public CacheStatistics Snapshot(string group)
    {
        var statistics = GetStatistics(group);
        return new(Interlocked.Read(ref statistics.Entries), Interlocked.Read(ref statistics.Bytes),
            Interlocked.Read(ref statistics.Hits), Interlocked.Read(ref statistics.Misses),
            Interlocked.Read(ref statistics.Invalidations));
    }

    private Statistics GetStatistics(string group)
    {
        CacheKeys.ValidateGroup(group);
        return _statistics.GetOrAdd(group, _ => new Statistics());
    }

    private static TagList Tags(string group, string level)
    {
        CacheKeys.ValidateGroup(group);
        if (level is not ("L1" or "L2"))
            throw new ArgumentException("Cache level must be L1 or L2.", nameof(level));
        return new TagList { { "cache.name", group }, { "cache.level", level } };
    }

    private IEnumerable<Measurement<long>> Measurements(Func<Statistics, long> value) =>
        _statistics.Select(pair => new Measurement<long>(value(pair.Value), Tags(pair.Key, "L1")));

    private sealed class Statistics
    {
        public long Entries, Bytes, Hits, Misses, Invalidations;
    }

    public sealed record CacheStatistics(long Entries, long Bytes, long Hits, long Misses, long Invalidations);
}
