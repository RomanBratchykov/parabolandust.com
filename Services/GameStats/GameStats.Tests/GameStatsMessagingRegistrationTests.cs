using GameStats.Application.Events;
using GameStats.Infrastructure;
using GameStats.Infrastructure.Events;
using GameStats.WebApi.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Platform.Caching;
using Platform.Messaging;
using Xunit;

namespace GameStats.Tests;

public sealed class GameStatsMessagingRegistrationTests
{
    [Fact]
    public async Task GameStats_resolves_scoped_transactional_handlers_on_its_own_queue()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:messaging"] = "amqp://guest:guest@localhost:5672";
        builder.Services.AddGameStatsInfrastructure("mongodb://localhost:27017/unused");
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddPlatformCaching();
        builder.AddGameStatsMessaging();
        using var host = builder.Build();
        await using var first = host.Services.CreateAsyncScope();
        await using var second = host.Services.CreateAsyncScope();
        var handler = first.ServiceProvider.GetRequiredService<IIntegrationEventHandler>();
        Assert.IsType<AccountEventHandler>(handler);
        Assert.NotSame(handler, second.ServiceProvider.GetRequiredService<IIntegrationEventHandler>());
        Assert.IsType<MongoAccountEventProjection>(first.ServiceProvider.GetRequiredService<IAccountEventProjection>());
        Assert.Equal(RabbitMqTopology.GameStatsQueue, host.Services.GetRequiredService<RabbitMqConsumerOptions>().Queue);
    }
}
