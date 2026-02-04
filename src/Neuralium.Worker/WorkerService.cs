using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Neuralium.Data;
using Neuralium.Worker.Agents;
using Neuralium.Worker.Models;
using OpenTelemetry.Trace;

namespace Neuralium.Worker;

/// <summary>
/// One-shot worker that executes the pipeline and exits.
/// Orchestrates agents sequentially: Ingest → Normalize → Dedupe → Classify → Enrich → Analyze → Publish
/// </summary>
public sealed partial class WorkerService(
    IServiceScopeFactory scopeFactory,
    ILogger<WorkerService> logger,
    IOptions<WorkerSettings> workerSettings,
    IHostApplicationLifetime lifetime) : BackgroundService
{
    public const string ActivitySourceName = "Neuralium.Worker";
    private static readonly ActivitySource s_activitySource = new(ActivitySourceName);
    private readonly WorkerSettings _workerSettings = workerSettings.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var activity = s_activitySource.StartActivity("Pipeline execution", ActivityKind.Internal);

        try
        {
            LogWorkerStarting();

            // Create a scope to resolve scoped services (agents use scoped DbContext)
            await using var scope = scopeFactory.CreateAsyncScope();

            // Backfill missing scores if enabled
            if (_workerSettings.BackfillMissingScores)
            {
                await BackfillMissingScoresAsync(scope.ServiceProvider, stoppingToken);
            }

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

    /// <summary>
    /// Backfill missing scores for news items that exist in the database but have no scores
    /// </summary>
    private async Task BackfillMissingScoresAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        using var activity = s_activitySource.StartActivity("Backfill missing scores");
        
        var dbContext = serviceProvider.GetRequiredService<NeuraliumDbContext>();
        var analyzeAgent = serviceProvider.GetRequiredService<AnalyzeAgent>();

        // Find items without scores (last N days)
        var cutoffDate = DateTime.UtcNow.AddDays(-_workerSettings.BackfillMaxDays);
        
        var itemsWithoutScores = await dbContext.NewsItems
            .Where(item => item.PublishedAtUtc >= cutoffDate)
            .Where(item => !dbContext.NewsItemScores.Any(score => score.NewsItemId == item.Id))
            .OrderByDescending(item => item.PublishedAtUtc)
            .ToListAsync(cancellationToken);

        if (itemsWithoutScores.Count == 0)
        {
            LogBackfillNoItems();
            return;
        }

        LogBackfillStarting(itemsWithoutScores.Count);

        // Create a pipeline context with these items
        var context = new PipelineContext();
        context.ClassifiedItems.AddRange(itemsWithoutScores);

        // Process through AnalyzeAgent to generate scores
        context = await analyzeAgent.ProcessAsync(context, cancellationToken);

        // Save the generated scores
        if (context.Metadata.PendingScores.Count > 0)
        {
            for (int i = 0; i < context.AnalyzedItems.Count && i < context.Metadata.PendingScores.Count; i++)
            {
                var item = context.AnalyzedItems[i];
                var score = context.Metadata.PendingScores[i];
                score.NewsItemId = item.Id;
            }

            // Update the items with new trend scores
            foreach (var item in context.AnalyzedItems)
            {
                dbContext.NewsItems.Update(item);
            }

            dbContext.NewsItemScores.AddRange(context.Metadata.PendingScores);
            var savedScores = await dbContext.SaveChangesAsync(cancellationToken);
            
            LogBackfillCompleted(savedScores);
        }
    }

    [LoggerMessage(LogLevel.Information, "Neuralium worker starting execution")]
    private partial void LogWorkerStarting();

    [LoggerMessage(LogLevel.Information, "Neuralium worker completed successfully in {DurationSeconds:F2}s")]
    private partial void LogWorkerCompleted(double durationSeconds);

    [LoggerMessage(LogLevel.Error, "Neuralium worker failed")]
    private partial void LogWorkerFailed(Exception ex);

    [LoggerMessage(LogLevel.Information, "Backfill: Found {Count} items without scores")]
    private partial void LogBackfillStarting(int count);

    [LoggerMessage(LogLevel.Information, "Backfill: No items found without scores")]
    private partial void LogBackfillNoItems();

    [LoggerMessage(LogLevel.Information, "Backfill: Saved {Count} scores to database")]
    private partial void LogBackfillCompleted(int count);
}
