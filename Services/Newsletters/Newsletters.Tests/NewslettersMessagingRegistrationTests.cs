using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newsletters.Api.Messaging;
using Newsletters.Bll;
using Newsletters.Bll.Events;
using Newsletters.Dal.Data;
using Newsletters.Dal.Events;
using Platform.Caching;
using Platform.Messaging;
using Xunit;

namespace Newsletters.Tests;

public sealed class NewslettersMessagingRegistrationTests
{
    [Fact]
    public async Task Projection_handlers_and_database_contexts_are_scoped_to_each_delivery()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:messaging"] = "amqp://guest:guest@localhost:5672";
        builder.Configuration["ConnectionStrings:NewslettersDb"] = "Host=localhost;Database=unused;Username=unused;Password=unused";
        builder.Services.AddNewslettersBll(builder.Configuration);
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddPlatformCaching();
        builder.AddNewslettersMessaging();
        using var host = builder.Build();
        await using var first = host.Services.CreateAsyncScope();
        await using var second = host.Services.CreateAsyncScope();
        var firstHandler = first.ServiceProvider.GetRequiredService<IIntegrationEventHandler>();
        Assert.IsType<AccountEventHandler>(firstHandler);
        Assert.NotSame(firstHandler, second.ServiceProvider.GetRequiredService<IIntegrationEventHandler>());
        Assert.IsType<AccountEventProjection>(first.ServiceProvider.GetRequiredService<IAccountEventProjection>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<NewslettersDbContext>(), second.ServiceProvider.GetRequiredService<NewslettersDbContext>());
        Assert.Equal(RabbitMqTopology.NewslettersQueue, host.Services.GetRequiredService<RabbitMqConsumerOptions>().Queue);
    }
}
