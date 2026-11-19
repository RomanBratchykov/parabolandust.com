using GameStats.Application.Events;
using GameStats.Infrastructure.Events;
using Platform.Messaging;
using Platform.ServiceDefaults.Messaging;

namespace GameStats.WebApi.Messaging;

public static class GameStatsMessagingRegistration
{
    public static TBuilder AddGameStatsMessaging<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.AddPlatformMessaging();
        builder.Services.AddScoped<IAccountEventProjection, MongoAccountEventProjection>();
        builder.Services.AddPlatformEventConsumer<AccountEventHandler>(new(RabbitMqTopology.GameStatsQueue));
        return builder;
    }
}
