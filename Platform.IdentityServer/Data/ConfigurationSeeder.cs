using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Models;
using IdentityServerHost.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityServerHost.Data;

public static class ConfigurationSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, IHostEnvironment environment)
    {
        var postmanSecret = Required(configuration, "Clients:Postman:Secret");
        var aggregatorSecret = Required(configuration, "Clients:Aggregator:Secret");
        var swaggerRedirect = Required(configuration, "Clients:Swagger:RedirectUri");
        if (!Uri.TryCreate(swaggerRedirect, UriKind.Absolute, out var redirect) ||
            (redirect.Scheme != Uri.UriSchemeHttps && !(environment.IsDevelopment() && redirect.IsLoopback)))
            throw new InvalidOperationException("Clients:Swagger:RedirectUri must be an absolute HTTPS URL (HTTP loopback is permitted in Development).");

        var db = services.GetRequiredService<ConfigurationDbContext>();
        IdentityResource[] identities = [new IdentityResources.OpenId(), new IdentityResources.Profile()];
        foreach (var resource in identities)
            if (!await db.IdentityResources.AnyAsync(x => x.Name == resource.Name)) db.IdentityResources.Add(resource.ToEntity());

        string[] scopeNames = ["orders_api", "products_api", "orders.read", "orders.write", "products.read", "products.write"];
        foreach (var name in scopeNames)
            if (!await db.ApiScopes.AnyAsync(x => x.Name == name)) db.ApiScopes.Add(new ApiScope(name).ToEntity());
        ApiResource[] apis =
        [
            new("orders_api") { Scopes = { "orders_api", "orders.read", "orders.write" } },
            new("products_api") { Scopes = { "products_api", "products.read", "products.write" } }
        ];
        foreach (var api in apis)
            if (!await db.ApiResources.AnyAsync(x => x.Name == api.Name)) db.ApiResources.Add(api.ToEntity());

        Client[] clients =
        [
            new() { ClientId = "postman", AllowedGrantTypes = GrantTypes.Code,
                ClientSecrets = { new Secret(postmanSecret.Sha256()) },
                RedirectUris = { "https://oauth.pstmn.io/v1/callback" },
                AllowedScopes = { "openid", "profile", "orders_api", "products_api", "orders.read", "orders.write", "products.read", "products.write" },
                AllowOfflineAccess = true, RequireConsent = true },
            new() { ClientId = "aggregator_service", AllowedGrantTypes = GrantTypes.ClientCredentials,
                ClientSecrets = { new Secret(aggregatorSecret.Sha256()) }, AllowedScopes = { "orders.read", "products.read" } },
            new() { ClientId = "swagger", AllowedGrantTypes = GrantTypes.Code, RequirePkce = true,
                RequireClientSecret = false, RedirectUris = { swaggerRedirect },
                AllowedScopes = { "openid", "profile", "orders.read", "orders.write", "products.read", "products.write" } }
        ];
        foreach (var client in clients)
            if (!await db.Clients.AnyAsync(x => x.ClientId == client.ClientId)) db.Clients.Add(client.ToEntity());
        await db.SaveChangesAsync();
        if (environment.IsDevelopment()) await SeedUserAsync(services, configuration);
    }

    private static async Task SeedUserAsync(IServiceProvider services, IConfiguration configuration)
    {
        var username = configuration["DemoUser:UserName"];
        var password = configuration["DemoUser:Password"];
        var email = configuration["DemoUser:Email"];
        if (string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(password) && string.IsNullOrWhiteSpace(email)) return;
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("All DemoUser settings are required when provisioning a demonstration user.");
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByNameAsync(username) is not null) return;
        var result = await users.CreateAsync(new ApplicationUser { UserName = username, Email = email, EmailConfirmed = true }, password);
        if (!result.Succeeded) throw new InvalidOperationException("Unable to provision demo Identity user: " + string.Join(", ", result.Errors.Select(x => x.Code)));
    }

    private static string Required(IConfiguration configuration, string key) =>
        !string.IsNullOrWhiteSpace(configuration[key]) ? configuration[key]! : throw new InvalidOperationException($"Configuration '{key}' is required.");
}
