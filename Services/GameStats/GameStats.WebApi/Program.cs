using System.Diagnostics;
using GameStats.Application;
using GameStats.Application.Caching;
using GameStats.Domain;
using GameStats.Infrastructure;
using GameStats.WebApi;
using GameStats.WebApi.Grpc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using MongoDB.Driver;
using OpenTelemetry.Trace;
using Platform.ServiceDefaults;
using Platform.Caching;
using Platform.ServiceDefaults.Grpc;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.WebHost.ConfigureKestrel(options =>
    options.ConfigureEndpointDefaults(endpoint => endpoint.Protocols = HttpProtocols.Http1AndHttp2));
builder.Services.AddGrpc(options => options.Interceptors.Add<GrpcLoggingInterceptor>());

var connectionString = builder.Configuration.GetConnectionString("GameStatsDb");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Connection string 'GameStatsDb' is required.");

builder.Services.AddGameStatsInfrastructure(connectionString);
builder.AddRedisDistributedCache("redis");
builder.Services.AddPlatformCaching();
builder.Services.AddScoped<IPlayerStatisticsService, PlayerStatisticsService>();
builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddSource(MongoTelemetry.ActivitySourceName));
builder.Services.AddHealthChecks().AddCheck<MongoReadinessCheck>("mongodb", timeout: TimeSpan.FromSeconds(5));
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using var source = new ActivitySource("Platform.GameStats");
app.MapGet("/api/stats/users/{userId:long}", async Task<IResult> (
    long userId, IPlayerStatisticsService service, ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    if (userId <= 0)
        return Results.Problem(statusCode: 400, title: "User ID must be positive.");

    using var activity = source.StartActivity("GetPlayerStatistics");
    activity?.SetTag("user.id", userId);
    var statistics = await service.GetByUserIdAsync(userId, cancellationToken);
    if (statistics is null)
        return Results.Problem(statusCode: 404, title: "Player statistics not found.");

    loggerFactory.CreateLogger("GameStats.Statistics")
        .LogInformation("Retrieved statistics for user {UserId}", userId);
    return Results.Ok(statistics);
})
    .WithName("GetPlayerStatistics")
    .Produces<PlayerStatistics>()
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status500InternalServerError);

app.MapDefaultEndpoints();
app.MapGrpcService<GameStatsGrpcService>();
app.Run();
