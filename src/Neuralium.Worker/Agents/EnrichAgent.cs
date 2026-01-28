using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Neuralium.Data;
using Neuralium.Data.Models;
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
    private readonly IArticleFetcherService _articleFetcher;

    // Metrics for tracking enrichment status (initialized in constructor with callbacks)
    private ObservableGauge<int>? _itemsMissingSummaries;
    private ObservableGauge<int>? _itemsMissingEmbeddings;

    public EnrichAgent(
        ILogger<EnrichAgent> logger,
        ILlmService llmService,
        IOptions<LlmSettings> llmSettings,
        NeuraliumDbContext dbContext,
        IArticleFetcherService articleFetcher)
    {
        _logger = logger;
        _llmService = llmService;
        _llmSettings = llmSettings.Value;
        _dbContext = dbContext;
        _articleFetcher = articleFetcher;

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
        var (backfillStats, backfilledItems) = await BackfillMissingEnrichmentsAsync(cancellationToken);

        // Add backfilled items to the pipeline so they flow through Analyze and Publish
        if (backfilledItems.Count > 0)
        {
            input.ClassifiedItems.AddRange(backfilledItems);
            LogBackfilledItemsAddedToPipeline(backfilledItems.Count);
        }

        LogEnrichStarting(input.ClassifiedItems.Count, _llmSettings.Enabled, backfillStats.ItemsProcessed);

        var summariesGenerated = backfillStats.SummariesGenerated;
        var embeddingsGenerated = backfillStats.EmbeddingsGenerated;
        var errors = backfillStats.Errors;

        // Process items in batches of 10 for better resilience
        const int batchSize = 10;
        var itemBatches = input.ClassifiedItems
            .Select((item, index) => new { item, index })
            .GroupBy(x => x.index / batchSize)
            .Select(g => g.Select(x => x.item).ToList())
            .ToList();

        foreach (var batch in itemBatches)
        {
            var batchSummaries = 0;
            var batchEmbeddings = 0;

            // Process each item in the batch
            foreach (var item in batch)
            {
                try
                {
                    // Generate summary if enabled and (missing or outdated version)
                    var needsSummary = string.IsNullOrEmpty(item.Summary) ||
                                      item.SummaryVersion < _llmSettings.CurrentSummaryVersion;

                    if (_llmSettings.Enabled && _llmSettings.EnableSummarization && needsSummary)
                    {
                        // Check if content is insufficient and try to fetch full article
                        var contentToSummarize = item.RawContent;
                        if (_articleFetcher.IsInsufficientContent(item.RawContent))
                        {
                            LogFetchingFullArticle(item.Title);
                            var fetchedContent = await _articleFetcher.FetchArticleContentAsync(item.Url, cancellationToken);
                            if (!string.IsNullOrWhiteSpace(fetchedContent))
                            {
                                contentToSummarize = fetchedContent;
                                LogFetchedArticleContent(item.Title, fetchedContent.Length);
                            }
                        }

                        var content = $"{item.Title}\n\n{contentToSummarize ?? string.Empty}".Trim();
                        if (!string.IsNullOrEmpty(content))
                        {
                            var summary = await _llmService.GenerateSummaryAsync(content, cancellationToken);
                            if (!string.IsNullOrEmpty(summary))
                            {
                                item.Summary = summary;
                                item.SummaryVersion = _llmSettings.CurrentSummaryVersion;
                                summariesGenerated++;
                                batchSummaries++;
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
                                batchEmbeddings++;
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

            // Save batch to database
            if (batchSummaries > 0 || batchEmbeddings > 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                LogBatchSaved(batch.Count, batchSummaries, batchEmbeddings);
            }
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
    /// Processes ALL items that need enrichment, in batches of 10 for better resilience.
    /// Returns the backfilled items so they can be added to the pipeline and flow through Analyze and Publish stages.
    /// </summary>
    private async Task<(BackfillStats Stats, List<NewsItem> BackfilledItems)> BackfillMissingEnrichmentsAsync(CancellationToken cancellationToken)
    {
        var stats = new BackfillStats();
        var backfilledItems = new List<NewsItem>();

        if (!_llmSettings.Enabled)
        {
            return (stats, backfilledItems);
        }

        const int batchSize = 10;

        try
        {
            // Process items missing summaries OR with outdated versions (if summarization is enabled)
            if (_llmSettings.EnableSummarization)
            {
                bool hasMoreSummaries = true;
                while (hasMoreSummaries)
                {
                    var itemsMissingSummaries = await _dbContext.NewsItems
                        .Where(item => (item.Summary == null || item.Summary == string.Empty) ||
                                      item.SummaryVersion < _llmSettings.CurrentSummaryVersion)
                        .Where(item => item.RawContent != null && item.RawContent != string.Empty)
                        .OrderBy(item => item.PublishedAtUtc)
                        .Take(batchSize)
                        .ToListAsync(cancellationToken);

                    if (itemsMissingSummaries.Count == 0)
                    {
                        hasMoreSummaries = false;
                        break;
                    }

                    var batchSummaries = 0;
                    foreach (var item in itemsMissingSummaries)
                    {
                        try
                        {
                            // Check if content is insufficient and try to fetch full article
                            var contentToSummarize = item.RawContent;
                            if (_articleFetcher.IsInsufficientContent(item.RawContent))
                            {
                                LogBackfillFetchingArticle(item.Title);
                                var fetchedContent = await _articleFetcher.FetchArticleContentAsync(item.Url, cancellationToken);
                                if (!string.IsNullOrWhiteSpace(fetchedContent))
                                {
                                    contentToSummarize = fetchedContent;
                                    LogBackfillFetchedContent(item.Title, fetchedContent.Length);
                                }
                            }

                            var content = $"{item.Title}\n\n{contentToSummarize ?? string.Empty}".Trim();
                            if (!string.IsNullOrEmpty(content))
                            {
                                var summary = await _llmService.GenerateSummaryAsync(content, cancellationToken);
                                if (!string.IsNullOrEmpty(summary))
                                {
                                    item.Summary = summary;
                                    item.SummaryVersion = _llmSettings.CurrentSummaryVersion;
                                    stats.SummariesGenerated++;
                                    batchSummaries++;
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

                    // Save summary batch
                    if (batchSummaries > 0)
                    {
                        await _dbContext.SaveChangesAsync(cancellationToken);
                        LogBackfillBatchSaved("summaries", batchSummaries);
                        
                        // Add items with regenerated summaries to the backfilled list
                        backfilledItems.AddRange(itemsMissingSummaries.Where(i => !string.IsNullOrEmpty(i.Summary)));
                    }

                    // Stop if we got fewer items than batch size (no more items to process)
                    if (itemsMissingSummaries.Count < batchSize)
                    {
                        hasMoreSummaries = false;
                    }
                }
            }

            // Process items missing embeddings (if embeddings are enabled)
            if (_llmSettings.EnableEmbeddings)
            {
                bool hasMoreEmbeddings = true;
                var processedIds = new HashSet<int>();

                while (hasMoreEmbeddings)
                {
                    var itemsMissingEmbeddings = await _dbContext.NewsItems
                        .Where(item => item.EmbeddingJson == null || item.EmbeddingJson == string.Empty)
                        .Where(item => (item.Summary != null && item.Summary != string.Empty) ||
                                       (item.RawContent != null && item.RawContent != string.Empty))
                        .OrderBy(item => item.PublishedAtUtc)
                        .Take(batchSize)
                        .ToListAsync(cancellationToken);

                    if (itemsMissingEmbeddings.Count == 0)
                    {
                        hasMoreEmbeddings = false;
                        break;
                    }

                    var batchEmbeddings = 0;
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
                                    batchEmbeddings++;
                                    s_embeddingsGenerated.Add(1);
                                    LogBackfillEmbeddingGenerated(item.Title, embedding.Length);
                                }
                            }

                            // Track unique items to avoid double-counting
                            if (!processedIds.Contains(item.Id))
                            {
                                processedIds.Add(item.Id);
                                stats.ItemsProcessed++;
                            }
                        }
                        catch (Exception ex)
                        {
                            stats.Errors++;
                            s_enrichmentErrors.Add(1);
                            LogBackfillError(item.Title, "embedding", ex.Message);
                        }
                    }

                    // Save embedding batch
                    if (batchEmbeddings > 0)
                    {
                        await _dbContext.SaveChangesAsync(cancellationToken);
                        LogBackfillBatchSaved("embeddings", batchEmbeddings);
                    }

                    // Stop if we got fewer items than batch size (no more items to process)
                    if (itemsMissingEmbeddings.Count < batchSize)
                    {
                        hasMoreEmbeddings = false;
                    }
                }
            }

            if (stats.ItemsProcessed > 0)
            {
                LogBackfillCompleted(stats.ItemsProcessed, stats.SummariesGenerated, stats.EmbeddingsGenerated);
            }
        }
        catch (Exception ex)
        {
            LogBackfillFailed(ex.Message);
        }

        return (stats, backfilledItems);
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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Saved batch: {BatchSize} items ({Summaries} summaries, {Embeddings} embeddings)")]
    private partial void LogBatchSaved(int batchSize, int summaries, int embeddings);

    [LoggerMessage(Level = LogLevel.Information, Message = "Added {Count} backfilled items to pipeline for reprocessing")]
    private partial void LogBackfilledItemsAddedToPipeline(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Backfill: Generated summary for: {Title}")]
    private partial void LogBackfillSummaryGenerated(string title);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Backfill: Generated embedding for: {Title} ({Dimensions} dimensions)")]
    private partial void LogBackfillEmbeddingGenerated(string title, int dimensions);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Backfill: Failed to generate {Type} for '{Title}': {Error}")]
    private partial void LogBackfillError(string title, string type, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Backfill: Completed - {ItemCount} items, {Summaries} summaries, {Embeddings} embeddings")]
    private partial void LogBackfillCompleted(int itemCount, int summaries, int embeddings);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Backfill: Saved batch of {Type} ({Count} items)")]
    private partial void LogBackfillBatchSaved(string type, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Backfill: Failed to process items: {Error}")]
    private partial void LogBackfillFailed(string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Fetching full article content for: {Title}")]
    private partial void LogFetchingFullArticle(string title);

    [LoggerMessage(Level = LogLevel.Information, Message = "Fetched article content for '{Title}' ({Length} chars)")]
    private partial void LogFetchedArticleContent(string title, int length);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Backfill: Fetching article for: {Title}")]
    private partial void LogBackfillFetchingArticle(string title);

    [LoggerMessage(Level = LogLevel.Information, Message = "Backfill: Fetched article for '{Title}' ({Length} chars)")]
    private partial void LogBackfillFetchedContent(string title, int length);
}
