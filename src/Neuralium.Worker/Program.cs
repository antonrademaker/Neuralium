// Neuralium.Worker — Daily execution pipeline
// Purpose: Runs the full data pipeline as a one-shot operation.
// - Designed to be invoked externally (systemd timer, manual trigger)
// - No background loops or scheduling inside the worker
// - Exits after completion (success or failure)
//
// Pipeline stages (implemented as skeleton agents):
// 1. Ingest: Fetch RSS/Atom feeds
// 2. Normalize: Canonical URLs, timestamps
// 3. Deduplicate: Cross-source hash + title similarity
// 4. Classify: Keyword + optional LLM tagging
// 5. Enrich: Optional summaries, embeddings
// 6. Analyze: Trend detection (7d vs 30d momentum)
// 7. Publish: Markdown output

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Neuralium.Data;
using Neuralium.Worker;
using Neuralium.Worker.Agents;
using Neuralium.Worker.Models;
using Neuralium.Worker.Services;
using System.Diagnostics;

var builder = Host.CreateApplicationBuilder(args);

// Configure LLM settings from appsettings
builder.Services.Configure<LlmSettings>(builder.Configuration.GetSection("Llm"));

// Add ActivitySource for LLM telemetry
var llmActivitySource = new ActivitySource("Neuralium.Worker.LlmService");

// Add OpenTelemetry for observability
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(WorkerService.ActivitySourceName)
        .AddSource("Neuralium.Worker.IngestAgent")
        .AddSource("Neuralium.Worker.StandardFeedProvider")
        .AddSource("Neuralium.Worker.ArxivFeedProvider")
        .AddSource("Neuralium.Worker.NormalizeAgent")
        .AddSource("Neuralium.Worker.DedupeAgent")
        .AddSource("Neuralium.Worker.ClassifyAgent")
        .AddSource("Neuralium.Worker.EnrichAgent")
        .AddSource("Neuralium.Worker.AnalyzeAgent")
        .AddSource("Neuralium.Worker.PublishAgent")
        .AddSource("Neuralium.Worker.LlmService"))
    .WithMetrics(metrics => metrics
        .AddMeter("Neuralium.Worker.EnrichAgent"));

// Register PostgreSQL DbContext with Aspire integration
builder.AddNpgsqlDbContext<NeuraliumDbContext>("neuralium");

// Register HttpClient with Polly resilience policies
builder.Services.AddHttpClient("FeedReader")
    .AddStandardResilienceHandler();

// Register LLM service (singleton for ActivitySource, scoped for service)
builder.Services.AddSingleton(llmActivitySource);

// Register the appropriate LLM service based on configuration
var llmProvider = builder.Configuration.GetValue<string>("Llm:Provider") ?? "AzureOpenAI";
if (llmProvider.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<ILlmService, OllamaService>();
}
else
{
    builder.Services.AddScoped<ILlmService, AzureOpenAIService>();
}

// Register feed providers for different source types
builder.Services.AddScoped<IFeedProvider, StandardFeedProvider>();
builder.Services.AddScoped<IFeedProvider, ArxivFeedProvider>();

// Register pipeline agents (scoped lifetime to match DbContext)
builder.Services.AddScoped<IngestAgent>();
builder.Services.AddScoped<NormalizeAgent>();
builder.Services.AddScoped<DedupeAgent>();
builder.Services.AddScoped<ClassifyAgent>();
builder.Services.AddScoped<EnrichAgent>();
builder.Services.AddScoped<AnalyzeAgent>();
builder.Services.AddScoped<PublishAgent>();

// Register main worker service
builder.Services.AddHostedService<WorkerService>();

using var host = builder.Build();
await host.RunAsync();
