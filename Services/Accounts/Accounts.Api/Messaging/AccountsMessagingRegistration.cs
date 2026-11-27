using Accounts.Bll.Events;
using Platform.ServiceDefaults;
using Platform.ServiceDefaults.Messaging;

namespace Accounts.Api.Messaging;

public static class AccountsMessagingRegistration
{
    public static TBuilder AddAccountsMessaging<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.AddPlatformMessaging();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IEventCorrelation, HttpEventCorrelation>();
        return builder;
    }

    private sealed class HttpEventCorrelation(IHttpContextAccessor accessor) : IEventCorrelation
    {
        public string? Current => accessor.HttpContext?.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();
    }
}
