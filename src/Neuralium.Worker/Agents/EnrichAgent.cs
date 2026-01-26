using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Neuralium.Data;
using Neuralium.Worker.Models;
using Neuralium.Worker.Services;

namespace Neuralium.Worker.Agents;

/// <summary>
/// Enriches news items with summaries, entities, and embeddings.
/// Uses LLM-based summarization and embedding generation when enabled.
/// Also backfills missing data for items from previous failed enrichment attempts.
/// </summary>
public partial class EnrichAgent : IAgent<PipelineContext, PipelineContext>
{
    private static readonly ActivitySource s_activitySource = new("Neuralium.Worker.EnrichAgent");
    private static readonly Meter s_meter = new("Neuralium.Worker.EnrichAgent", "1.0.0");

    private static readonly Counter<int> s_summariesGenerated = s_meter.CreateCounter<int>(
        "neuralium.enrich.summaries_generated",
        description: "Total number of summaries generated");

    private static readonly Counter<int> s_embeddingsGenerated = s_meter.CreateCounter<int>(
        "neuralium.enrich.embeddings_generated",
        description: "Total number of embeddings generated");

    private static readonly Counter<int> s_enrichmentErrors = s_meter.CreateCounter<int>(
        "neuralium.enrich.errors",
        description: "Total number of enrichment errors");

    private readonly ILogger<EnrichAgent> _logger;
    private readonly ILlmService _llmService;
    private readonly LlmSettings _llmSettings;
    private readonly NeuraliumDbContext _dbContext;

    // Metrics for tracking enrichment status (initialized in constructor with callbacks)
    private ObservableGauge<int>? _itemsMissingSummaries;
    private ObservableGauge<int>? _itemsMissingEmbeddings;

    public EnrichAgent(
        ILogger<EnrichAgent> logger,
        ILlmService llmService,
        IOptions<LlmSettings> llmSettings,
        NeuraliumDbContext dbContext)
    {
        _logger = logger;
        _llmService = llmService;
        _llmSettings = llmSettings.Value;
        _dbContext = dbContext;

        // Register observable gauges with callbacks
        _itemsMissingSummaries = s_meter.CreateObservableGauge(
            "neuralium.enrich.items_missing_summaries",
            () => GetItemsMissingSummariesCount(),
            description: "Number of items in database missing LLM summaries");

        _itemsMissingEmbeddings = s_meter.CreateObservableGauge(
            "neuralium.enrich.items_missing_embeddings",
            () => GetItemsMissingEmbeddingsCount(),
            description: "Number of items in database missing embeddings");
    }

    private int GetItemsMissingSummariesCount()
    {
        if (!_llmSettings.Enabled || !_llmSettings.EnableSummarization)
            return 0;

        try
        {
            return _dbContext.NewsItems
                .Where(item => item.Summary == null || item.Summary == string.Empty)
                .Where(item => item.RawContent != null && item.RawContent != string.Empty)
                .Count();
        }
        catch
        {
            return 0;
        }
    }

    private int GetItemsMissingEmbeddingsCount()
    {
        if (!_llmSettings.Enabled || !_llmSettings.EnableEmbeddings)
            return 0;

        try
        {
            return _dbContext.NewsItems
                .Where(item => item.EmbeddingJson == null || item.EmbeddingJson == string.Empty)
                .Where(item => (item.Summary != null && item.Summary != string.Empty) ||
                               (item.RawContent != null && item.RawContent != string.Empty))
                .Count();
        }
        catch
        {
            return 0;
        }
    }

