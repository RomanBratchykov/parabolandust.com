using Microsoft.Extensions.DependencyInjection;

namespace Accounts.Dal;

public static class DependencyInjection
{
    public static IServiceCollection AddAccountsDal(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(connectionString));
        return services;
    }
}
