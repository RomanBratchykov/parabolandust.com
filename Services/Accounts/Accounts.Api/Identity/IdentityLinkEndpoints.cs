using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using Accounts.Bll;
using Microsoft.AspNetCore.Authorization;
using Npgsql;
using Platform.ServiceDefaults.Authentication;

namespace Accounts.Api.Identity;

public static class IdentityLinkEndpoints
{
    public static void MapIdentityLinks(this IEndpointRouteBuilder endpoints)
    {
        var authenticated = new AuthorizeAttribute { AuthenticationSchemes = PlatformAuthentication.Scheme };
        endpoints.MapGet("/internal/identity/self", ResolveAsync).RequireAuthorization(authenticated);
        endpoints.MapPost("/internal/identity/self", ProvisionAsync).RequireAuthorization(authenticated);
    }

    private static async Task<IResult> ResolveAsync(ClaimsPrincipal caller, IConfiguration configuration,
        IdentityLinkService links, CancellationToken cancellationToken)
    {
        if (!TryIdentity(caller, configuration, out var issuer, out var subject)) return Results.Forbid();
        var id = await links.ResolveAsync(issuer, subject, cancellationToken);
        return id is null ? Results.Forbid() : Results.Ok(new { userId = id.Value });
    }

    private static async Task<IResult> ProvisionAsync(ClaimsPrincipal caller, IConfiguration configuration,
        IdentityLinkService links, CancellationToken cancellationToken)
    {
        if (!TryIdentity(caller, configuration, out var issuer, out var subject)) return Results.Forbid();
        var email = SingleClaim(caller, "email");
        var verified = SingleClaim(caller, "email_verified");
        var name = SingleClaim(caller, "unique_name");
        if (!string.Equals(verified, "true", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(email) || email.Length > 254 || !new EmailAddressAttribute().IsValid(email) ||
            string.IsNullOrWhiteSpace(name) || name.Length > 50)
            return Results.Forbid();

        try
        {
            var id = await links.ProvisionAsync(issuer, subject, name, email, cancellationToken);
            return id is null ? Results.Forbid() : Results.Ok(new { userId = id.Value });
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // A matching email/name is never proof of ownership of an existing account.
            return Results.Conflict();
        }
    }

    private static bool TryIdentity(ClaimsPrincipal caller, IConfiguration configuration,
        out string issuer, out string subject)
    {
        issuer = SingleClaim(caller, "iss") ?? string.Empty;
        subject = SingleClaim(caller, "sub") ?? string.Empty;
        return configuration["Authentication:Provider"] == "IdentityJwt" &&
            !string.IsNullOrWhiteSpace(issuer) && !string.IsNullOrWhiteSpace(subject) &&
            Encoding.UTF8.GetByteCount(issuer) <= 1024 && Encoding.UTF8.GetByteCount(subject) <= 512;
    }

    private static string? SingleClaim(ClaimsPrincipal caller, string type)
    {
        var claims = caller.FindAll(type).Take(2).ToArray();
        return claims.Length == 1 ? claims[0].Value : null;
    }
}
