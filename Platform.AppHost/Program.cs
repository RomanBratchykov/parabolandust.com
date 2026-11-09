var builder = DistributedApplication.CreateBuilder(args);

var postgresPassword = builder.AddParameter("postgres-password", secret: true);
var accountsScripts = Path.GetFullPath("../db/p1", builder.AppHostDirectory);
var bootstrapScript = Path.Combine(builder.AppHostDirectory, "DatabaseBootstrap.sh");

var postgres = builder.AddPostgres("platform-postgres")
    .WithPassword(postgresPassword)
    .WithDataVolume("parabolandust-platform-postgres-data")
    .WithBindMount(accountsScripts, "/platform/accounts", isReadOnly: true)
    .WithBindMount(bootstrapScript, "/docker-entrypoint-initdb.d/01-platform.sh", isReadOnly: true);

postgres.AddDatabase("AccountsDb", "accounts_db_p1");
postgres.AddDatabase("NewslettersDb", "accounts_db_p2");

builder.Build().Run();
