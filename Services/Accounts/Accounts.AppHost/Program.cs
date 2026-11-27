var builder = DistributedApplication.CreateBuilder(args);

var postgresPassword = builder.AddParameter("postgres-password", secret: true);
var postgres = builder.AddPostgres("postgres")
    .WithPassword(postgresPassword)
    .WithBindMount(Path.GetFullPath("db/p1/schema.sql"), "/docker-entrypoint-initdb.d/01-schema.sql", isReadOnly: true)
    .WithBindMount(Path.GetFullPath("db/p1/seed.sql"), "/docker-entrypoint-initdb.d/02-seed.sql", isReadOnly: true)
    .WithBindMount(Path.GetFullPath("db/p1/procedures.sql"), "/docker-entrypoint-initdb.d/03-procedures.sql", isReadOnly: true)
    .WithDataVolume();
var accountsDb = postgres.AddDatabase("AccountsDb", "accounts_db_p1");

builder.AddProject<Projects.Accounts_Api>("accounts-api")
    .WithReference(accountsDb)
    .WaitFor(accountsDb);

builder.Build().Run();
