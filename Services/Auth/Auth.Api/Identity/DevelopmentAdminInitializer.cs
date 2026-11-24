using Auth.Api.Data;
using Auth.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace Auth.Api.Identity;

public static class DevelopmentAdminInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration,
        IHostEnvironment environment)
    {
        var section = configuration.GetSection("BootstrapAdmin");
        var userName = section["UserName"];
        var email = section["Email"];
        var password = section["Password"];
        if (string.IsNullOrWhiteSpace(userName) && string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
            return;
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("BootstrapAdmin is supported only in Development.");
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("BootstrapAdmin requires UserName, Email and Password.");

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var existing = await users.FindByNameAsync(userName);
        if (existing is not null)
        {
            if (string.Equals(existing.Email, email, StringComparison.OrdinalIgnoreCase) &&
                await users.IsInRoleAsync(existing, "Admin"))
                return;
            throw new InvalidOperationException("BootstrapAdmin conflicts with an existing account; it will not be promoted automatically.");
        }

        var db = services.GetRequiredService<ApplicationDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = new ApplicationUser { UserName = userName, Email = email, EmailConfirmed = true, LockoutEnabled = true };
        EnsureSuccess(await users.CreateAsync(user, password));
        EnsureSuccess(await users.AddToRoleAsync(user, "Admin"));
        await transaction.CommitAsync();
    }

    private static void EnsureSuccess(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException("BootstrapAdmin provisioning failed: " +
                string.Join(", ", result.Errors.Select(error => error.Code)));
    }
}
