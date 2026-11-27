using GameStats.Application;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace GameStats.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGameStatsInfrastructure(
        this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var url = MongoUrl.Create(connectionString);
        if (string.IsNullOrWhiteSpace(url.DatabaseName))
            throw new ArgumentException("GameStatsDb must include a database name.", nameof(connectionString));

        services.AddSingleton<IMongoClient>(_ => new MongoClient(MongoClientSettings.FromUrl(url)));
        services.AddSingleton(provider =>
            provider.GetRequiredService<IMongoClient>().GetDatabase(url.DatabaseName));
        services.AddScoped<IPlayerStatisticsRepository, PlayerStatisticsRepository>();
        return services;
    }
}
