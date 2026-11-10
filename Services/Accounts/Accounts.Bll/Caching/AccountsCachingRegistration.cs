using Microsoft.Extensions.DependencyInjection;

namespace Accounts.Bll.Caching;

public static class AccountsCachingRegistration
{
    public static IServiceCollection AddAccountsCaching(this IServiceCollection services)
    {
        var original = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(IAccountsService))
            ?? throw new InvalidOperationException("Register AddAccountsBll before AddAccountsCaching.");
        services.Remove(original);
        services.AddScoped<IAccountsService>(provider =>
        {
            var inner = original.ImplementationInstance as IAccountsService
                ?? original.ImplementationFactory?.Invoke(provider) as IAccountsService
                ?? (IAccountsService)ActivatorUtilities.CreateInstance(provider, original.ImplementationType!);
            return ActivatorUtilities.CreateInstance<CachedAccountsService>(provider, inner);
        });
        return services;
    }
}
