using System.Diagnostics;
using CodeHollow.FeedReader;
using Neuralium.Data.Models;
using Neuralium.Worker.Models;

namespace Neuralium.Worker.Agents;

/// <summary>
/// Provider for standard RSS and Atom feeds.
/// Handles common feed formats using CodeHollow.FeedReader.
/// </summary>
public sealed partial class StandardFeedProvider(ILogger<StandardFeedProvider> logger) : IFeedProvider
{
    private static readonly ActivitySource s_activitySource = new("Neuralium.Worker.StandardFeedProvider");
    private static readonly string[] SupportedTypes = ["Rss", "Atom", "GitHub", "Reddit", "Medium"];

    public bool CanHandle(FeedSource source) =>
        SupportedTypes.Contains(source.Type, StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<RawFeedItem>> FetchAsync(
        FeedSource source,
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        using var activity = s_activitySource.StartActivity($"Fetch {source.Type} feed");
        activity?.SetTag("source.name", source.Name);
        activity?.SetTag("source.url", source.Url);

        try
        {
            httpClient.Timeout = TimeSpan.FromSeconds(source.TimeoutSeconds);
            var feed = await FeedReader.ReadAsync(source.Url, cancellationToken);

            var items = new List<RawFeedItem>();
            foreach (var item in feed.Items)
            {
                var url = item.Link ?? string.Empty;
                if (string.IsNullOrWhiteSpace(url))
                {
                    LogSkippingItemNoUrl(item.Title ?? "(No Title)");
                    continue;
                }

                items.Add(new RawFeedItem
                {
                    Source = source.Name,
                    Title = item.Title ?? "(No Title)",
                    Url = url,
                    PublishedAt = item.PublishingDate ?? DateTime.UtcNow,
                    Content = item.Description ?? item.Content ?? string.Empty
                });
            }

            activity?.SetTag("items.count", items.Count);
            LogFetchedItems(source.Name, items.Count);
            return items;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogFetchError(source.Name, ex.Message);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "StandardFeed: Skipping item with no URL: {Title}")]
    private partial void LogSkippingItemNoUrl(string title);

    [LoggerMessage(Level = LogLevel.Debug, Message = "StandardFeed: [{Source}] fetched {Count} items")]
    private partial void LogFetchedItems(string source, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "StandardFeed: [{Source}] error: {Error}")]
    private partial void LogFetchError(string source, string error);
}
