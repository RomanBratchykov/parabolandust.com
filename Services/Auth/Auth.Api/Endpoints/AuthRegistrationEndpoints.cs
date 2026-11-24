using Auth.Api.Authorization;
using Auth.Api.Contracts;
using Auth.Api.Data;
using Auth.Api.Email;
using Auth.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace Auth.Api.Endpoints;

public static class AuthRegistrationEndpoints
{
    // Map after the Identity store, roles and mail sender have been configured.
    public static RouteHandlerBuilder MapAuthRegistration(this IEndpointRouteBuilder endpoints)
        => endpoints.MapPost("/auth/register", RegisterAsync)
            .AllowAnonymous()
            .AddEndpointFilter<SessionAntiforgeryFilter>();

    private static async Task<IResult> RegisterAsync(RegisterRequest request,
        UserManager<ApplicationUser> users, ApplicationDbContext db,
        IAuthEmailSender mail, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || request.UserName.Length > 256 ||
            string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 256 ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Provide a username and email of at most 256 characters and a password.");
        }

        var user = new ApplicationUser
        {
            UserName = request.UserName.Trim(),
            Email = request.Email.Trim(),
            EmailConfirmed = false,
            LockoutEnabled = true
        };
        string confirmationToken;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var created = await users.CreateAsync(user, request.Password);
            if (!created.Succeeded)
                return ValidationFailure(created);

            var assigned = await users.AddToRoleAsync(user, "User");
            if (!assigned.Succeeded)
                return ValidationFailure(assigned);

            confirmationToken = await users.GenerateEmailConfirmationTokenAsync(user);
            await transaction.CommitAsync(cancellationToken);
        }

        // Mail failure retains the unconfirmed account; the resend flow can recover it.
        await mail.SendConfirmationAsync(user, confirmationToken, cancellationToken);
        return Results.Created($"/users/{Uri.EscapeDataString(user.Id)}", new
        {
            user.Id,
            user.UserName,
            user.Email
        });
    }

    private static IResult ValidationFailure(IdentityResult result) => Results.ValidationProblem(
        result.Errors.GroupBy(error => error.Code).ToDictionary(
            group => group.Key, group => group.Select(error => error.Description).ToArray()));
}
