using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Neuralium.Data;
using Neuralium.Worker.Models;

namespace Neuralium.Worker.Agents;

/// <summary>
/// Publishes analyzed news items to database and Markdown output.
/// Final stage of the pipeline.
/// </summary>
public partial class PublishAgent(
    ILogger<PublishAgent> logger,
    NeuraliumDbContext dbContext) : ISinkAgent<PipelineContext>
{
    private static readonly ActivitySource s_activitySource = new("Neuralium.Worker.PublishAgent");
    private static readonly string[] UncategorizedArray = ["Uncategorized"];

    public async Task ConsumeAsync(PipelineContext input, CancellationToken cancellationToken)
    {
        using var activity = s_activitySource.StartActivity("Publish items");
        LogPublishStarting(input.AnalyzedItems.Count);

        if (input.AnalyzedItems.Count == 0)
        {
            LogNoItemsToPublish();
            return;
        }

        // Save items to database
        dbContext.NewsItems.AddRange(input.AnalyzedItems);
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

            dbContext.NewsItemScores.AddRange(input.Metadata.PendingScores);
            var scoresSaved = await dbContext.SaveChangesAsync(cancellationToken);
            LogScoresSaved(scoresSaved);
        }

        // Generate Markdown summary
        var markdownPath = Path.Combine(Environment.CurrentDirectory, "output",
            $"news-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.md");
        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);

        var markdown = GenerateMarkdown(input);
        await File.WriteAllTextAsync(markdownPath, markdown, cancellationToken);
        LogMarkdownGenerated(markdownPath);

        input.Metadata.StageItemCounts["Publish"] = input.AnalyzedItems.Count;
        LogPublishCompleted(input.AnalyzedItems.Count);
    }

    private static string GenerateMarkdown(PipelineContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# AI News Digest - {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine();
        sb.AppendLine($"**Pipeline Run:** {context.Metadata.StartedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Total Items:** {context.AnalyzedItems.Count}");
        sb.AppendLine();

        // Group by topic
        var itemsByTopic = context.AnalyzedItems
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

            var items = topicGroup
                .OrderByDescending(x => x.Item.TrendScore ?? 0)
                .ThenByDescending(x => x.Item.PublishedAtUtc)
                .Take(10)
                .Select(x => x.Item)
                .Distinct();

            foreach (var item in items)
            {
                sb.AppendLine($"### [{item.Title}]({item.Url})");
                sb.AppendLine();
                sb.AppendLine($"**Source:** {item.Source}");
                sb.AppendLine($"**Published:** {item.PublishedAtUtc:yyyy-MM-dd HH:mm} UTC");
                if (item.TrendScore.HasValue)
                {
                    sb.AppendLine($"**Trend Score:** {item.TrendScore.Value:F2}");
                }
                if (!string.IsNullOrEmpty(item.RawContent))
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

        return sb.ToString();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Publish: Generating output for {Count} items...")]
    private partial void LogPublishStarting(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Publish: No items to publish")]
    private partial void LogNoItemsToPublish();

    [LoggerMessage(Level = LogLevel.Information, Message = "Publish: Saved {Count} items to database")]
    private partial void LogSavedToDatabase(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Publish: Saved {Count} score records to database")]
    private partial void LogScoresSaved(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Publish: Generated Markdown at {Path}")]
    private partial void LogMarkdownGenerated(string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Publish: Completed - published {Count} items")]
    private partial void LogPublishCompleted(int count);
}
