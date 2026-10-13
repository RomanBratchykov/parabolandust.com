using Microsoft.Extensions.DependencyInjection;
using Dapper;

namespace Accounts.Dal;

public static class DependencyInjection
{
    public static IServiceCollection AddAccountsDal(
        this IServiceCollection services,
        string connectionString)
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(connectionString));
        return services;
    }
}
