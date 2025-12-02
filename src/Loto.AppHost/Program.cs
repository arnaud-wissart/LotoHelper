var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres").WithDataVolume();
var lotoDatabase = postgres.AddDatabase("loto-db");

var lotoApi = builder.AddProject<Projects.Loto_Api>("loto-api")
    .WithReference(lotoDatabase);

builder.AddProject<Projects.Loto_Ingestion_Worker>("loto-ingestion-worker")
    .WithReference(lotoDatabase);

builder.AddNpmApp("loto-frontend", "../../frontend/loto-frontend")
    .WithReference(lotoApi)
    .WithHttpEndpoint(env: "PORT")
    .WithEnvironment("NODE_ENV", "development");

builder.Build().Run();
