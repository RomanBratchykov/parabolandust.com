namespace Platform.Caching;

public sealed record CachePolicy
{
    public const long MemorySizeLimit = 10_000;
    public static TimeSpan RedisTimeout { get; } = TimeSpan.FromSeconds(1);
    public static TimeSpan DashboardExpiration { get; } = TimeSpan.FromSeconds(30);
    public static CachePolicy Accounts { get; } = new(
        TimeSpan.FromSeconds(40), TimeSpan.FromSeconds(45), TimeSpan.FromSeconds(60));
    public static CachePolicy PlayerProjection => Accounts;
    public static CachePolicy GameStats => Accounts;
    public static CachePolicy Newsletters { get; } = new(
        TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(300));

    public TimeSpan SlidingExpiration { get; }
    public TimeSpan MemoryExpiration { get; }
    public TimeSpan DistributedExpiration { get; }
    public long Size => 1;

    public CachePolicy(TimeSpan slidingExpiration, TimeSpan memoryExpiration, TimeSpan distributedExpiration)
    {
        if (slidingExpiration <= TimeSpan.Zero || slidingExpiration > memoryExpiration)
            throw new ArgumentOutOfRangeException(nameof(slidingExpiration),
                "Sliding expiration must be positive and no longer than the memory lifetime.");
        if (memoryExpiration >= distributedExpiration)
            throw new ArgumentOutOfRangeException(nameof(distributedExpiration),
                "The Redis lifetime must be longer than the memory lifetime.");

        SlidingExpiration = slidingExpiration;
        MemoryExpiration = memoryExpiration;
        DistributedExpiration = distributedExpiration;
    }
}
