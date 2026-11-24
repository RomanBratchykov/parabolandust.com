var builder = DistributedApplication.CreateBuilder(args);

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
var authJwtSecret = builder.AddParameter("auth-jwt-secret", secret: true);
var authKeyPath = builder.AddParameter("auth-data-protection-key-path");
var authMailPath = builder.AddParameter("auth-email-pickup-path");

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

var auth = builder.AddProject<Projects.Auth_Api>("auth-api")
    .WithHttpsEndpoint(name: "https")
    .WithReference(identityDb).WaitFor(identityDb)
    .WithEnvironment("Jwt__Issuer", "parabolandust-auth")
    .WithEnvironment("Jwt__Audience", "parabolandust-api")
    .WithEnvironment("Jwt__SecretKey", authJwtSecret)
    .WithEnvironment("DataProtection__KeyPath", authKeyPath)
    .WithEnvironment("Email__PickupDirectory", authMailPath)
    .WithHttpHealthCheck("/health/ready", endpointName: "https");

var accounts = builder.AddProject<Projects.Accounts_Api>("accounts-api", launchProfileName: "http")
    .WithHttpsEndpoint(name: "https")
    .WithReference(accountsDb).WaitFor(accountsDb)
    .WithReference(messaging).WaitFor(messaging)
    .WithReference(redis)
    .WithHttpHealthCheck("/health/ready", endpointName: "http");
var newsletters = builder.AddProject<Projects.Newsletters_Api>("newsletters-api", launchProfileName: "http")
    .WithHttpsEndpoint(name: "https")
    .WithReference(newslettersDb).WaitFor(newslettersDb)
    .WithReference(messaging).WaitFor(messaging)
    .WithReference(redis)
    .WithHttpHealthCheck("/health/ready", endpointName: "http");
var gameStats = builder.AddProject<Projects.GameStats_WebApi>("gamestats-api")
    .WithHttpEndpoint(name: "http")
    .WithHttpsEndpoint(name: "https")
    .WithReference(gameStatsDb).WaitFor(gameStatsDb)
    .WithReference(messaging).WaitFor(messaging)
    .WithReference(redis)
    .WithHttpHealthCheck("/health/ready", endpointName: "http");

var aggregator = builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithHttpEndpoint(name: "http")
    .WithReference(accounts).WithReference(newsletters).WithReference(gameStats)
    .WithReference(redis)
    .WaitFor(accounts).WaitFor(newsletters).WaitFor(gameStats)
    .WithHttpHealthCheck("/health/ready", endpointName: "http");

builder.AddProject<Projects.Platform_ApiGateway>("gateway")
    .WithHttpEndpoint(port: 5000, name: "http")
    .WithReference(auth).WithReference(accounts).WithReference(newsletters).WithReference(gameStats).WithReference(aggregator)
    .WaitFor(auth).WaitFor(accounts).WaitFor(newsletters).WaitFor(gameStats).WaitFor(aggregator)
    .WithHttpHealthCheck("/health/ready", endpointName: "http");

builder.Build().Run();
