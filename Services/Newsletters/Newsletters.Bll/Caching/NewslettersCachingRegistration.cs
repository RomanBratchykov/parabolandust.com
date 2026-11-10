using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Newsletters.Bll.Services;
using Platform.Caching;

namespace Newsletters.Bll.Caching;

public static class NewslettersCachingRegistration
{
    public static IServiceCollection AddNewslettersCaching(this IServiceCollection services)
    {
        services.AddScoped<NewsletterService>();
        services.AddScoped<SubscriptionService>();
        services.Replace(ServiceDescriptor.Scoped<INewsletterService>(provider => new CachedNewsletterService(
            provider.GetRequiredService<NewsletterService>(), provider.GetRequiredService<ICacheAside>(),
            provider.GetRequiredService<CacheInvalidator>())));
        services.Replace(ServiceDescriptor.Scoped<ISubscriptionService>(provider => new CachedSubscriptionService(
            provider.GetRequiredService<SubscriptionService>(), provider.GetRequiredService<CacheInvalidator>())));
        return services;
    }
}
