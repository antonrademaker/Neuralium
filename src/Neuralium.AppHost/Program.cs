// Neuralium.AppHost — Aspire orchestrator
// Purpose: Configures and launches all services in the Neuralium ecosystem.
// - Runs PostgreSQL database with pgAdmin management UI
// - Runs the MigrationService to apply EF Core migrations and seed data
// - Runs the Worker as an executable project
// - Hosts the Api with HTTP endpoint
// - Provides service discovery and observability

var builder = DistributedApplication.CreateBuilder(args);

// Stable PostgreSQL password (use parameter for production secrets management)
var postgresPassword = builder.AddParameter("postgres-password", secret: true);

// PostgreSQL: Primary data store for news items, classifications, and trends
var postgres = builder.AddPostgres("postgres", password: postgresPassword)
                      .WithDataVolume()                    // Persistent storage across restarts
                      .WithLifetime(ContainerLifetime.Persistent)  // Keep running when AppHost stops
                      .WithPgAdmin(c =>
                      {
                          c.WithLifetime(ContainerLifetime.Persistent)
                           .WithHostPort(5050);  // Fixed port for pgAdmin
                      });

var db = postgres.AddDatabase("neuralium");

// MigrationService: Applies EF Core migrations and seeds database before other services start
var migrations = builder.AddProject<Projects.Neuralium_MigrationService>("migrations")
                        .WithReference(db)
                        .WaitFor(postgres);

// API: Read-only endpoints (future: news query, trend data)
var api = builder.AddProject<Projects.Neuralium_Api>("api")
                 .WithReference(db)
                 .WaitForCompletion(migrations);

// Angular Frontend: News browsing and exploration UI
var frontend = builder.AddJavaScriptApp("frontend", "../Neuralium.Frontend", "dev")
                      .WithNpm()
                      .WithHttpEndpoint(port: 4200, env: "PORT")
                      .WithEnvironment("NODE_ENV", "development")
                      .WithReference(api)
                      .WaitFor(api);

// Worker: One-shot execution pipeline (ingest → normalize → dedupe → classify → enrich → analyze → publish)
// Scheduled externally via systemd timer or manual trigger
var worker = builder.AddProject<Projects.Neuralium_Worker>("worker")
                    .WithReference(db)
                    .WaitForCompletion(migrations);

builder.Build().Run();
