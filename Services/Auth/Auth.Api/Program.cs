using Auth.Api.Email;
using Auth.Api.Data;
using Auth.Api.Endpoints;
using Auth.Api.Identity;
using Auth.Api.Tokens;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Platform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var identityConnection = builder.Configuration.GetConnectionString("IdentityDb");
if (string.IsNullOrWhiteSpace(identityConnection))
    throw new InvalidOperationException("Connection string 'IdentityDb' is required.");
builder.Services.AddAuthIdentity(options => options.UseNpgsql(identityConnection));

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
await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await database.Database.MigrateAsync();
    await IdentityRoleInitializer.InitializeAsync(scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
}
app.UseAuthentication();
app.UseAuthorization();
app.MapAuthRegistration();
app.MapAuthEmailConfirmation();
app.MapAuthResendVerification();
app.MapAuthForgotPassword();
app.MapAuthResetPassword();
app.MapAuthLogin();
app.MapAuthRefresh();
app.MapGet("/auth/csrf-token", (HttpContext context, IAntiforgery antiforgery) =>
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    return Results.Ok(new { requestToken = tokens.RequestToken });
}).AllowAnonymous();
app.MapDefaultEndpoints();
app.Run();
