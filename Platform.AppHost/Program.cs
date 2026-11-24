using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

// Only one issuer is trusted by the gateway and business APIs in each run.
var authProvider = builder.Configuration["Authentication:Provider"] ?? "IdentityJwt";
if (authProvider is not ("IdentityJwt" or "IdentityServer" or "Keycloak"))
    throw new InvalidOperationException("Authentication:Provider must be IdentityJwt, IdentityServer or Keycloak.");

var postgresPassword = builder.AddParameter("postgres-password", secret: true);
var accountsScripts = Path.GetFullPath("../db/p1", builder.AppHostDirectory);
var bootstrapScript = Path.Combine(builder.AppHostDirectory, "DatabaseBootstrap.sh");

var postgres = builder.AddPostgres("platform-postgres")
    .WithPassword(postgresPassword)
    .WithDataVolume("parabolandust-platform-postgres-data")
    .WithBindMount(accountsScripts, "/platform/accounts", isReadOnly: true)
    .WithBindMount(bootstrapScript, "/docker-entrypoint-initdb.d/01-platform.sh", isReadOnly: true);

var accountsDb = postgres.AddDatabase("AccountsDb", "accounts_db_p1");
var newslettersDb = postgres.AddDatabase("NewslettersDb", "accounts_db_p2");
var identityDb = postgres.AddDatabase("IdentityDb", "identity_db");
var identityServerDb = postgres.AddDatabase("IdentityServerDb", "identityserver_db");
var authJwtSecret = builder.AddParameter("auth-jwt-secret", secret: true);
var authAuthority = authProvider == "IdentityJwt" ? null : builder.AddParameter("authentication-authority");
var authAudience = authProvider == "IdentityJwt" ? null : builder.AddParameter("authentication-audience");
var requireHttpsMetadata = builder.Configuration["Authentication:RequireHttpsMetadata"] ?? "true";
if (!bool.TryParse(requireHttpsMetadata, out _))
    throw new InvalidOperationException("Authentication:RequireHttpsMetadata must be true or false.");
var authKeyPath = builder.AddParameter("auth-data-protection-key-path");
var authMailPath = builder.AddParameter("auth-email-pickup-path");
var keycloakAdminUser = builder.AddParameter("keycloak-admin-user");
var keycloakAdminPassword = builder.AddParameter("keycloak-admin-password", secret: true);
var keycloakRealmAuthority = builder.AddParameter("keycloak-realm-authority");
var identityServerKeyPath = builder.AddParameter("identityserver-data-protection-key-path");
var postmanSecret = builder.AddParameter("identityserver-postman-secret", secret: true);
var aggregatorSecret = builder.AddParameter("identityserver-aggregator-secret", secret: true);
var swaggerRedirect = builder.AddParameter("identityserver-swagger-redirect-uri");

IResourceBuilder<ProjectResource> ConfigureBusinessAuthentication(IResourceBuilder<ProjectResource> project)
{
    project.WithEnvironment("Authentication__Provider", authProvider);
    if (authProvider == "IdentityJwt")
    {
        project.WithEnvironment("Jwt__Issuer", "parabolandust-auth")
            .WithEnvironment("Jwt__Audience", "parabolandust-api")
            .WithEnvironment("Jwt__SecretKey", authJwtSecret);
    }
    else
    {
        project.WithEnvironment("Authentication__Authority", authAuthority!)
            .WithEnvironment("Authentication__Audience", authAudience!)
            .WithEnvironment("Authentication__RequireHttpsMetadata", requireHttpsMetadata);
    }
    return project;
}

var mongoPassword = builder.AddParameter("mongo-password", secret: true);
#pragma warning disable ASPIREMONGODB001 // Single-member replica set enables local projection transactions.
var mongo = builder.AddMongoDB("platform-mongo", password: mongoPassword)
    .WithReplicaSet()
    .WithDataVolume("parabolandust-platform-mongo-data");
#pragma warning restore ASPIREMONGODB001

var gameStatsDb = mongo.AddDatabase("GameStatsDb", "game_stats_db");
var redis = builder.AddRedis("redis")
    .WithDataVolume("parabolandust-platform-redis-data");
