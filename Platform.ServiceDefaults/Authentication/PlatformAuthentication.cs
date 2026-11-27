using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Platform.ServiceDefaults.Authentication;

public static class PlatformAuthentication
{
    public const string Scheme = "PlatformBearer";
    public const string BusinessPolicy = "BusinessApi";

    public static IServiceCollection AddPlatformAuthentication(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        var provider = configuration["Authentication:Provider"];
        if (provider is not ("IdentityJwt" or "IdentityServer" or "Keycloak"))
            throw new InvalidOperationException("Authentication:Provider must be IdentityJwt, IdentityServer or Keycloak.");

        var validation = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = "unique_name",
            RoleClaimType = "role"
        };
        string? authority = null;
        bool requireHttps = true;
        if (provider == "IdentityJwt")
        {
            var issuer = configuration["Jwt:Issuer"];
            var audience = configuration["Jwt:Audience"];
            var secret = configuration["Jwt:SecretKey"];
            if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience) ||
                string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
                throw new InvalidOperationException("IdentityJwt requires Jwt:Issuer, Jwt:Audience and a Jwt:SecretKey of at least 32 UTF-8 bytes.");
            validation.ValidIssuer = issuer;
            validation.ValidAudience = audience;
            validation.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            validation.ValidAlgorithms = [SecurityAlgorithms.HmacSha256];
        }
        else
        {
            authority = configuration["Authentication:Authority"];
            var audience = configuration["Authentication:Audience"];
            requireHttps = configuration.GetValue("Authentication:RequireHttpsMetadata", true);
            if (!Uri.TryCreate(authority, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
                string.IsNullOrWhiteSpace(audience))
                throw new InvalidOperationException("Authentication:Authority and Authentication:Audience are required for external providers.");
            if ((!requireHttps || uri.Scheme != Uri.UriSchemeHttps) && !environment.IsDevelopment())
                throw new InvalidOperationException("HTTP authority or metadata is allowed only in Development.");
            if (uri.Scheme == Uri.UriSchemeHttp && requireHttps)
                throw new InvalidOperationException("HTTP authority requires Authentication:RequireHttpsMetadata=false in Development.");
            validation.ValidAudience = audience;
        }

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = Scheme;
            options.DefaultChallengeScheme = Scheme;
            options.DefaultForbidScheme = Scheme;
        }).AddJwtBearer(Scheme, options =>
        {
            options.MapInboundClaims = false;
            options.Authority = authority;
            options.RequireHttpsMetadata = requireHttps;
            options.TokenValidationParameters = validation;
        });
        services.AddAuthorization(options => options.AddPolicy(BusinessPolicy, policy =>
            policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser()));
        return services;
    }
}
