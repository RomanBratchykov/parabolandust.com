using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Caching.Memory;

namespace Platform.Caching;

public static class CacheRegistration
{
    public const string MemoryCacheKey = "Platform.Cache";

    public static IServiceCollection AddPlatformCaching(this IServiceCollection services)
    {
        // Framework consumers may create unsized entries; isolate the bounded domain cache.
        services.AddMemoryCache();
        services.TryAddKeyedSingleton<IMemoryCache>(MemoryCacheKey, (_, _) => new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = CachePolicy.MemorySizeLimit,
            TrackStatistics = true
        }));
        services.AddMetrics();
        services.AddLogging();
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton<CacheMetrics>();
        services.TryAddSingleton<ICacheAside, CacheAside>();
        return services;
    }
}
