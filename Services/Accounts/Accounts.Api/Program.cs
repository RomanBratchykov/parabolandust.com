using Accounts.Bll;
using Accounts.Bll.Caching;
using Accounts.Api.Grpc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Platform.ServiceDefaults.Grpc;
using Accounts.Api.ExceptionHandling;
using Platform.ServiceDefaults;
using Platform.Caching;
using Platform.ServiceDefaults.Caching;
using Accounts.Api.Messaging;
using Accounts.Api.Identity;
using Accounts.Dal.Repositories;
using Platform.ServiceDefaults.Authentication;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
    options.ConfigureEndpointDefaults(endpoint => endpoint.Protocols = HttpProtocols.Http1AndHttp2));
builder.AddServiceDefaults();
builder.Services.AddPlatformAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddAuthorization(options => options.AddPolicy("InterimAdmin", policy =>
    policy.AddAuthenticationSchemes(PlatformAuthentication.Scheme).RequireRole("Admin")));
builder.AddAccountsMessaging();
builder.AddRedisDistributedCache("redis", options => options.DisableHealthChecks = true);
builder.Services.AddPlatformCaching();
builder.Services.AddCacheHealthChecks();

var connectionString = builder.Configuration.GetConnectionString("AccountsDb");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'AccountsDb' is missing. Set ConnectionStrings:AccountsDb or ConnectionStrings__AccountsDb.");
}

builder.Services.AddAccountsBll(connectionString);
builder.Services.AddScoped(_ => new IdentityLinkRepository(connectionString));
builder.Services.AddScoped<IdentityLinkService>();
builder.Services.AddHealthChecks().AddNpgSql(connectionString, healthQuery: "SELECT 1",
    name: "accounts-db", tags: ["database", "postgresql", "ready"], timeout: TimeSpan.FromSeconds(5));
builder.Services.AddAccountsCaching();
builder.Services.AddGrpc(options => options.Interceptors.Add<GrpcLoggingInterceptor>());
builder.Services.AddGrpcHealthChecks();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<DomainExceptionMiddleware>();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers().RequireAuthorization("InterimAdmin");
app.MapIdentityLinks();
app.MapGrpcService<AccountsGrpcService>().RequireAuthorization("InterimAdmin");
app.MapGrpcHealthChecksService();
app.MapDefaultEndpoints();

app.Run();
