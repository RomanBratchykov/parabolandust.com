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

var mongoPassword = builder.AddParameter("mongo-password", secret: true);
var mongo = builder.AddMongoDB("platform-mongo", password: mongoPassword)
    .WithDataVolume("parabolandust-platform-mongo-data");

var gameStatsDb = mongo.AddDatabase("GameStatsDb", "game_stats_db");
var redis = builder.AddRedis("redis")
    .WithDataVolume("parabolandust-platform-redis-data");

var accounts = builder.AddProject<Projects.Accounts_Api>("accounts-api", launchProfileName: "http")
    .WithReference(accountsDb).WaitFor(accountsDb)
    .WithReference(redis).WaitFor(redis)
    .WithHttpHealthCheck("/health", endpointName: "http");
var newsletters = builder.AddProject<Projects.Newsletters_Api>("newsletters-api", launchProfileName: "http")
    .WithReference(newslettersDb).WaitFor(newslettersDb)
    .WithReference(redis).WaitFor(redis)
    .WithHttpHealthCheck("/health", endpointName: "http");
var gameStats = builder.AddProject<Projects.GameStats_WebApi>("gamestats-api")
    .WithHttpEndpoint(name: "http")
    .WithReference(gameStatsDb).WaitFor(gameStatsDb)
    .WithReference(redis).WaitFor(redis)
    .WithHttpHealthCheck("/health", endpointName: "http");

var aggregator = builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithHttpEndpoint(name: "http")
    .WithReference(accounts).WithReference(newsletters).WithReference(gameStats)
    .WithReference(redis).WaitFor(redis)
    .WaitFor(accounts).WaitFor(newsletters).WaitFor(gameStats)
    .WithHttpHealthCheck("/health", endpointName: "http");

builder.AddProject<Projects.Platform_ApiGateway>("gateway")
    .WithHttpEndpoint(port: 5000, name: "http")
    .WithReference(accounts).WithReference(newsletters).WithReference(gameStats).WithReference(aggregator)
    .WaitFor(accounts).WaitFor(newsletters).WaitFor(gameStats).WaitFor(aggregator)
    .WithHttpHealthCheck("/health", endpointName: "http");

builder.Build().Run();
