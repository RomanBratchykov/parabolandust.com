using Accounts.Bll;
using Accounts.Bll.Caching;
using Accounts.Api.Grpc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Platform.ServiceDefaults.Grpc;
using Accounts.Api.ExceptionHandling;
using Platform.ServiceDefaults;
using Platform.Caching;
using Platform.ServiceDefaults.Caching;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
    options.ConfigureEndpointDefaults(endpoint => endpoint.Protocols = HttpProtocols.Http1AndHttp2));
builder.AddServiceDefaults();
builder.AddRedisDistributedCache("redis");
builder.Services.AddPlatformCaching();
builder.Services.AddCacheHealthChecks();

var connectionString = builder.Configuration.GetConnectionString("AccountsDb");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'AccountsDb' is missing. Set ConnectionStrings:AccountsDb or ConnectionStrings__AccountsDb.");
}

builder.Services.AddAccountsBll(connectionString);
builder.Services.AddAccountsCaching();
builder.Services.AddGrpc(options => options.Interceptors.Add<GrpcLoggingInterceptor>());
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

app.UseAuthorization();

app.MapControllers();
app.MapGrpcService<AccountsGrpcService>();
app.MapDefaultEndpoints();

app.Run();
