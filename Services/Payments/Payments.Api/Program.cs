using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Payments.Api.Authorization;
using Platform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var authority = builder.Configuration["Authentication:Keycloak:Authority"];
if (!Uri.TryCreate(authority, UriKind.Absolute, out var authorityUri) ||
    (authorityUri.Scheme != Uri.UriSchemeHttps &&
        !(builder.Environment.IsDevelopment() && authorityUri.Scheme == Uri.UriSchemeHttp)))
{
    throw new InvalidOperationException("Authentication:Keycloak:Authority must be an HTTPS realm URL (HTTP is allowed only in Development).");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority!.TrimEnd('/');
        options.Audience = "payments-api";
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in new[] { "payments:read", "payments:process", "payments:refund" })
    {
        options.AddPolicy(permission, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(new PermissionRequirement("payments-api", permission));
        });
    }
});
builder.Services.AddControllers();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapDefaultEndpoints();
app.Run();
