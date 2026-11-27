using Microsoft.AspNetCore.DataProtection;

namespace Auth.Api.Identity;

public static class AuthDataProtectionRegistration
{
    public static IServiceCollection AddAuthDataProtection(
        this IServiceCollection services, IConfiguration configuration)
    {
        var keyPath = configuration["DataProtection:KeyPath"];
        if (string.IsNullOrWhiteSpace(keyPath) || !Path.IsPathFullyQualified(keyPath))
        {
            throw new InvalidOperationException(
                "DataProtection:KeyPath must be an absolute path to a persistent Auth key directory.");
        }

        var protection = services.AddDataProtection()
            .SetApplicationName("parabolandust-auth")
            .PersistKeysToFileSystem(new DirectoryInfo(keyPath));

        // Explicit file persistence disables automatic encryption; restore it on Windows.
        if (OperatingSystem.IsWindows())
            protection.ProtectKeysWithDpapi();

        return services;
    }
}