    public async Task<PipelineContext> ProcessAsync(PipelineContext input, CancellationToken cancellationToken)
    {
        using var activity = s_activitySource.StartActivity("Enrich items");

        // First, backfill any items from the database that are missing enrichments
        var backfillStats = await BackfillMissingEnrichmentsAsync(cancellationToken);

        LogEnrichStarting(input.ClassifiedItems.Count, _llmSettings.Enabled, backfillStats.ItemsProcessed);

        var summariesGenerated = backfillStats.SummariesGenerated;
        var embeddingsGenerated = backfillStats.EmbeddingsGenerated;
        var errors = backfillStats.Errors;

        // Process each item for enrichment
        foreach (var item in input.ClassifiedItems)
        {
            try
            {
                // Generate summary if enabled and not already present
                if (_llmSettings.Enabled && _llmSettings.EnableSummarization && string.IsNullOrEmpty(item.Summary))
                {
                    var content = $"{item.Title}\n\n{item.RawContent ?? string.Empty}".Trim();
                    if (!string.IsNullOrEmpty(content))
                    {
                        var summary = await _llmService.GenerateSummaryAsync(content, cancellationToken);
                        if (!string.IsNullOrEmpty(summary))
                        {
                            item.Summary = summary;
                            summariesGenerated++;
                            s_summariesGenerated.Add(1);
                            LogSummaryGenerated(item.Title);
                        }
                    }
                }

                // Generate embedding if enabled and not already present
                if (_llmSettings.Enabled && _llmSettings.EnableEmbeddings && string.IsNullOrEmpty(item.EmbeddingJson))
                {
                    var content = $"{item.Title}\n\n{item.Summary ?? item.RawContent ?? string.Empty}".Trim();
                    if (!string.IsNullOrEmpty(content))
                    {
                        var embedding = await _llmService.GenerateEmbeddingAsync(content, cancellationToken);
                        if (embedding != null && embedding.Length > 0)
                        {
                            item.EmbeddingJson = JsonSerializer.Serialize(embedding);
                            embeddingsGenerated++;
                            s_embeddingsGenerated.Add(1);
                            LogEmbeddingGenerated(item.Title, embedding.Length);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errors++;
                s_enrichmentErrors.Add(1);
                LogEnrichmentError(item.Title, ex.Message);
                // Continue processing other items even if one fails
            }
        }

        // Save changes to database
        if (summariesGenerated > 0 || embeddingsGenerated > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        input.Metadata.StageItemCounts["Enrich"] = input.ClassifiedItems.Count;
        activity?.SetTag("summaries.generated", summariesGenerated);
        activity?.SetTag("embeddings.generated", embeddingsGenerated);
        activity?.SetTag("errors", errors);

        LogEnrichCompleted(
            input.ClassifiedItems.Count,
            summariesGenerated,
            embeddingsGenerated,
            errors);

        return input;
    }

    /// <summary>
    /// Backfills missing summaries and embeddings for items from previous pipeline runs.
    /// Processes up to 100 items per run to avoid overwhelming the system.
    /// </summary>
    private async Task<BackfillStats> BackfillMissingEnrichmentsAsync(CancellationToken cancellationToken)
    {
        var stats = new BackfillStats();

        if (!_llmSettings.Enabled)
        {
            return stats;
        }

        const int batchSize = 100;

        try
        {
            // Find items missing summaries (if summarization is enabled)
            if (_llmSettings.EnableSummarization)
            {
                var itemsMissingSummaries = await _dbContext.NewsItems
                    .Where(item => item.Summary == null || item.Summary == string.Empty)
                    .Where(item => item.RawContent != null && item.RawContent != string.Empty)
                    .OrderBy(item => item.PublishedAtUtc)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);

                foreach (var item in itemsMissingSummaries)
                {
                    try
                    {
                        var content = $"{item.Title}\n\n{item.RawContent ?? string.Empty}".Trim();
                        if (!string.IsNullOrEmpty(content))
                        {
                            var summary = await _llmService.GenerateSummaryAsync(content, cancellationToken);
                            if (!string.IsNullOrEmpty(summary))
                            {
                                item.Summary = summary;
                                stats.SummariesGenerated++;
                                s_summariesGenerated.Add(1);
                                LogBackfillSummaryGenerated(item.Title);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        stats.Errors++;
                        s_enrichmentErrors.Add(1);
                        LogBackfillError(item.Title, "summary", ex.Message);
                    }
                }

                stats.ItemsProcessed += itemsMissingSummaries.Count;
            }

            // Find items missing embeddings (if embeddings are enabled)
            if (_llmSettings.EnableEmbeddings)
            {
                var itemsMissingEmbeddings = await _dbContext.NewsItems
                    .Where(item => item.EmbeddingJson == null || item.EmbeddingJson == string.Empty)
                    .Where(item => (item.Summary != null && item.Summary != string.Empty) ||
                                   (item.RawContent != null && item.RawContent != string.Empty))
                    .OrderBy(item => item.PublishedAtUtc)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);

                foreach (var item in itemsMissingEmbeddings)
                {
                    try
                    {
                        var content = $"{item.Title}\n\n{item.Summary ?? item.RawContent ?? string.Empty}".Trim();
                        if (!string.IsNullOrEmpty(content))
                        {
                            var embedding = await _llmService.GenerateEmbeddingAsync(content, cancellationToken);
                            if (embedding != null && embedding.Length > 0)
                            {
                                item.EmbeddingJson = JsonSerializer.Serialize(embedding);
                                stats.EmbeddingsGenerated++;
                                s_embeddingsGenerated.Add(1);
                                LogBackfillEmbeddingGenerated(item.Title, embedding.Length);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        stats.Errors++;
                        s_enrichmentErrors.Add(1);
                        LogBackfillError(item.Title, "embedding", ex.Message);
                    }
                }

                // Only count unique items (some might need both summary and embedding)
                var existingSummaryIds = await _dbContext.NewsItems
                    .Where(item => item.Summary == null || item.Summary == string.Empty)
                    .Where(item => item.RawContent != null && item.RawContent != string.Empty)
                    .Take(batchSize)
                    .Select(i => i.Id)
                    .ToListAsync(cancellationToken);

                var uniqueItemsProcessed = itemsMissingEmbeddings
                    .Select(i => i.Id)
                    .Except(existingSummaryIds)
                    .Count();
                stats.ItemsProcessed += uniqueItemsProcessed;
            }

            // Save changes to database
            if (stats.SummariesGenerated > 0 || stats.EmbeddingsGenerated > 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                LogBackfillCompleted(stats.ItemsProcessed, stats.SummariesGenerated, stats.EmbeddingsGenerated);
            }
        }
        catch (Exception ex)
        {
            LogBackfillFailed(ex.Message);
        }

        return stats;
    }

    private sealed record BackfillStats
    {
        public int ItemsProcessed { get; set; }
        public int SummariesGenerated { get; set; }
        public int EmbeddingsGenerated { get; set; }
        public int Errors { get; set; }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Enrich: Processing {Count} classified items (LLM enabled: {LlmEnabled}), backfilled {BackfilledCount} items...")]
    private partial void LogEnrichStarting(int count, bool llmEnabled, int backfilledCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Generated summary for: {Title}")]
    private partial void LogSummaryGenerated(string title);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Generated embedding for: {Title} ({Dimensions} dimensions)")]
    private partial void LogEmbeddingGenerated(string title, int dimensions);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to enrich item '{Title}': {Error}")]
    private partial void LogEnrichmentError(string title, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Enrich: Completed - {TotalCount} items, {Summaries} summaries, {Embeddings} embeddings, {Errors} errors")]
    private partial void LogEnrichCompleted(int totalCount, int summaries, int embeddings, int errors);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Backfill: Generated summary for: {Title}")]
    private partial void LogBackfillSummaryGenerated(string title);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Backfill: Generated embedding for: {Title} ({Dimensions} dimensions)")]
    private partial void LogBackfillEmbeddingGenerated(string title, int dimensions);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Backfill: Failed to generate {Type} for '{Title}': {Error}")]
    private partial void LogBackfillError(string title, string type, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Backfill: Completed - {ItemCount} items, {Summaries} summaries, {Embeddings} embeddings")]
    private partial void LogBackfillCompleted(int itemCount, int summaries, int embeddings);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Backfill: Failed to process items: {Error}")]
    private partial void LogBackfillFailed(string error);
}
