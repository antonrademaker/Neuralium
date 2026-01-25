using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Neuralium.Data;
using Neuralium.Worker.Models;

namespace Neuralium.Worker.Agents;

/// <summary>
/// Removes duplicate news items based on URL hash.
/// Checks database for existing items with the same normalized URL.
/// </summary>
public partial class DedupeAgent(
    ILogger<DedupeAgent> logger,
    NeuraliumDbContext dbContext) : IAgent<PipelineContext, PipelineContext>
{
    private static readonly ActivitySource s_activitySource = new("Neuralium.Worker.DedupeAgent");
    public async Task<PipelineContext> ProcessAsync(PipelineContext input, CancellationToken cancellationToken)
    {
        using var activity = s_activitySource.StartActivity("Deduplicate items");
        LogDedupeStarting(input.NormalizedItems.Count);

        if (input.NormalizedItems.Count == 0)
        {
            input.Metadata.StageItemCounts["Dedupe"] = 0;
            LogDedupeCompleted(0, 0);
            return input;
        }

        // Get all URL hashes from new items
        var newHashes = input.NormalizedItems.Select(item => item.UrlHash).ToHashSet();

        // Query database for existing items with matching hashes
        var existingHashes = await dbContext.NewsItems
            .Where(item => newHashes.Contains(item.UrlHash))
            .Select(item => item.UrlHash)
            .ToHashSetAsync(cancellationToken);

        LogFoundExisting(existingHashes.Count);

        // Filter out duplicates (both from database and within batch)
        var seenHashes = new HashSet<string>(existingHashes);
        var inBatchDuplicates = 0;

        foreach (var item in input.NormalizedItems)
        {
            if (!seenHashes.Contains(item.UrlHash))
            {
                input.UniqueItems.Add(item);
                seenHashes.Add(item.UrlHash); // Track within batch
            }
            else
            {
                if (existingHashes.Contains(item.UrlHash))
                {
                    LogSkippingDuplicate(item.Title, item.Url);
                }
                else
                {
                    inBatchDuplicates++;
                    LogSkippingBatchDuplicate(item.Title, item.Url);
                }
            }
        }

        var duplicateCount = input.NormalizedItems.Count - input.UniqueItems.Count;
        if (inBatchDuplicates > 0)
        {
            LogInBatchDuplicates(inBatchDuplicates);
        }
        input.Metadata.StageItemCounts["Dedupe"] = input.UniqueItems.Count;
        LogDedupeCompleted(input.UniqueItems.Count, duplicateCount);

        return input;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Dedupe: Processing {Count} normalized items...")]
    private partial void LogDedupeStarting(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Dedupe: Found {Count} existing items in database")]
    private partial void LogFoundExisting(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Dedupe: Skipping duplicate from database '{Title}' ({Url})")]
    private partial void LogSkippingDuplicate(string title, string url);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Dedupe: Skipping duplicate within batch '{Title}' ({Url})")]
    private partial void LogSkippingBatchDuplicate(string title, string url);

    [LoggerMessage(Level = LogLevel.Information, Message = "Dedupe: Found {Count} duplicates within current batch")]
    private partial void LogInBatchDuplicates(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Dedupe: Completed with {UniqueCount} unique items ({DuplicateCount} duplicates removed)")]
    private partial void LogDedupeCompleted(int uniqueCount, int duplicateCount);
}