var messaging = builder.AddRabbitMQ("messaging")
    .WithDataVolume("parabolandust-platform-rabbitmq-data")
    .WithManagementPlugin();

var keycloak = builder.AddKeycloak("keycloak", 8080, keycloakAdminUser, keycloakAdminPassword)
    .WithDataVolume();
var payments = builder.AddProject<Projects.Payments_Api>("payments-api")
    .WithHttpsEndpoint(name: "https")
    .WithReference(keycloak).WaitFor(keycloak)
    .WithEnvironment("Authentication__Keycloak__Authority", keycloakRealmAuthority)
    .WithHttpHealthCheck("/health/ready", endpointName: "https");

var auth = builder.AddProject<Projects.Auth_Api>("auth-api")
    .WithHttpsEndpoint(name: "https")
    .WithReference(identityDb).WaitFor(identityDb)
    .WithEnvironment("Jwt__Issuer", "parabolandust-auth")
    .WithEnvironment("Jwt__Audience", "parabolandust-api")
    .WithEnvironment("Jwt__SecretKey", authJwtSecret)
    .WithEnvironment("DataProtection__KeyPath", authKeyPath)
    .WithEnvironment("Email__PickupDirectory", authMailPath)
    .WithHttpHealthCheck("/health/ready", endpointName: "https");

builder.AddProject<Projects.Platform_IdentityServer>("identity-server")
    .WithHttpsEndpoint(port: 5002, name: "https")
    .WithReference(identityServerDb).WaitFor(identityServerDb)
    .WithEnvironment("DataProtection__KeyPath", identityServerKeyPath)
    .WithEnvironment("Clients__Postman__Secret", postmanSecret)
    .WithEnvironment("Clients__Aggregator__Secret", aggregatorSecret)
    .WithEnvironment("Clients__Swagger__RedirectUri", swaggerRedirect)
    .WithHttpHealthCheck("/health/ready", endpointName: "https");

var accounts = ConfigureBusinessAuthentication(builder.AddProject<Projects.Accounts_Api>("accounts-api", launchProfileName: "http")
    .WithHttpsEndpoint(name: "https"))
    .WithReference(accountsDb).WaitFor(accountsDb)
    .WithReference(messaging).WaitFor(messaging)
    .WithReference(redis)
    .WithHttpHealthCheck("/health/ready", endpointName: "http");
var newsletters = ConfigureBusinessAuthentication(builder.AddProject<Projects.Newsletters_Api>("newsletters-api", launchProfileName: "http")
    .WithHttpsEndpoint(name: "https"))
    .WithReference(newslettersDb).WaitFor(newslettersDb)
    .WithReference(messaging).WaitFor(messaging)
    .WithReference(redis)
    .WithHttpHealthCheck("/health/ready", endpointName: "http");
var gameStats = ConfigureBusinessAuthentication(builder.AddProject<Projects.GameStats_WebApi>("gamestats-api")
    .WithHttpEndpoint(name: "http")
    .WithHttpsEndpoint(name: "https"))
    .WithReference(gameStatsDb).WaitFor(gameStatsDb)
    .WithReference(messaging).WaitFor(messaging)
    .WithReference(redis)
    .WithHttpHealthCheck("/health/ready", endpointName: "http");

var aggregator = ConfigureBusinessAuthentication(builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithHttpEndpoint(name: "http"))
    .WithReference(accounts).WithReference(newsletters).WithReference(gameStats)
    .WithReference(redis)
    .WaitFor(accounts).WaitFor(newsletters).WaitFor(gameStats)
    .WithHttpHealthCheck("/health/ready", endpointName: "http");

ConfigureBusinessAuthentication(builder.AddProject<Projects.Platform_ApiGateway>("gateway")
    .WithHttpEndpoint(port: 5000, name: "http")
    .WithHttpsEndpoint(port: 5001, name: "https"))
    .WithReference(auth).WithReference(accounts).WithReference(newsletters).WithReference(gameStats).WithReference(aggregator).WithReference(payments)
    .WaitFor(auth).WaitFor(accounts).WaitFor(newsletters).WaitFor(gameStats).WaitFor(aggregator).WaitFor(payments)
    .WithHttpHealthCheck("/health/ready", endpointName: "http");

builder.Build().Run();
