using GameStats.Application.Common.Interfaces;
using GameStats.Domain.Interfaces;
using GameStats.Infrastructure.Persistence;
using GameStats.Infrastructure.Seeding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameStats.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        MongoMappings.Register();

        services.Configure<MongoDbOptions>(config.GetSection("MongoDb"));
        services.AddSingleton<MongoDbContext>();

        services.AddScoped<IRunRepository, RunRepository>();
        services.AddScoped<IGameEventRepository, GameEventRepository>();
        services.AddScoped<IRunQueries, RunQueries>();
        services.AddScoped<IStatsQueries, StatsQueries>();
        services.AddScoped<IGameEventQueries, GameEventQueries>();

        services.AddSingleton<IDataSeeder, RunSeeder>();
        services.AddSingleton<IDataSeeder, GameEventSeeder>();

        // порядок важливий: спочатку індекси, потім сідінг
        services.AddHostedService<IndexCreationService>();
        services.AddHostedService<SeedingHostedService>();

        return services;
    }
}