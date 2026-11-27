using Microsoft.OpenApi.Models;
using Platform.ApiGateway;
using Platform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new OpenApiInfo
{
    Title = "Platform Gateway",
    Version = "v1",
    Description = "Forwards Accounts, Newsletters, GameStats and Aggregator APIs. " +
        "Request and response schemas are documented by each backend service."
}));

var app = builder.Build();
app.UseRouting();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GatewayTrafficMiddleware>();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapReverseProxy();
app.MapDefaultEndpoints();
app.Run();
