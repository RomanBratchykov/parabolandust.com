using Microsoft.AspNetCore.Identity;

namespace Auth.Api.Identity;

public static class IdentityRoleInitializer
{
    public static async Task InitializeAsync(RoleManager<IdentityRole> roles)
    {
        foreach (var roleName in new[] { "User", "Admin" })
        {
            if (await roles.RoleExistsAsync(roleName))
                continue;

            var result = await roles.CreateAsync(new IdentityRole(roleName));
            // Another replica may have inserted the role after our existence check.
            if (!result.Succeeded && !await roles.RoleExistsAsync(roleName))
            {
                var codes = string.Join(", ", result.Errors.Select(error => error.Code));
                throw new InvalidOperationException($"Unable to initialize Identity role '{roleName}': {codes}");
            }
        }
    }
}
