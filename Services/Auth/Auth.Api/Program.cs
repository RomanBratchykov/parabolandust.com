using Auth.Api.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Platform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var jwtSettings = builder.Configuration.GetRequiredSection("Jwt").Get<JwtSettings>()!;
jwtSettings.Validate();
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = jwtSettings.CreateValidationParameters();
    });
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultEndpoints();
app.Run();
