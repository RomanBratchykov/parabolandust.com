using Auth.Api.Authorization;
using Auth.Api.Contracts;
using Auth.Api.Models;
using Auth.Api.Tokens;
using Microsoft.AspNetCore.Identity;

namespace Auth.Api.Endpoints;

public static class AuthLoginEndpoints
{
    public static RouteHandlerBuilder MapAuthLogin(this IEndpointRouteBuilder endpoints)
        => endpoints.MapPost("/auth/login", LoginAsync)
            .AllowAnonymous()
            .AddEndpointFilter<SessionAntiforgeryFilter>();

    private static async Task<IResult> LoginAsync(LoginRequest request, HttpContext context,
        UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
        IJwtTokenService tokens, IRefreshTokenService refreshTokens, CancellationToken cancellationToken)
    {
        context.Response.Headers["Cache-Control"] = "no-store";
        context.Response.Headers["Pragma"] = "no-cache";
        if (string.IsNullOrWhiteSpace(request.UserName) || request.UserName.Length > 256 ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Provide a username of at most 256 characters and a password.");
        }

        var user = await users.FindByNameAsync(request.UserName.Trim());
        if (user is null || !user.EmailConfirmed)
            return Results.Unauthorized();

        var checkedPassword = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        // This password-only flow must not bypass an account's two-factor requirement.
        if (!checkedPassword.Succeeded || user.TwoFactorEnabled)
            return Results.Unauthorized();

        cancellationToken.ThrowIfCancellationRequested();
        var roles = await users.GetRolesAsync(user);
        var accessToken = tokens.GenerateAccessToken(user, roles);
        var refresh = await refreshTokens.IssueAsync(user.Id, cancellationToken);
        RefreshTokenCookie.Write(context.Response, refresh);
        return Results.Ok(new { accessToken, tokenType = "Bearer", expiresIn = 900 });
    }
}
