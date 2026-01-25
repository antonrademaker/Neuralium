using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Neuralium.Data;
using Neuralium.Data.Models;
using Neuralium.Worker.Models;

namespace Neuralium.Worker.Agents;

/// <summary>
///Analyzes trends using topic frequency and recency.
/// Creates separate NewsItemScore records instead of storing scores on NewsItem.
/// Aggregates user feedback (thumbs up/down) to calculate UserFeedbackScore.
/// </summary>
public partial class AnalyzeAgent(
    ILogger<AnalyzeAgent> logger,
    NeuraliumDbContext dbContext) : IAgent<PipelineContext, PipelineContext>
{
    private static readonly ActivitySource s_activitySource = new("Neuralium.Worker.AnalyzeAgent");

    public async Task<PipelineContext> ProcessAsync(PipelineContext input, CancellationToken cancellationToken)
    {
        using var activity = s_activitySource.StartActivity("Analyze trends");
        LogAnalyzeStarting(input.ClassifiedItems.Count);

        var now = DateTime.UtcNow;
        var scores = new List<NewsItemScore>();

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
            var daysSincePublished = (now - item.PublishedAtUtc).TotalDays;
            var recencyScore = Math.Max(0, 1.0 - (daysSincePublished / 7.0)); // Decay over 7 days

            var topicCount = string.IsNullOrEmpty(item.TopicsJson) ? 0 :
                item.TopicsJson.Count(c => c == ',') + 1; // Simple count
            var topicScore = Math.Min(topicCount / 3.0, 1.0); // Cap at 3 topics

            // Get user feedback score (0.0 for new items, calculated for existing items with feedback)
            feedbackScores.TryGetValue(item.Id, out var userFeedbackScore);
            var computedTrendScore = (recencyScore * 0.7) + (topicScore * 0.2) + (userFeedbackScore * 0.1);

            // Set legacy TrendScore for backward compatibility
            item.TrendScore = computedTrendScore;

            // Create separate score record
            var score = new NewsItemScore
            {
                // NewsItemId will be set after items are saved to database
                RecencyScore = recencyScore,
                TopicScore = topicScore,
                UserFeedbackScore = userFeedbackScore,
                ComputedTrendScore = computedTrendScore,
                CalculatedAtUtc = now
            };

            scores.Add(score);
            input.AnalyzedItems.Add(item);
        }

        // Store scores in context metadata for later association with NewsItemId
        input.Metadata.PendingScores = scores;

        await Task.CompletedTask;

        input.Metadata.StageItemCounts["Analyze"] = input.AnalyzedItems.Count;
        LogAnalyzeCompleted(input.AnalyzedItems.Count);

        return input;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Analyze: Computing trends for {Count} items...")]
    private partial void LogAnalyzeStarting(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Analyze: Completed trend analysis for {Count} items")]
    private partial void LogAnalyzeCompleted(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Analyze: Loaded feedback for {FeedbackCount} of {ItemCount} items")]
    private partial void LogFeedbackLoaded(int feedbackCount, int itemCount);
}
