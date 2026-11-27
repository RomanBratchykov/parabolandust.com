using Auth.Api.Authorization;
using Auth.Api.Contracts;
using Auth.Api.Data;
using Auth.Api.Identity;
using Auth.Api.Models;
using Auth.Api.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Auth.Api.Endpoints;

public static class AuthChangePasswordEndpoints
{
    public static RouteHandlerBuilder MapAuthChangePassword(this IEndpointRouteBuilder endpoints)
        => endpoints.MapPost("/auth/change-password", ChangePasswordAsync)
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = AuthIdentityRegistration.BearerScheme })
            .AddEndpointFilter<SessionAntiforgeryFilter>();

    private static async Task<IResult> ChangePasswordAsync(ChangePasswordRequest request, HttpContext context,
        UserManager<ApplicationUser> users, ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var subject = context.User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(subject)) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            return InvalidPassword();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = await users.FindByIdAsync(subject);
        if (user is null) return Results.Forbid();
        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded) return InvalidPassword();
        var now = DateTimeOffset.UtcNow;
        await db.RefreshTokens.Where(token => token.UserId == subject && token.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(token => token.RevokedAt, (DateTimeOffset?)now), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        RefreshTokenCookie.Clear(context.Response);
        return Results.NoContent();
    }

    private static IResult InvalidPassword() => Results.Problem(statusCode: StatusCodes.Status400BadRequest,
        title: "Invalid password change request.");
}
