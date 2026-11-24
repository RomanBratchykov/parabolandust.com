using Auth.Api.Authorization;
using Auth.Api.Contracts;
using Auth.Api.Data;
using Auth.Api.Email;
using Auth.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace Auth.Api.Endpoints;

public static class AuthEmailConfirmationEndpoints
{
    public static RouteHandlerBuilder MapAuthEmailConfirmation(this IEndpointRouteBuilder endpoints)
        => endpoints.MapPost("/auth/verify-email", VerifyEmailAsync)
            .AllowAnonymous()
            .AddEndpointFilter<SessionAntiforgeryFilter>();

    public static RouteHandlerBuilder MapAuthResendVerification(this IEndpointRouteBuilder endpoints)
        => endpoints.MapPost("/auth/resend-verification", ResendVerificationAsync)
            .AllowAnonymous()
            .AddEndpointFilter<SessionAntiforgeryFilter>();

    private static async Task<IResult> ResendVerificationAsync(ResendVerificationRequest request,
        UserManager<ApplicationUser> users, IAuthEmailSender mail, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 256)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Provide an email of at most 256 characters.");

        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is not null && !user.EmailConfirmed)
        {
            var token = await users.GenerateEmailConfirmationTokenAsync(user);
            await mail.SendConfirmationAsync(user, token, cancellationToken);
        }

        return Results.Ok(new { message = "If the account needs confirmation, a message has been sent." });
    }

    private static async Task<IResult> VerifyEmailAsync(VerifyEmailRequest request,
        UserManager<ApplicationUser> users, ApplicationDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) || request.UserId.Length > 450 ||
            string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > 4096)
            return InvalidConfirmation();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = await users.FindByIdAsync(request.UserId);
        if (user is null || user.EmailConfirmed)
            return InvalidConfirmation();

        var confirmed = await users.ConfirmEmailAsync(user, request.Token);
        if (!confirmed.Succeeded)
            return InvalidConfirmation();

        // Invalidate this user's outstanding confirmation tokens in the same transaction.
        var invalidated = await users.UpdateSecurityStampAsync(user);
        if (!invalidated.Succeeded)
            return InvalidConfirmation();

        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    private static IResult InvalidConfirmation() => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Invalid or expired email confirmation.");
}
