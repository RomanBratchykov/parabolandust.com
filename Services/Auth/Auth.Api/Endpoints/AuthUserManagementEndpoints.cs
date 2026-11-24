using System.Security.Claims;
using Auth.Api.Contracts;
using Auth.Api.Data;
using Auth.Api.Identity;
using Auth.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Auth.Api.Endpoints;

public static class AuthUserManagementEndpoints
{
    public static void MapAuthUserManagement(this IEndpointRouteBuilder endpoints)
    {
        var authenticated = new AuthorizeAttribute { AuthenticationSchemes = AuthIdentityRegistration.BearerScheme };
        var admin = new AuthorizeAttribute { AuthenticationSchemes = AuthIdentityRegistration.BearerScheme, Roles = "Admin" };
        endpoints.MapGet("/users", ListAsync).RequireAuthorization(admin);
        endpoints.MapGet("/users/{id}", GetAsync).RequireAuthorization(authenticated);
        endpoints.MapPut("/users/{id}", UpdateAsync).RequireAuthorization(authenticated);
        endpoints.MapDelete("/users/{id}", DeleteAsync).RequireAuthorization(admin);
        endpoints.MapGet("/users/{id}/refresh-tokens", TokenHistoryAsync).RequireAuthorization(authenticated);
        endpoints.MapGet("/users/{id}/roles", GetRolesAsync).RequireAuthorization(authenticated);
        endpoints.MapPost("/users/{id}/roles", AddRoleAsync).RequireAuthorization(admin);
        endpoints.MapDelete("/users/{id}/roles/{role}", RemoveRoleAsync).RequireAuthorization(admin);
    }

    private static async Task<IResult> ListAsync(UserManager<ApplicationUser> users, int? page, int? pageSize,
        CancellationToken cancellationToken)
    {
        var number = page ?? 1;
        var size = pageSize ?? 25;
        if (number < 1 || number > 100000 || size < 1 || size > 100) return Results.BadRequest();
        var items = await users.Users.AsNoTracking().OrderBy(user => user.Id)
            .Skip((number - 1) * size).Take(size)
            .Select(user => new { user.Id, user.UserName, user.Email, user.EmailConfirmed })
            .ToListAsync(cancellationToken);
        return Results.Ok(items);
    }

    private static async Task<IResult> GetAsync(string id, ClaimsPrincipal caller, UserManager<ApplicationUser> users)
    {
        if (!CanAccess(caller, id)) return Results.Forbid();
        var user = await users.FindByIdAsync(id);
        return user is null ? Results.NotFound() : Results.Ok(new { user.Id, user.UserName, user.Email, user.EmailConfirmed });
    }

    private static async Task<IResult> UpdateAsync(string id, UpdateAuthUserRequest request,
        ClaimsPrincipal caller, UserManager<ApplicationUser> users)
    {
        if (!CanAccess(caller, id)) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(request.UserName) || request.UserName.Length > 256) return Results.BadRequest();
        var user = await users.FindByIdAsync(id);
        if (user is null) return Results.NotFound();
        var result = await users.SetUserNameAsync(user, request.UserName.Trim());
        return result.Succeeded ? Results.NoContent() : Results.ValidationProblem(ToErrors(result));
    }

    private static async Task<IResult> DeleteAsync(string id, UserManager<ApplicationUser> users,
        ApplicationDbContext db, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = await users.FindByIdAsync(id);
        if (user is null) return Results.NotFound();
        // Break replacement links before cascading deletion of the user's session chain.
        await db.RefreshTokens.Where(token => token.UserId == id && token.ReplacementTokenId != null)
            .ExecuteUpdateAsync(update => update.SetProperty(token => token.ReplacementTokenId, (Guid?)null), cancellationToken);
        var result = await users.DeleteAsync(user);
        if (!result.Succeeded) return Results.Conflict();
        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> TokenHistoryAsync(string id, ClaimsPrincipal caller,
        UserManager<ApplicationUser> users, ApplicationDbContext db, CancellationToken cancellationToken)
    {
        if (!CanAccess(caller, id)) return Results.Forbid();
        if (await users.FindByIdAsync(id) is null) return Results.NotFound();
        var items = await db.RefreshTokens.AsNoTracking().Where(token => token.UserId == id)
            .OrderByDescending(token => token.CreatedAt)
            .Select(token => new { token.Id, token.CreatedAt, token.ExpiresAt, token.RevokedAt, token.ReplacementTokenId })
            .ToListAsync(cancellationToken);
        return Results.Ok(items);
    }

    private static async Task<IResult> GetRolesAsync(string id, UserManager<ApplicationUser> users)
    {
        var user = await users.FindByIdAsync(id);
        return user is null ? Results.NotFound() : Results.Ok(await users.GetRolesAsync(user));
    }

    private static async Task<IResult> AddRoleAsync(string id, AuthRoleRequest request, UserManager<ApplicationUser> users)
    {
        if (!ValidRole(request.Role)) return Results.BadRequest();
        var user = await users.FindByIdAsync(id);
        if (user is null) return Results.NotFound();
        var result = await users.AddToRoleAsync(user, request.Role!);
        return result.Succeeded ? Results.NoContent() : Results.Conflict();
    }

    private static async Task<IResult> RemoveRoleAsync(string id, string role, UserManager<ApplicationUser> users)
    {
        if (!ValidRole(role)) return Results.BadRequest();
        var user = await users.FindByIdAsync(id);
        if (user is null) return Results.NotFound();
        var result = await users.RemoveFromRoleAsync(user, role);
        return result.Succeeded ? Results.NoContent() : Results.Conflict();
    }

    private static bool CanAccess(ClaimsPrincipal caller, string id) =>
        caller.IsInRole("Admin") || string.Equals(caller.FindFirst("sub")?.Value, id, StringComparison.Ordinal);

    private static bool ValidRole(string? role) => role is "User" or "Admin";

    private static Dictionary<string, string[]> ToErrors(IdentityResult result) => result.Errors
        .GroupBy(error => error.Code).ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());
}
