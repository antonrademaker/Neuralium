using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Neuralium.Worker.Agents;
using OpenTelemetry.Trace;

namespace Neuralium.Worker;

/// <summary>
/// One-shot worker that executes the pipeline and exits.
/// Orchestrates agents sequentially: Ingest → Normalize → Dedupe → Classify → Enrich → Analyze → Publish
/// </summary>
public sealed partial class WorkerService(
    IServiceScopeFactory scopeFactory,
    ILogger<WorkerService> logger,
    IHostApplicationLifetime lifetime) : BackgroundService
{
    public const string ActivitySourceName = "Neuralium.Worker";
    private static readonly ActivitySource s_activitySource = new(ActivitySourceName);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var activity = s_activitySource.StartActivity("Pipeline execution", ActivityKind.Internal);

        try
        {
            LogWorkerStarting();

            // Create a scope to resolve scoped services (agents use scoped DbContext)
            await using var scope = scopeFactory.CreateAsyncScope();

            var ingestAgent = scope.ServiceProvider.GetRequiredService<IngestAgent>();
            var normalizeAgent = scope.ServiceProvider.GetRequiredService<NormalizeAgent>();
            var dedupeAgent = scope.ServiceProvider.GetRequiredService<DedupeAgent>();
            var classifyAgent = scope.ServiceProvider.GetRequiredService<ClassifyAgent>();
            var enrichAgent = scope.ServiceProvider.GetRequiredService<EnrichAgent>();
            var analyzeAgent = scope.ServiceProvider.GetRequiredService<AnalyzeAgent>();
            var publishAgent = scope.ServiceProvider.GetRequiredService<PublishAgent>();

            // Execute pipeline stages sequentially
            var context = await ingestAgent.ProduceAsync(stoppingToken);
            context = await normalizeAgent.ProcessAsync(context, stoppingToken);
            context = await dedupeAgent.ProcessAsync(context, stoppingToken);
            context = await classifyAgent.ProcessAsync(context, stoppingToken);
            context = await enrichAgent.ProcessAsync(context, stoppingToken);
            context = await analyzeAgent.ProcessAsync(context, stoppingToken);
            await publishAgent.ConsumeAsync(context, stoppingToken);

            context.Metadata.CompletedAt = DateTime.UtcNow;
            var duration = context.Metadata.CompletedAt.Value - context.Metadata.StartedAt;

            LogWorkerCompleted(duration.TotalSeconds);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            LogWorkerFailed(ex);
            Environment.ExitCode = 1;
        }
        finally
        {
            lifetime.StopApplication();
        }
    }

    [LoggerMessage(LogLevel.Information, "Neuralium worker starting execution")]
    private partial void LogWorkerStarting();

    [LoggerMessage(LogLevel.Information, "Neuralium worker completed successfully in {DurationSeconds:F2}s")]
    private partial void LogWorkerCompleted(double durationSeconds);

    [LoggerMessage(LogLevel.Error, "Neuralium worker failed")]
    private partial void LogWorkerFailed(Exception ex);
}
