using Accounts.Api.Messaging;
using Accounts.Bll;
using Accounts.Bll.Caching;
using Accounts.Bll.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Platform.Caching;
using Platform.Messaging;
using Platform.ServiceDefaults;
using Xunit;

namespace Accounts.Tests;

public sealed class AccountsMessagingRegistrationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("caller-93")]
    public async Task Accounts_resolves_with_the_normalized_request_correlation(string? supplied)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:messaging"] = "amqp://guest:guest@localhost:5672";
        builder.Services.AddAccountsBll("Host=localhost;Database=unused;Username=unused;Password=unused");
        builder.Services.AddAccountsCaching();
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddPlatformCaching();
        builder.AddAccountsMessaging();
        builder.Services.AddSingleton<IEventPublisher>(new AccountEventTestDoubles());
        using var host = builder.Build();
        await using var scope = host.Services.CreateAsyncScope();
        Assert.IsType<CachedAccountsService>(scope.ServiceProvider.GetRequiredService<IAccountsService>());
        var context = new DefaultHttpContext();
        if (supplied is not null) context.Request.Headers[CorrelationIdMiddleware.HeaderName] = supplied;
        host.Services.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
        string? captured = null;
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            captured = scope.ServiceProvider.GetRequiredService<IEventCorrelation>().Current;
            return Task.CompletedTask;
        }, NullLogger<CorrelationIdMiddleware>.Instance);
        try { await middleware.InvokeAsync(context); }
        finally { host.Services.GetRequiredService<IHttpContextAccessor>().HttpContext = null; }
        Assert.Equal(context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString(), captured);
        if (supplied is null) Assert.True(Guid.TryParse(captured, out _));
        else Assert.Equal("caller-93", captured);
    }
}
