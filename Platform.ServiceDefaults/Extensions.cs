using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;

namespace Platform.ServiceDefaults;

public static class Extensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.AddDefaultLogging();
        builder.AddDefaultTelemetry();
        builder.AddDefaultHealthChecks();
        var service = builder.Environment.ApplicationName;
        var prefix = service == "Platform.Aggregator" ? "dashboard" : service.Split('.')[0].ToLowerInvariant();
        builder.Services.AddSingleton(provider => new BusinessMetrics(
            provider.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>(), service, prefix));
        builder.Services.AddServiceDiscovery();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTransient<CorrelationIdHandler>();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler(options =>
                options.Retry.DisableForUnsafeHttpMethods());
            http.AddServiceDiscovery();
            http.AddHttpMessageHandler<CorrelationIdHandler>();
        });

        return builder;
    }
}
