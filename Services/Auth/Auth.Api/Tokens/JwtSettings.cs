using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Auth.Api.Tokens;

public sealed class JwtSettings
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience))
            throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience are required.");

        if (string.IsNullOrWhiteSpace(SecretKey) || Encoding.UTF8.GetByteCount(SecretKey) < 32)
            throw new InvalidOperationException("Jwt:SecretKey must contain at least 32 UTF-8 bytes; supply it through secrets or environment configuration.");
    }

    internal SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SecretKey));

    internal TokenValidationParameters CreateValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Issuer,
        ValidateAudience = true,
        ValidAudience = Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = CreateSigningKey(),
        RequireSignedTokens = true,
        RequireExpirationTime = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        NameClaimType = "unique_name",
        RoleClaimType = "role"
    };
}
