using Auth.Api.Authorization;
using Auth.Api.Contracts;
using Auth.Api.Email;
using Auth.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace Auth.Api.Endpoints;

public static class AuthPasswordEndpoints
{
    public static RouteHandlerBuilder MapAuthForgotPassword(this IEndpointRouteBuilder endpoints)
        => endpoints.MapPost("/auth/forgot-password", ForgotPasswordAsync)
            .AllowAnonymous()
            .AddEndpointFilter<SessionAntiforgeryFilter>();

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
