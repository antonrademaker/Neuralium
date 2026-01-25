using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Neuralium.Data;
using Neuralium.Worker.Models;

namespace Neuralium.Worker.Agents;

/// <summary>
/// Classifies news items using keyword matching.
/// Assigns topics based on keyword presence in title and content.
/// Reads topic keywords from database for dynamic configuration.
/// </summary>
public partial class ClassifyAgent(
    ILogger<ClassifyAgent> logger,
    NeuraliumDbContext dbContext) : IAgent<PipelineContext, PipelineContext>
{
    private static readonly ActivitySource s_activitySource = new("Neuralium.Worker.ClassifyAgent");
    public async Task<PipelineContext> ProcessAsync(PipelineContext input, CancellationToken cancellationToken)
    {
        using var activity = s_activitySource.StartActivity("Classify items");
        LogClassifyStarting(input.UniqueItems.Count);

        // Load topic keywords from database grouped by topic
        var topicKeywords = await dbContext.TopicKeywords
            .Where(tk => tk.Enabled)
            .GroupBy(tk => tk.Topic)
            .ToDictionaryAsync(
                g => g.Key,
                g => g.Select(tk => tk.Keyword).ToList(),
                cancellationToken);

        LogLoadedTopics(topicKeywords.Count);

        foreach (var item in input.UniqueItems)
        {
            try
            {
                // Combine title and content for keyword matching
                var searchText = $"{item.Title} {item.RawContent}".ToLowerInvariant();

                // Find matching topics
                var matchedTopics = new List<string>();
                foreach (var (topic, keywords) in topicKeywords)
                {
                    if (keywords.Any(keyword =>
                        searchText.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                    {
                        matchedTopics.Add(topic);
                    }
                }

                // Set TopicsJson if any topics matched
                if (matchedTopics.Count > 0)
                {
                    item.TopicsJson = JsonSerializer.Serialize(matchedTopics);
                    LogItemClassified(item.Title, matchedTopics.Count, string.Join(", ", matchedTopics));
                }
                else
                {
                    LogItemUnclassified(item.Title);
                }

                input.ClassifiedItems.Add(item);
            }
            catch (Exception ex)
            {
                LogClassificationError(item.Title, ex.Message);
                input.Metadata.Errors.Add($"[Classify:{item.Title}] {ex.Message}");
            }
        }

        await Task.CompletedTask;

        input.Metadata.StageItemCounts["Classify"] = input.ClassifiedItems.Count;
        LogClassifyCompleted(input.ClassifiedItems.Count);

        return input;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Classify: Processing {Count} unique items...")]
    private partial void LogClassifyStarting(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Classify: Loaded {Count} topics from database")]
    private partial void LogLoadedTopics(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Classify: '{Title}' matched {Count} topics: {Topics}")]
    private partial void LogItemClassified(string title, int count, string topics);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Classify: '{Title}' matched no topics")]
    private partial void LogItemUnclassified(string title);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Classify: Error processing '{Title}': {Error}")]
    private partial void LogClassificationError(string title, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Classify: Completed with {Count} classified items")]
    private partial void LogClassifyCompleted(int count);
}
