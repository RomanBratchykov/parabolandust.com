using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using Platform.Messaging;
using Platform.Messaging.Diagnostics;

namespace Platform.ServiceDefaults.Messaging;

public static class MessagingRegistration
{
    public static TBuilder AddPlatformMessaging<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.AddRabbitMQClient("messaging", settings =>
        {
            settings.DisableHealthChecks = true;
            settings.DisableAutoActivation = true;
        }, factory =>
        {
            factory.AutomaticRecoveryEnabled = true;
            // The worker recreates subscriptions itself when a channel closes.
            factory.TopologyRecoveryEnabled = false;
        });
        builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
        builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddSource(TracePropagation.SourceName));
        builder.Services.AddHealthChecks().AddCheck<RabbitMqReadinessCheck>("messaging",
            tags: ["messaging", "rabbitmq", "ready"], timeout: TimeSpan.FromSeconds(5));
        return builder;
    }

    public static IServiceCollection AddPlatformEventConsumer<THandler>(this IServiceCollection services,
        RabbitMqConsumerOptions options) where THandler : class, IIntegrationEventHandler
    {
        services.AddSingleton(options);
        services.AddScoped<IIntegrationEventHandler, THandler>();
        services.AddSingleton<RabbitMqDeliveryProcessor>();
        services.AddSingleton<RabbitMqEventConsumer>();
        services.AddHostedService(provider => provider.GetRequiredService<RabbitMqEventConsumer>());
        return services;
    }
}
