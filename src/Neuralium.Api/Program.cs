// Neuralium.Api — Read-only HTTP interface
// Purpose: Provides query access to aggregated news and trends.
// - Designed for future consumption by dashboards or external tools
// - No write operations (all data comes from the Worker pipeline)
// - Minimal API style (no controllers, no ceremony)
//
// Future endpoints:
// - GET /news?topic=agents&days=7 — Recent news items
// - GET /trends?window=7d — Trending topics and entities
// - GET /digest/latest — Most recent daily digest

var builder = WebApplication.CreateBuilder(args);

// Future: Add services here
// builder.Services.AddDbContext<NeuraliumDbContext>();
// builder.Services.AddSingleton<NewsQueryService>();
// builder.Services.AddSingleton<TrendQueryService>();

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

// Placeholder: Confirm API is running
app.MapGet("/", () => new
{
    Service = "Neuralium.Api",
    Status = "Running",
    Message = "Query endpoints will be added as the pipeline is implemented."
});

app.Run();
