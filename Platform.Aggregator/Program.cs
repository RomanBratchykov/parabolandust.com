using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.OpenApi.Models;
using Platform.Aggregator;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Services;
using Platform.ServiceDefaults;
using Platform.Caching;
using Platform.Aggregator.Caching;
using Platform.ServiceDefaults.Authentication;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddPlatformAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddAuthorization(options => options.AddPolicy("InterimAdmin", policy =>
    policy.AddAuthenticationSchemes(PlatformAuthentication.Scheme).RequireRole("Admin")));
builder.AddRedisDistributedCache("redis", settings => settings.DisableHealthChecks = true);
builder.Services.AddPlatformCaching();
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.AddOptions<DownstreamOptions>()
    .BindConfiguration("Downstream")
    .Validate(options => options.TimeoutSeconds is >= 1 and <= 30,
        "Downstream:TimeoutSeconds must be between 1 and 30 seconds.")
    .ValidateOnStart();
builder.Services.AddProblemDetails();
builder.Services.AddDashboardClients();
builder.Services.AddGrpcDashboardClients();
builder.Services.AddScoped<PlayerDashboardService>();
builder.Services.AddScoped<PlayerDashboardCache>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new OpenApiInfo
{
    Title = "Player Dashboard Aggregator",
    Version = "v1"
}));

var app = builder.Build();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers().RequireAuthorization("InterimAdmin");
app.MapDefaultEndpoints();
app.Run();

namespace Platform.Aggregator
{
    public sealed class DownstreamOptions
    {
        public int TimeoutSeconds { get; init; } = 5;
    }
}
