var builder = DistributedApplication.CreateBuilder(args);

builder.AddParameter("postgres-password", secret: true);

builder.Build().Run();
