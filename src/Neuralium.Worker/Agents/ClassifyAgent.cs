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
/// Classifies news items using a hybrid approach:
/// 1. Keyword matching (fast, deterministic baseline)
/// 2. Optional LLM refinement (when enabled, improves accuracy)
/// Falls back to keyword results if LLM is unavailable or fails.
/// </summary>
public partial class ClassifyAgent(
    ILogger<ClassifyAgent> logger,
    NeuraliumDbContext dbContext,
    ILlmService llmService,
    IOptions<LlmSettings> llmSettings) : IAgent<PipelineContext, PipelineContext>
{
    private static readonly ActivitySource s_activitySource = new("Neuralium.Worker.ClassifyAgent");
    private readonly LlmSettings _llmSettings = llmSettings.Value;
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

        // Get list of available topics for LLM (if enabled)
        var availableTopics = topicKeywords.Keys.ToList();
        var llmEnabled = _llmSettings.Enabled && _llmSettings.EnableClassification;

        foreach (var item in input.UniqueItems)
        {
            try
            {
                // Stage 1: Keyword-based classification (always run, serves as baseline)
                var keywordTopics = PerformKeywordClassification(item, topicKeywords);

                List<string> finalTopics;

                // Stage 2: Optional LLM refinement
                if (llmEnabled && availableTopics.Count > 0)
                {
                    using var llmActivity = s_activitySource.StartActivity("LLM Classification");
                    llmActivity?.SetTag("item.title", item.Title);

                    // Prepare content for LLM (limit size)
                    var contentForLlm = item.RawContent?.Length > 2000
                        ? item.RawContent.Substring(0, 2000)
                        : item.RawContent ?? "";

                    // Use LLM to refine classification, passing keyword results as context
                    var llmTopics = await llmService.ClassifyTopicsAsync(
                        item.Title,
                        contentForLlm,
                        availableTopics,
                        keywordTopics,
                        cancellationToken);

                    // Use LLM results if available, otherwise fall back to keywords
                    if (llmTopics != null && llmTopics.Count > 0)
                    {
                        finalTopics = llmTopics;
                        LogLlmClassificationUsed(item.Title, finalTopics.Count);
                    }
                    else
                    {
                        finalTopics = keywordTopics;
                        LogLlmClassificationFailed(item.Title, keywordTopics.Count);
                    }
                }
                else
                {
                    // LLM disabled or not available, use keyword results
                    finalTopics = keywordTopics;
                }

                // Store classification results
                if (finalTopics.Count > 0)
                {
                    item.TopicsJson = JsonSerializer.Serialize(finalTopics);
                    LogItemClassified(item.Title, finalTopics.Count, string.Join(", ", finalTopics));
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

        input.Metadata.StageItemCounts["Classify"] = input.ClassifiedItems.Count;
        LogClassifyCompleted(input.ClassifiedItems.Count);

        return input;
    }

    /// <summary>
    /// Performs keyword-based classification on a news item.
    /// This is the fast, deterministic baseline classification.
    /// </summary>
    private static List<string> PerformKeywordClassification(
        NewsItem item,
        Dictionary<string, List<string>> topicKeywords)
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

        return matchedTopics;
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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Classify: LLM refined '{Title}' classification to {Count} topics")]
    private partial void LogLlmClassificationUsed(string title, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Classify: LLM failed for '{Title}', using {Count} keyword topics")]
    private partial void LogLlmClassificationFailed(string title, int count);
}
