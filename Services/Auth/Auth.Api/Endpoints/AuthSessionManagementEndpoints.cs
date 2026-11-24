using Auth.Api.Authorization;
using Auth.Api.Identity;
using Auth.Api.Tokens;
using Microsoft.AspNetCore.Authorization;

namespace Auth.Api.Endpoints;

public static class AuthSessionManagementEndpoints
{
    public static void MapAuthSessionManagement(this IEndpointRouteBuilder endpoints)
    {
        var auth = new AuthorizeAttribute { AuthenticationSchemes = AuthIdentityRegistration.BearerScheme };
        endpoints.MapPost("/auth/revoke-token", RevokeAsync).RequireAuthorization(auth)
            .AddEndpointFilter<SessionAntiforgeryFilter>();
        endpoints.MapPost("/auth/logout", LogoutAsync).RequireAuthorization(auth)
            .AddEndpointFilter<SessionAntiforgeryFilter>();
    }

    private static async Task<IResult> RevokeAsync(HttpContext context, IRefreshTokenService sessions,
        CancellationToken cancellationToken)
    {
        var subject = context.User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(subject)) return Results.Forbid();
        var token = RefreshTokenCookie.Read(context.Request);
        if (string.IsNullOrWhiteSpace(token)) return Results.BadRequest();
        if (!await sessions.RevokeAsync(subject, token, cancellationToken)) return Results.Forbid();
        RefreshTokenCookie.Clear(context.Response);
        return Results.NoContent();
    }

    private static async Task<IResult> LogoutAsync(HttpContext context, IRefreshTokenService sessions,
        CancellationToken cancellationToken)
    {
        var subject = context.User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(subject)) return Results.Forbid();
        var token = RefreshTokenCookie.Read(context.Request);
        if (!string.IsNullOrWhiteSpace(token) && !await sessions.RevokeAsync(subject, token, cancellationToken))
            return Results.Forbid();
        RefreshTokenCookie.Clear(context.Response);
        return Results.NoContent();
    }
}
