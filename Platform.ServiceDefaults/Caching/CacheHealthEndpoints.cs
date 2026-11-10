using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Platform.Caching;

namespace Platform.ServiceDefaults.Caching;

public static class CacheHealthEndpoints
{
    public static IServiceCollection AddCacheHealthChecks(this IServiceCollection services)
    {
        if (services.Any(service => service.ServiceType == typeof(CacheHealthCheck))) return services;
        services.TryAddSingleton<CacheHealthCheck>();
        services.AddHealthChecks().AddCheck<CacheHealthCheck>(CacheHealthCheck.Name, tags: ["cache"]);
        return services;
    }

    public static WebApplication MapCacheHealthEndpoints(this WebApplication app)
    {
        if (!app.Services.GetRequiredService<IServiceProviderIsService>().IsService(typeof(ICacheAside)))
            return app;

        app.MapHealthChecks("/health/cache", new HealthCheckOptions
        {
            Predicate = registration => registration.Name == CacheHealthCheck.Name,
            ResponseWriter = (context, report) => context.Response.WriteAsJsonAsync(new
            {
                status = report.Status.ToString(),
                caches = report.Entries[CacheHealthCheck.Name].Data.GetValueOrDefault("caches")
            }, cancellationToken: context.RequestAborted)
        });
        return app;
    }
}
