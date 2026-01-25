using System.Diagnostics;
using Neuralium.Data;
using Neuralium.Data.Models;
using Neuralium.Worker.Models;

namespace Neuralium.Worker.Agents;

/// <summary>
///Analyzes trends using topic frequency and recency.
/// Creates separate NewsItemScore records instead of storing scores on NewsItem.
/// Future: 7d vs 30d momentum, entity deltas, cluster growth, user feedback scoring.
/// </summary>
#pragma warning disable IDE0060 // Remove unused parameter - will be used for feedback scoring
public partial class AnalyzeAgent(
    ILogger<AnalyzeAgent> logger,
    NeuraliumDbContext dbContext) : IAgent<PipelineContext, PipelineContext>
#pragma warning restore IDE0060
{
    private static readonly ActivitySource s_activitySource = new("Neuralium.Worker.AnalyzeAgent");

    public async Task<PipelineContext> ProcessAsync(PipelineContext input, CancellationToken cancellationToken)
    {
        using var activity = s_activitySource.StartActivity("Analyze trends");
        LogAnalyzeStarting(input.ClassifiedItems.Count);

        var now = DateTime.UtcNow;
        var scores = new List<NewsItemScore>();

        // TODO: Use dbContext to query NewsItemFeedback for user feedback scores  
        _ = dbContext; // Suppress unused parameter warning

        // Calculate scores for each item
        foreach (var item in input.ClassifiedItems)
        {
            var daysSincePublished = (now - item.PublishedAtUtc).TotalDays;
            var recencyScore = Math.Max(0, 1.0 - (daysSincePublished / 7.0)); // Decay over 7 days

            var topicCount = string.IsNullOrEmpty(item.TopicsJson) ? 0 :
                item.TopicsJson.Count(c => c == ',') + 1; // Simple count
            var topicScore = Math.Min(topicCount / 3.0, 1.0); // Cap at 3 topics

            // Calculate user feedback score (will be 0 for new items without feedback)
            // TODO: Query NewsItemFeedback via dbContext to calculate feedback score
            var userFeedbackScore = 0.0;
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
}
