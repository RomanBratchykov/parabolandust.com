using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.OpenApi.Models;
using Platform.Aggregator;
using Platform.Aggregator.Clients;
using Platform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
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
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new OpenApiInfo
{
    Title = "Player Dashboard Aggregator",
    Version = "v1"
}));

var app = builder.Build();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapDefaultEndpoints();
app.Run();

namespace Platform.Aggregator
{
    public sealed class DownstreamOptions
    {
        public int TimeoutSeconds { get; init; } = 5;
    }
}
