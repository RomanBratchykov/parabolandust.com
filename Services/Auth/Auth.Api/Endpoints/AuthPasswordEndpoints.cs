using Auth.Api.Authorization;
using Auth.Api.Contracts;
using Auth.Api.Data;
using Auth.Api.Email;
using Auth.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Auth.Api.Endpoints;

public static class AuthPasswordEndpoints
{
    public static RouteHandlerBuilder MapAuthForgotPassword(this IEndpointRouteBuilder endpoints)
        => endpoints.MapPost("/auth/forgot-password", ForgotPasswordAsync)
            .AllowAnonymous()
            .AddEndpointFilter<SessionAntiforgeryFilter>();

    public static RouteHandlerBuilder MapAuthResetPassword(this IEndpointRouteBuilder endpoints)
        => endpoints.MapPost("/auth/reset-password", ResetPasswordAsync)
            .AllowAnonymous()
            .AddEndpointFilter<SessionAntiforgeryFilter>();

    private static async Task<IResult> ResetPasswordAsync(ResetPasswordRequest request,
        UserManager<ApplicationUser> users, ApplicationDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) || request.UserId.Length > 450 ||
            string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > 4096 ||
            string.IsNullOrWhiteSpace(request.NewPassword))
            return InvalidReset();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = await users.FindByIdAsync(request.UserId);
        if (user is null || !user.EmailConfirmed)
            return InvalidReset();

        var result = await users.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
            return InvalidReset();

        var revokedAt = DateTimeOffset.UtcNow;
        await db.RefreshTokens.Where(token => token.UserId == user.Id && token.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(token => token.RevokedAt, (DateTimeOffset?)revokedAt),
                cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    private static IResult InvalidReset() => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest, title: "Invalid password reset request.");

    private static async Task<IResult> ForgotPasswordAsync(ForgotPasswordRequest request,
        UserManager<ApplicationUser> users, IAuthEmailSender mail, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 256)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Provide an email of at most 256 characters.");

        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is not null && user.EmailConfirmed)
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            await mail.SendPasswordResetAsync(user, token, cancellationToken);
        }

        return Results.Ok(new { message = "If the account is eligible, a reset message has been sent." });
    }
}
