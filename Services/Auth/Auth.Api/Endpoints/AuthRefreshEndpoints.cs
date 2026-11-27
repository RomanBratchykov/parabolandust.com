using Auth.Api.Authorization;
using Auth.Api.Models;
using Auth.Api.Tokens;
using Microsoft.AspNetCore.Identity;

namespace Auth.Api.Endpoints;

public static class AuthRefreshEndpoints
{
    public static RouteHandlerBuilder MapAuthRefresh(this IEndpointRouteBuilder endpoints)
        => endpoints.MapPost("/auth/refresh-token", RefreshAsync)
            .AllowAnonymous()
            .AddEndpointFilter<SessionAntiforgeryFilter>();

    private static async Task<IResult> RefreshAsync(HttpContext context,
        UserManager<ApplicationUser> users, IJwtTokenService tokens,
        IRefreshTokenService refreshTokens, CancellationToken cancellationToken)
    {
        context.Response.Headers["Cache-Control"] = "no-store";
        context.Response.Headers["Pragma"] = "no-cache";
        var rawToken = RefreshTokenCookie.Read(context.Request);
        if (string.IsNullOrWhiteSpace(rawToken))
            return Results.Unauthorized();

        var replacement = await refreshTokens.RotateAsync(rawToken, cancellationToken);
        if (replacement is null)
            return Results.Unauthorized();

        var user = await users.FindByIdAsync(replacement.UserId);
        if (user is null || !user.EmailConfirmed || user.TwoFactorEnabled ||
            await users.IsLockedOutAsync(user))
        {
            await refreshTokens.RevokeAsync(replacement.UserId, replacement.Token, cancellationToken);
            return Results.Unauthorized();
        }

        var roles = await users.GetRolesAsync(user);
        var accessToken = tokens.GenerateAccessToken(user, roles);
        RefreshTokenCookie.Write(context.Response, replacement);
        return Results.Ok(new { accessToken, tokenType = "Bearer", expiresIn = 900 });
    }
}
