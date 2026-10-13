using Accounts.Dal;
using Microsoft.Extensions.DependencyInjection;

namespace Accounts.Bll;

public static class DependencyInjection
{
    public static IServiceCollection AddAccountsBll(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddAccountsDal(connectionString);
        services.AddScoped<IAccountsService, AccountsService>();
        return services;
    }
}
