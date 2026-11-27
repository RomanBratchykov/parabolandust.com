using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Auth.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace Auth.Api.Tokens;

public sealed class JwtTokenService(JwtSettings settings) : IJwtTokenService
{
    public string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(user.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(user.UserName);
        if (!user.EmailConfirmed || string.IsNullOrWhiteSpace(user.Email))
            throw new InvalidOperationException("A confirmed email is required to issue an access token.");
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("email_verified", "true", ClaimValueTypes.Boolean),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(now).ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64)
        };
        claims.AddRange(roles.Distinct(StringComparer.Ordinal).Select(role => new Claim("role", role)));
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims,
            notBefore: now, expires: now.AddMinutes(15),
            signingCredentials: new SigningCredentials(settings.CreateSigningKey(), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // Persistence and cookie delivery belong to the session lifecycle, not this generator.
    public string GenerateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public ClaimsPrincipal? ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            return handler.ValidateToken(token, settings.CreateValidationParameters(), out _);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
