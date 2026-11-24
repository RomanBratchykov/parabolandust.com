using Pulumi;
using System.Collections.Immutable;
using Pulumi.Keycloak;
using Pulumi.Keycloak.OpenId;
using Pulumi.Keycloak.OpenId.Inputs;

public sealed class RealmStack : Stack
{
    [Output] public Output<string> RealmUrl { get; private set; }
    [Output] public Output<string> AdminConsoleUrl { get; private set; }
    [Output] public Output<string[]> ClientIds { get; private set; }
    [Output] public Output<ImmutableDictionary<string, string>> ClientSecrets { get; private set; }

    public RealmStack()
    {
        var config = new Pulumi.Config();
        var baseUrl = config.Require("keycloakUrl").TrimEnd('/');
        var aggregatorSecret = config.RequireSecret("aggregatorClientSecret");
        var options = new CustomResourceOptions();
        var realm = new Realm("drivers-realm", new RealmArgs
        {
            RealmName = "DevRealmDrivers",
            Enabled = true,
            RegistrationAllowed = true,
            ResetPasswordAllowed = true,
            RememberMe = true,
            LoginTheme = "keycloak",
            AccountTheme = "keycloak"
        }, options);

        var roles = new Dictionary<string, Role>();
        foreach (var roleName in new[] { "admin", "manager", "user", "guest" })
            roles[roleName] = new Role($"realm-{roleName}", new RoleArgs { RealmId = realm.Id, Name = roleName });

        var resourceServers = new Dictionary<string, Dictionary<string, string[]>>
        {
            ["orders-api"] = new()
            {
                ["orders:read"] = ["admin", "manager", "user"],
                ["orders:create"] = ["admin", "user"],
                ["orders:update"] = ["admin", "manager"],
                ["orders:delete"] = ["admin"],
                ["orders:approve"] = ["admin", "manager"]
            },
            ["products-api"] = new()
            {
                ["products:read"] = ["admin", "manager", "user", "guest"],
                ["products:create"] = ["admin"],
                ["products:update"] = ["admin", "manager"],
                ["products:delete"] = ["admin"],
                ["products:publish"] = ["admin", "manager"]
            },
            ["payments-api"] = new()
            {
                ["payments:read"] = ["admin"],
                ["payments:process"] = ["admin"],
                ["payments:refund"] = ["admin"]
            }
        };
        var postman = CreateClient(realm, "postman", "PUBLIC", true, false,
            ["https://oauth.pstmn.io/v1/callback"], null);
        var swagger = CreateClient(realm, "swagger", "PUBLIC", true, false,
            [config.Require("swaggerRedirectUri")], null);
        var aggregator = CreateClient(realm, "aggregator-service", "CONFIDENTIAL", false, true,
            [], aggregatorSecret);

        foreach (var (name, permissions) in resourceServers)
            CreateResourceServer(realm, name, permissions, roles, aggregator);

        foreach (var (clientName, client) in new[] { ("postman", postman), ("swagger", swagger), ("aggregator-service", aggregator) })
        {
            AddDefaultScopes(realm, clientName, client);
            foreach (var audience in resourceServers.Keys)
            {
                if (clientName == "aggregator-service" && audience == "payments-api")
                    continue;
                AddAudience(realm, clientName, client, audience);
            }
        }

        foreach (var roleName in roles.Keys)
        {
            var user = new User($"demo-{roleName}", new UserArgs
            {
                RealmId = realm.Id,
                Username = $"lab9.{roleName}",
                Email = $"lab9.{roleName}@example.invalid",
                EmailVerified = true,
                Enabled = true,
                InitialPassword = new Pulumi.Keycloak.Inputs.UserInitialPasswordArgs
                {
                    Value = config.RequireSecret($"{roleName}Password"),
                    Temporary = true
                }
            });
            new UserRoles($"demo-{roleName}-roles", new UserRolesArgs
            {
                RealmId = realm.Id, UserId = user.Id, RoleIds = { roles[roleName].Id }, Exhaustive = false
            });
        }

        RealmUrl = Output.Create($"{baseUrl}/realms/DevRealmDrivers");
        AdminConsoleUrl = Output.Create($"{baseUrl}/admin/master/console/");
        ClientIds = Output.Create(new[] { "postman", "swagger", "aggregator-service" });
        ClientSecrets = aggregatorSecret.Apply(secret =>
            ImmutableDictionary<string, string>.Empty.Add("aggregator-service", secret));
    }

