using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Neuralium.Data;
using Neuralium.Data.Models;
using Neuralium.Worker.Models;
using Neuralium.Worker.Services;

namespace Neuralium.Worker.Agents;

/// <summary>
/// Publishes analyzed news items to database and Markdown output.
/// Final stage of the pipeline.
/// </summary>
public partial class PublishAgent(
    ILogger<PublishAgent> logger,
    NeuraliumDbContext dbContext,
    ILlmService llmService,
    IOptions<LlmSettings> llmSettings) : ISinkAgent<PipelineContext>
{
    private static readonly ActivitySource s_activitySource = new("Neuralium.Worker.PublishAgent");
    private static readonly string[] UncategorizedArray = ["Uncategorized"];
    private readonly LlmSettings _llmSettings = llmSettings.Value;

    public async Task ConsumeAsync(PipelineContext input, CancellationToken cancellationToken)
    {
        using var activity = s_activitySource.StartActivity("Publish items");
        LogPublishStarting(input.AnalyzedItems.Count);

        if (input.AnalyzedItems.Count == 0)
        {
            LogNoItemsToPublish();
        }
        else
        {

            // Save items to database (handle both new and existing items)
            var newItemsCount = 0;
            var updatedItemsCount = 0;

            foreach (var item in input.AnalyzedItems)
            {
                if (item.Id == 0)
                {
                    // New item - insert
                    dbContext.NewsItems.Add(item);
                    newItemsCount++;
                }
                else
                {
                    // Existing item - update
                    // Check if already tracked, if not attach and mark as modified
                    var existingEntry = dbContext.ChangeTracker.Entries<Data.Models.NewsItem>()
                        .FirstOrDefault(e => e.Entity.Id == item.Id);

                    if (existingEntry == null)
                    {
                        dbContext.NewsItems.Update(item);
                    }
                    // else: already tracked, changes will be saved automatically
                    updatedItemsCount++;
                }
            }

            LogItemsSplit(newItemsCount, updatedItemsCount);
            var savedCount = await dbContext.SaveChangesAsync(cancellationToken);
            LogSavedToDatabase(savedCount);

            // Associate score records with saved NewsItems
            if (input.Metadata.PendingScores.Count > 0 && input.AnalyzedItems.Count > 0)
            {
                for (int i = 0; i < input.AnalyzedItems.Count && i < input.Metadata.PendingScores.Count; i++)
                {
                    var item = input.AnalyzedItems[i];
                    var score = input.Metadata.PendingScores[i];
                    score.NewsItemId = item.Id; // Set the foreign key after NewsItem is saved
                }

                // Add all scores (they are always new records per run)
                dbContext.NewsItemScores.AddRange(input.Metadata.PendingScores);
                var scoresSaved = await dbContext.SaveChangesAsync(cancellationToken);
                LogScoresSaved(scoresSaved);
            }

        }

        // Generate Markdown summary
        var markdownPath = Path.Combine(Environment.CurrentDirectory, "output",
            $"news-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.md");
        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);

        // Query all recent items from database (last 90 days) for publishing
        var ninetyDaysAgo = DateTime.UtcNow.AddDays(-90);
        var allRecentItems = await dbContext.NewsItems
            .Where(item => item.PublishedAtUtc >= ninetyDaysAgo)
            .OrderByDescending(item => item.TrendScore ?? 0)
            .ToListAsync(cancellationToken);

        LogQueriedRecentItems(allRecentItems.Count);

        // Filter: relevance > 0.6 OR top 30
        var highRelevanceItems = allRecentItems
            .Where(item => item.TrendScore.HasValue && item.TrendScore.Value > 0.6)
            .ToList();

        var topItems = allRecentItems.Take(30).ToList();

        // Combine and deduplicate
        var itemsToPublish = highRelevanceItems
            .Union(topItems)
            .OrderByDescending(item => item.TrendScore ?? 0)
            .ToList();

        LogFilteredItems(itemsToPublish.Count, highRelevanceItems.Count, topItems.Count);

        // Generate LLM trend summary if enabled and we have items
        string? trendSummary = null;
        if (_llmSettings.Enabled && _llmSettings.EnableTrendAnalysis && itemsToPublish.Count > 0)
        {
            trendSummary = await GenerateTrendSummaryAsync(itemsToPublish, cancellationToken);
        }

        var markdown = await GenerateMarkdownAsync(itemsToPublish, trendSummary, input.Metadata.StartedAt, cancellationToken);
        await File.WriteAllTextAsync(markdownPath, markdown, cancellationToken);
        LogMarkdownGenerated(markdownPath);

        input.Metadata.StageItemCounts["Publish"] = itemsToPublish.Count;
        LogPublishCompleted(itemsToPublish.Count);
    }

    /// <summary>
    /// Generate LLM summary of most important trends and groundbreaking developments
    /// </summary>
    private async Task<string?> GenerateTrendSummaryAsync(List<NewsItem> items, CancellationToken cancellationToken)
    {
        try
        {
            var topItems = items.Take(20).ToList();

            var articles = topItems.Select(item => (
                Title: item.Title,
                Summary: item.Summary ?? "",
                Topics: string.IsNullOrEmpty(item.TopicsJson)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(item.TopicsJson) ?? new List<string>(),
                Published: item.PublishedAtUtc
            )).ToList();

            var summary = await llmService.AnalyzeTrendInsightsAsync(articles, cancellationToken);

            if (!string.IsNullOrEmpty(summary))
            {
                LogTrendSummaryGenerated();
                return summary;
            }
        }
        catch (Exception ex)
        {
            LogTrendSummaryError(ex.Message);
        }

        return null;
    }

    private static async Task<string> GenerateMarkdownAsync(List<NewsItem> items, string? trendSummary, DateTime runStartedAt, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# AI News Digest - {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine();
        sb.AppendLine($"**Pipeline Run:** {runStartedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Total Items:** {items.Count}");
        sb.AppendLine();

        // Add LLM-generated trend summary if available
        if (!string.IsNullOrEmpty(trendSummary))
        {
            sb.AppendLine("## 🔥 Key Trends & Groundbreaking Developments");
            sb.AppendLine();
            sb.AppendLine(trendSummary);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        // Group by topic
        var itemsByTopic = items
            .SelectMany(item =>
            {
                var topics = string.IsNullOrEmpty(item.TopicsJson)
                    ? UncategorizedArray
                    : JsonSerializer.Deserialize<string[]>(item.TopicsJson) ?? UncategorizedArray;
                return topics.Select(topic => (Topic: topic, Item: item));
            })
            .GroupBy(x => x.Topic)
            .OrderByDescending(g => g.Count());

        foreach (var topicGroup in itemsByTopic)
        {
            sb.AppendLine($"## {topicGroup.Key}");
            sb.AppendLine();

            var topicItems = topicGroup
                .OrderByDescending(x => x.Item.TrendScore ?? 0)
                .ThenByDescending(x => x.Item.PublishedAtUtc)
                .Take(10)
                .Select(x => x.Item)
                .Distinct();

            foreach (var item in topicItems)
            {
                sb.AppendLine($"### [{item.Title}]({item.Url})");
                sb.AppendLine();
                sb.AppendLine($"**Source:** {item.Source}");
                sb.AppendLine($"**Published:** {item.PublishedAtUtc:yyyy-MM-dd HH:mm} UTC");
                if (item.TrendScore.HasValue)
                {
                    sb.AppendLine($"**Trend Score:** {item.TrendScore.Value:F2}");
                }
                if (!string.IsNullOrEmpty(item.Summary))
                {
                    sb.AppendLine();
                    sb.AppendLine($"**Summary:** {item.Summary}");
                }
                else if (!string.IsNullOrEmpty(item.RawContent))
                {
                    var preview = item.RawContent.Length > 200
                        ? item.RawContent[..200] + "..."
                        : item.RawContent;
                    sb.AppendLine();
                    sb.AppendLine(preview);
                }
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
            }
        }

        await Task.CompletedTask;
        return sb.ToString();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Publish: Generating output for {Count} items...")]
    private partial void LogPublishStarting(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Publish: No items to publish")]
    private partial void LogNoItemsToPublish();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Publish: Saving {NewCount} new items and {UpdatedCount} updated items")]
    private partial void LogItemsSplit(int newCount, int updatedCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Publish: Saved {Count} items to database")]
    private partial void LogSavedToDatabase(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Publish: Saved {Count} score records to database")]
    private partial void LogScoresSaved(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Publish: Queried {Count} items from last 90 days")]
    private partial void LogQueriedRecentItems(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Publish: Filtered to {Total} items ({HighRelevance} > 0.6, {TopN} top-30)")]
    private partial void LogFilteredItems(int total, int highRelevance, int topN);

    [LoggerMessage(Level = LogLevel.Information, Message = "Publish: Generated LLM trend summary")]
    private partial void LogTrendSummaryGenerated();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Publish: Error generating trend summary: {Error}")]
    private partial void LogTrendSummaryError(string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Publish: Generated Markdown at {Path}")]
    private partial void LogMarkdownGenerated(string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Publish: Completed - published {Count} items")]
    private partial void LogPublishCompleted(int count);
}
