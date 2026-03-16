using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Neuralium.Data;
using Neuralium.Data.Models;
using Neuralium.Worker.Models;
using Neuralium.Worker.Services;

namespace Neuralium.Worker.Agents;

/// <summary>
/// Analyzes trends using topic frequency, recency, and optional LLM-based insights.
/// - Calculates 7d vs 30d momentum from historical data
/// - Extracts entities using LLM (when enabled)
/// - Creates separate NewsItemScore records
/// - Aggregates user feedback (thumbs up/down) to calculate UserFeedbackScore
/// </summary>
public partial class AnalyzeAgent(
    ILogger<AnalyzeAgent> logger,
    NeuraliumDbContext dbContext,
    ILlmService llmService,
    IOptions<LlmSettings> llmSettings) : IAgent<PipelineContext, PipelineContext>
{
    private static readonly ActivitySource s_activitySource = new("Neuralium.Worker.AnalyzeAgent");
    private readonly LlmSettings _llmSettings = llmSettings.Value;

    public async Task<PipelineContext> ProcessAsync(PipelineContext input, CancellationToken cancellationToken)
    {
        using var activity = s_activitySource.StartActivity("Analyze trends");
        LogAnalyzeStarting(input.ClassifiedItems.Count);

        var now = DateTime.UtcNow;
        var scores = new List<NewsItemScore>();
        var llmEnabled = _llmSettings.Enabled && _llmSettings.EnableTrendAnalysis;

        // Calculate 7d and 30d momentum (count of articles per topic)
        var sevenDaysAgo = now.AddDays(-7);
        var thirtyDaysAgo = now.AddDays(-30);

        var topicMomentum = await CalculateTopicMomentumAsync(sevenDaysAgo, thirtyDaysAgo, cancellationToken);

        // For new items (no IDs), feedback score will be 0.0
        // For existing items being re-scored, query feedback from database
        var itemsWithIds = input.ClassifiedItems.Where(item => item.Id > 0).ToList();
        var feedbackScores = new Dictionary<int, double>();

        if (itemsWithIds.Count > 0)
        {
            var itemIds = itemsWithIds.Select(item => item.Id).ToList();
            var feedbacks = await dbContext.NewsItemFeedbacks
                .Where(f => itemIds.Contains(f.NewsItemId))
                .GroupBy(f => f.NewsItemId)
                .Select(g => new
                {
                    NewsItemId = g.Key,
                    ThumbsUp = g.Count(f => f.FeedbackType == "ThumbsUp"),
                    ThumbsDown = g.Count(f => f.FeedbackType == "ThumbsDown")
                })
                .ToListAsync(cancellationToken);

            foreach (var feedback in feedbacks)
            {
                var total = feedback.ThumbsUp + feedback.ThumbsDown;
                if (total > 0)
                {
                    // Normalize to -1.0 to 1.0 range
                    feedbackScores[feedback.NewsItemId] = ((double)feedback.ThumbsUp - feedback.ThumbsDown) / total;
                }
            }

            LogFeedbackLoaded(feedbacks.Count, itemIds.Count);
        }

        // Calculate scores for each item
        foreach (var item in input.ClassifiedItems)
        {
            try
            {
                // Extract entities using LLM (if enabled)
                if (llmEnabled && !string.IsNullOrEmpty(item.RawContent))
                {
                    var contentForLlm = item.RawContent.Length > 2000
                        ? item.RawContent.Substring(0, 2000)
                        : item.RawContent;

                    var entities = await llmService.ExtractEntitiesAsync(
                        item.Title,
                        item.Url,
                        contentForLlm,
                        cancellationToken);

                    if (entities != null && entities.Count > 0)
                    {
                        item.EntitiesJson = JsonSerializer.Serialize(entities);
                        LogEntitiesExtracted(item.Title, item.Url, entities.Count);
                    }
                }

                // Calculate recency score (decay over 7 days)
                var daysSincePublished = (now - item.PublishedAtUtc).TotalDays;
                var recencyScore = Math.Max(0, 1.0 - (daysSincePublished / 7.0));

                // Calculate topic score based on:
                // 1. Number of topics (more topics = more relevant)
                // 2. Topic momentum (topics with 7d growth get bonus)
                var topics = string.IsNullOrEmpty(item.TopicsJson)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(item.TopicsJson) ?? new List<string>();

                var topicScore = CalculateTopicScore(topics, topicMomentum);

                // Get user feedback score (0.0 for new items)
                feedbackScores.TryGetValue(item.Id, out var userFeedbackScore);

                // Enhanced trend score: recency (30%), topics (30%), momentum (30%), feedback (10%)
                // Time is less important, LLM momentum analysis is more important (+20%)
                var momentumBonus = CalculateMomentumBonus(topics, topicMomentum);
                var computedTrendScore = (recencyScore * 0.3) + (topicScore * 0.3) + (momentumBonus * 0.3) + (userFeedbackScore * 0.1);

                // Set legacy TrendScore for backward compatibility
                item.TrendScore = computedTrendScore;

                // Create separate score record
                var score = new NewsItemScore
                {
                    RecencyScore = recencyScore,
                    TopicScore = topicScore,
                    UserFeedbackScore = userFeedbackScore,
                    ComputedTrendScore = computedTrendScore,
                    CalculatedAtUtc = now
                };

                scores.Add(score);
                input.AnalyzedItems.Add(item);
            }
            catch (Exception ex)
            {
                LogAnalysisError(item.Title, ex.Message);
                input.Metadata.Errors.Add($"[Analyze:{item.Title}] {ex.Message}");
            }
        }

        // Store scores in context metadata for later association with NewsItemId
        input.Metadata.PendingScores = scores;

        // Optional: Generate overall trend insights for the batch
        if (llmEnabled && input.AnalyzedItems.Count > 5)
        {
            await GenerateTrendInsightsAsync(input, cancellationToken);
        }

        input.Metadata.StageItemCounts["Analyze"] = input.AnalyzedItems.Count;
        LogAnalyzeCompleted(input.AnalyzedItems.Count);

        return input;
    }

    /// <summary>
    /// Calculate 7d vs 30d momentum for topics by counting articles in each time window
    /// </summary>
    private async Task<Dictionary<string, (int sevenDay, int thirtyDay)>> CalculateTopicMomentumAsync(
        DateTime sevenDaysAgo,
        DateTime thirtyDaysAgo,
        CancellationToken cancellationToken)
    {
        var momentum = new Dictionary<string, (int sevenDay, int thirtyDay)>();

        // Get all items from last 30 days
        var recentItems = await dbContext.NewsItems
            .Where(ni => ni.PublishedAtUtc >= thirtyDaysAgo)
            .Select(ni => new { ni.TopicsJson, ni.PublishedAtUtc })
            .ToListAsync(cancellationToken);

        // Count articles per topic in 7d and 30d windows
        foreach (var item in recentItems)
        {
            if (string.IsNullOrEmpty(item.TopicsJson))
                continue;

            try
            {
                var topics = JsonSerializer.Deserialize<List<string>>(item.TopicsJson);
                if (topics == null)
                    continue;

                var isInSevenDays = item.PublishedAtUtc >= sevenDaysAgo;

                foreach (var topic in topics)
                {
                    if (!momentum.TryGetValue(topic, out var counts))
                    {
                        counts = (0, 0);
                        momentum[topic] = counts;
                    }

                    var (sevenDay, thirtyDay) = counts;
                    momentum[topic] = (
                        isInSevenDays ? sevenDay + 1 : sevenDay,
                        thirtyDay + 1
                    );
                }
            }
            catch (JsonException)
            {
                // Skip malformed JSON
                continue;
            }
        }

        LogMomentumCalculated(momentum.Count);
        return momentum;
    }

    /// <summary>
    /// Calculate topic score based on number of topics and their individual relevance
    /// </summary>
    private static double CalculateTopicScore(List<string> topics, Dictionary<string, (int sevenDay, int thirtyDay)> topicMomentum)
    {
        if (topics.Count == 0)
            return 0.0;

        // Base score from topic count (cap at 3 topics)
        var baseScore = Math.Min(topics.Count / 3.0, 1.0);

        // Bonus for topics with high momentum
        var momentumBonus = 0.0;
        foreach (var topic in topics)
        {
            if (topicMomentum.TryGetValue(topic, out var counts) && counts.thirtyDay > 0)
            {
                // Topics with more recent activity get bonus
                var recentActivity = counts.sevenDay / (double)counts.thirtyDay;
                momentumBonus += recentActivity * 0.1;
            }
        }

        return Math.Min(baseScore + momentumBonus, 1.0);
    }

    /// <summary>
    /// Calculate momentum bonus based on 7d vs 30d article counts for the item's topics
    /// </summary>
    private static double CalculateMomentumBonus(List<string> topics, Dictionary<string, (int sevenDay, int thirtyDay)> topicMomentum)
    {
        if (topics.Count == 0)
            return 0.0;

        var totalMomentum = 0.0;
        var topicCount = 0;

        foreach (var topic in topics)
        {
            if (topicMomentum.TryGetValue(topic, out var counts) && counts.thirtyDay > 0)
            {
                // Momentum = recent activity / total activity
                // Values > 0.5 indicate acceleration
                var momentum = counts.sevenDay / (double)counts.thirtyDay;
                totalMomentum += momentum;
                topicCount++;
            }
        }

        if (topicCount == 0)
            return 0.0;

        // Average momentum across topics, scaled to 0-1 range
        return Math.Min((totalMomentum / topicCount) * 2.0, 1.0);
    }

    /// <summary>
    /// Generate overall trend insights for the analyzed batch using LLM
    /// </summary>
    private async Task GenerateTrendInsightsAsync(PipelineContext input, CancellationToken cancellationToken)
    {
        try
        {
            var articles = input.AnalyzedItems
                .Select(item => (
                    Title: item.Title,
                    Summary: item.Summary ?? "",
                    Topics: string.IsNullOrEmpty(item.TopicsJson)
                        ? new List<string>()
                        : JsonSerializer.Deserialize<List<string>>(item.TopicsJson) ?? new List<string>(),
                    Published: item.PublishedAtUtc
                ))
                .ToList();

            var insights = await llmService.AnalyzeTrendInsightsAsync(articles, cancellationToken);

            if (!string.IsNullOrEmpty(insights))
            {
                input.Metadata.TrendInsights = insights;
                LogTrendInsightsGenerated();
            }
        }
        catch (Exception ex)
        {
            LogTrendInsightsError(ex.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Analyze: Computing trends for {Count} items...")]
    private partial void LogAnalyzeStarting(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Analyze: Completed trend analysis for {Count} items")]
    private partial void LogAnalyzeCompleted(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Analyze: Loaded feedback for {FeedbackCount} of {ItemCount} items")]
    private partial void LogFeedbackLoaded(int feedbackCount, int itemCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Analyze: Calculated momentum for {Count} topics")]
    private partial void LogMomentumCalculated(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Analyze: Extracted entities for '{Title}' ({Url}): {Count} entities")]
    private partial void LogEntitiesExtracted(string title, string url, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Analyze: Generated trend insights for batch")]
    private partial void LogTrendInsightsGenerated();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Analyze: Error analyzing '{Title}': {Error}")]
    private partial void LogAnalysisError(string title, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Analyze: Error generating trend insights: {Error}")]
    private partial void LogTrendInsightsError(string error);
}