    private static void AddDefaultScopes(Realm realm, string clientName, Client client)
        => _ = new ClientDefaultScopes($"{clientName}-default-scopes", new ClientDefaultScopesArgs
        {
            RealmId = realm.Id, ClientId = client.Id,
            DefaultScopes = { "profile", "email", "roles", "web-origins" }
        });

    private static void AddAudience(Realm realm, string clientName, Client client, string audience)
        => _ = new AudienceProtocolMapper($"{clientName}-{audience}-audience", new AudienceProtocolMapperArgs
        {
            RealmId = realm.Id, ClientId = client.Id, Name = $"{audience}-audience",
            IncludedCustomAudience = audience, AddToAccessToken = true
        });

    private static Client CreateClient(Realm realm, string name, string accessType, bool standardFlow,
        bool serviceAccount, string[] redirectUris, Input<string>? secret)
    {
        return new Client(name, new ClientArgs
        {
            RealmId = realm.Id, ClientId = name, Name = name, Enabled = true,
            AccessType = accessType, StandardFlowEnabled = standardFlow,
            ServiceAccountsEnabled = serviceAccount, DirectAccessGrantsEnabled = false,
            ValidRedirectUris = redirectUris, ClientSecret = secret,
            PkceCodeChallengeMethod = standardFlow ? "S256" : (Input<string>?)null
        });
    }

    private static void CreateResourceServer(Realm realm, string name,
        Dictionary<string, string[]> permissions, Dictionary<string, Role> roles, Client aggregator)
    {
        var server = new Client(name, new ClientArgs
        {
            RealmId = realm.Id, ClientId = name, Name = name, Enabled = true,
            AccessType = "CONFIDENTIAL", ServiceAccountsEnabled = true,
            StandardFlowEnabled = false, DirectAccessGrantsEnabled = false,
            Authorization = new ClientAuthorizationArgs { PolicyEnforcementMode = "ENFORCING", KeepDefaults = false }
        });
        var scopeObjects = permissions.Keys.ToDictionary(permission => permission, permission =>
            new ClientAuthorizationScope($"{name}-{permission.Replace(':', '-')}", new ClientAuthorizationScopeArgs
            {
                RealmId = realm.Id, ResourceServerId = server.ResourceServerId, Name = permission
            }));
        var resource = new ClientAuthorizationResource($"{name}-resource", new ClientAuthorizationResourceArgs
        {
            RealmId = realm.Id, ResourceServerId = server.ResourceServerId, Name = name,
            Scopes = scopeObjects.Values.Select(scope => scope.Name).ToArray()
        });
        Role? serviceReadRole = null;
        if (name is "orders-api" or "products-api")
        {
            serviceReadRole = new Role($"{name}-service-read", new RoleArgs
            {
                RealmId = realm.Id, ClientId = server.Id, Name = "service-read"
            });
            _ = new ClientServiceAccountRole($"{name}-aggregator-read", new ClientServiceAccountRoleArgs
            {
                RealmId = realm.Id, ClientId = server.Id, Role = serviceReadRole.Name,
                ServiceAccountUserId = aggregator.ServiceAccountUserId
            });
        }
        foreach (var (permission, allowedRoles) in permissions)
        {
            var suffix = permission.Replace(':', '-');
            var policyRoles = allowedRoles.Select(role => new ClientRolePolicyRoleArgs
            {
                Id = roles[role].Id, Required = false
            }).ToList();
            if (serviceReadRole is not null && permission.EndsWith(":read", StringComparison.Ordinal))
                policyRoles.Add(new ClientRolePolicyRoleArgs { Id = serviceReadRole.Id, Required = false });
            var policy = new ClientRolePolicy($"{name}-{suffix}-roles", new ClientRolePolicyArgs
            {
                RealmId = realm.Id, ResourceServerId = server.ResourceServerId,
                Name = $"{suffix}-roles", Type = "role", DecisionStrategy = "AFFIRMATIVE",
                Roles = policyRoles.ToArray()
            });
            new ClientAuthorizationPermission($"{name}-{suffix}-permission", new ClientAuthorizationPermissionArgs
            {
                RealmId = realm.Id, ResourceServerId = server.ResourceServerId,
                Name = $"{suffix}-permission", Type = "scope", DecisionStrategy = "UNANIMOUS",
                Policies = { policy.Id }, Resources = { resource.Id }, Scopes = { scopeObjects[permission].Id }
            });
        }
    }
}
