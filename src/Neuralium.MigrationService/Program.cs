using Microsoft.EntityFrameworkCore;
using Neuralium.Data;
using Neuralium.MigrationService;

var builder = Host.CreateApplicationBuilder(args);

// Add OpenTelemetry for observability
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(Worker.ActivitySourceName));

// Register the migration worker
builder.Services.AddHostedService<Worker>();

// Register PostgreSQL DbContext with Aspire integration
// Configure migrations to be stored in MigrationService project
builder.AddNpgsqlDbContext<NeuraliumDbContext>("neuralium",
    configureDbContextOptions: options =>
    {
        options.UseNpgsql(npgsqlOptions =>
            npgsqlOptions.MigrationsAssembly("Neuralium.MigrationService"));
    });

var host = builder.Build();
host.Run();
