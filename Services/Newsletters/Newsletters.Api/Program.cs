using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Newsletters.Api.Grpc;
using Newsletters.Api.ExceptionHandling;
using Newsletters.Bll;
using Newsletters.Bll.Caching;
using Newsletters.Dal.Data;
using Newsletters.Dal.Seeding;
using Platform.ServiceDefaults;
using Platform.ServiceDefaults.Grpc;
using Platform.Caching;
using Serilog;
using Serilog.Context;
using Newsletters.Api.Messaging;
using Platform.ServiceDefaults.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddPlatformAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddAuthorization(options => options.AddPolicy("InterimAdmin", policy =>
    policy.AddAuthenticationSchemes(PlatformAuthentication.Scheme).RequireRole("Admin")));
builder.AddNewslettersMessaging();
builder.AddRedisDistributedCache("redis", options => options.DisableHealthChecks = true);
builder.Services.AddPlatformCaching();

builder.Services.AddNewslettersBll(builder.Configuration);
builder.Services.AddHealthChecks().AddNpgSql(builder.Configuration.GetConnectionString("NewslettersDb")!,
    healthQuery: "SELECT 1", name: "newsletters-db", tags: ["database", "postgresql", "ready"],
    timeout: TimeSpan.FromSeconds(5));
builder.Services.AddNewslettersCaching();
builder.WebHost.ConfigureKestrel(options => options.ConfigureEndpointDefaults(endpoint =>
    endpoint.Protocols = HttpProtocols.Http1AndHttp2));
builder.Services.AddGrpc(options => options.Interceptors.Add<GrpcLoggingInterceptor>());
builder.Services.AddGrpcHealthChecks();
builder.Services.AddControllers();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<NewsletterExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
    using (LogContext.PushProperty("UserId", userId))
    using (LogContext.PushProperty("RequestPath", context.Request.Path))
    {
        await next();
    }
});
app.UseSerilogRequestLogging();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers().RequireAuthorization("InterimAdmin");
app.MapGrpcService<NewslettersGrpcService>().RequireAuthorization("InterimAdmin");
app.MapGrpcHealthChecksService();
app.MapDefaultEndpoints();

await using (var scope = app.Services.CreateAsyncScope())
{
    var context = scope.ServiceProvider.GetRequiredService<NewslettersDbContext>();
    await context.Database.MigrateAsync();
    if (!app.Environment.IsProduction())
        await DbInitializer.SeedAsync(context);
}

app.Run();
