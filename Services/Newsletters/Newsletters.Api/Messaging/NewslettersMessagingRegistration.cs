using Newsletters.Bll.Events;
using Newsletters.Dal.Events;
using Platform.Messaging;
using Platform.ServiceDefaults.Messaging;

namespace Newsletters.Api.Messaging;

public static class NewslettersMessagingRegistration
{
    public static TBuilder AddNewslettersMessaging<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.AddPlatformMessaging();
        builder.Services.AddScoped<IAccountEventProjection, AccountEventProjection>();
        builder.Services.AddPlatformEventConsumer<AccountEventHandler>(new(RabbitMqTopology.NewslettersQueue));
        return builder;
    }
}
