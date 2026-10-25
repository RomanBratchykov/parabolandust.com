using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newsletters.Dal.Data;
using Newsletters.Dal.Repositories;

namespace Newsletters.Dal;

public static class DependencyInjection
{
    public static IServiceCollection AddNewslettersDal(
        this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<NewslettersDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<INewsletterRepository, NewsletterRepository>();
        services.AddScoped<IUserCopyRepository, UserCopyRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}
