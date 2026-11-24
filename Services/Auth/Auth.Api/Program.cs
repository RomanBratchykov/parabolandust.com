using Auth.Api.Email;
using Auth.Api.Identity;
using Auth.Api.Tokens;
using Microsoft.AspNetCore.Antiforgery;
using Platform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var jwtSettings = builder.Configuration.GetRequiredSection("Jwt").Get<JwtSettings>()!;
jwtSettings.Validate();
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddAuthentication(AuthIdentityRegistration.BearerScheme)
    .AddJwtBearer(AuthIdentityRegistration.BearerScheme, options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = jwtSettings.CreateValidationParameters();
    });
builder.Services.AddAuthorization();
builder.Services.AddAuthDataProtection(builder.Configuration);
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IAuthEmailSender>(
        new DevelopmentEmailSender(builder.Environment, builder.Configuration));
}
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "__Host-parabolandust-csrf";
    options.Cookie.Path = "/";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/auth/csrf-token", (HttpContext context, IAntiforgery antiforgery) =>
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    return Results.Ok(new { requestToken = tokens.RequestToken });
}).AllowAnonymous();
app.MapDefaultEndpoints();
app.Run();
