using Microsoft.OpenApi.Models;
using Platform.ApiGateway;
using Platform.ServiceDefaults;
using Platform.ServiceDefaults.Authentication;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddPlatformAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(context => context.AddRequestHeaderRemove("X-User-Id"))
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
app.UseAuthentication();
app.UseAuthorization();
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
