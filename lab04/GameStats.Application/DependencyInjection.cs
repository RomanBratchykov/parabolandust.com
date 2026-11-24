using FluentValidation;
using GameStats.Application.Common.Behaviors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameStats.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services, IConfiguration config)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.LicenseKey = config["LuckyPenny:LicenseKey"];
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));      // 1: зовнішній
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));   // 2
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));  // 3: біля обробника
        });

        services.AddValidatorsFromAssembly(assembly);
        return services;
    }
}