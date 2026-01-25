using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Neuralium.Data;
using Neuralium.Worker.Models;

namespace Neuralium.Worker.Agents;

/// <summary>
/// Ingests news from RSS/Atom feeds, GitHub releases, Reddit, Medium, and arXiv.
/// Uses IFeedProvider abstraction for different source types with volatility handling.
/// Reads feed sources from database for dynamic configuration.
/// </summary>
public partial class IngestAgent(
    ILogger<IngestAgent> logger,
    IHttpClientFactory httpClientFactory,
    NeuraliumDbContext dbContext,
    IEnumerable<IFeedProvider> feedProviders) : ISourceAgent<PipelineContext>
{
    private static readonly ActivitySource s_activitySource = new("Neuralium.Worker.IngestAgent");
    private readonly List<IFeedProvider> _providers = feedProviders.ToList();
    public async Task<PipelineContext> ProduceAsync(CancellationToken cancellationToken)
    {
        using var activity = s_activitySource.StartActivity("Ingest feeds");
        LogIngestStarting();

        var context = new PipelineContext();
        var httpClient = httpClientFactory.CreateClient("FeedReader");

        // Load enabled feed sources from database
        var enabledSources = await dbContext.FeedSources
            .Where(s => s.Enabled)
            .ToListAsync(cancellationToken);

        LogProcessingSources(enabledSources.Count);

        foreach (var source in enabledSources)
        {
            try
            {
                // Rate limiting: Skip if fetched too recently
                if (source.LastFetchedAtUtc.HasValue)
                {
                    var timeSinceLastFetch = DateTime.UtcNow - source.LastFetchedAtUtc.Value;
                    var minimumInterval = TimeSpan.FromMinutes(source.MinimumFetchIntervalMinutes);

                    if (timeSinceLastFetch < minimumInterval)
                    {
                        var remainingTime = minimumInterval - timeSinceLastFetch;
                        LogRateLimited(source.Name, remainingTime.TotalMinutes);
                        continue;
                    }
                }

                // Find appropriate provider for this source type
                var provider = _providers.FirstOrDefault(p => p.CanHandle(source));
                if (provider == null)
                {
                    LogNoProviderFound(source.Name, source.Type);
                    continue;
                }

                LogFetchingFeed(source.Name, source.Url, source.Type);

                // Fetch items using provider abstraction
                var items = await provider.FetchAsync(source, httpClient, cancellationToken);

                LogFeedFetched(source.Name, items.Count);

                // Add items to context
                context.RawItems.AddRange(items);

                // Update last fetched timestamp
                source.LastFetchedAtUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                LogFeedError(source.Name, ex.Message);
                context.Metadata.Errors.Add($"[{source.Name}] {ex.Message}");
            }
        }

        // Save updated fetch timestamps
        if (enabledSources.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        context.Metadata.StageItemCounts["Ingest"] = context.RawItems.Count;
        LogIngestCompleted(context.RawItems.Count);

        return context;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ingest: Starting feed ingestion...")]
    private partial void LogIngestStarting();

    [LoggerMessage(Level = LogLevel.Information, Message = "Ingest: Processing {Count} enabled sources")]
    private partial void LogProcessingSources(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ingest: [{Source}] rate limited - skipped (wait {RemainingMinutes:F1} more minutes)")]
    private partial void LogRateLimited(string source, double remainingMinutes);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Ingest: No provider found for [{Source}] type '{Type}'")]
    private partial void LogNoProviderFound(string source, string type);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ingest: Fetching [{Source}] (type: {Type}) from {Url}")]
    private partial void LogFetchingFeed(string source, string url, string type);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ingest: [{Source}] fetched with {Count} raw items")]
    private partial void LogFeedFetched(string source, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Ingest: [{Source}] error: {Error}")]
    private partial void LogFeedError(string source, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ingest: Completed with {Count} raw items")]
    private partial void LogIngestCompleted(int count);
}
