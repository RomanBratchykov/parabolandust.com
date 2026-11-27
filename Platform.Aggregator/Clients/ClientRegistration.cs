namespace Platform.Aggregator.Clients;

public static class ClientRegistration
{
    public static IServiceCollection AddDashboardClients(this IServiceCollection services)
    {
        services.AddScoped<AccountsClient>();
        services.AddScoped<NewslettersClient>();
        services.AddScoped<GameStatsClient>();
        return services;
    }

}
