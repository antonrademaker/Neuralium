using System.Diagnostics;
using System.Web;
using CodeHollow.FeedReader;
using Neuralium.Data.Models;
using Neuralium.Worker.Models;

namespace Neuralium.Worker.Agents;

/// <summary>
/// Provider for arXiv API searches.
/// Constructs arXiv API queries and parses Atom responses.
/// API: http://export.arxiv.org/api/query
/// Rate limit: 3 second delay recommended between calls (handled by caller's rate limiting)
/// </summary>
public sealed partial class ArxivFeedProvider(ILogger<ArxivFeedProvider> logger) : IFeedProvider
{
    private static readonly ActivitySource s_activitySource = new("Neuralium.Worker.ArxivFeedProvider");
    private const string ArxivApiBase = "http://export.arxiv.org/api/query";
    private const int DefaultMaxResults = 50; // arXiv recommends keeping this reasonable

    public bool CanHandle(FeedSource source) =>
        string.Equals(source.Type, "Arxiv", StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<RawFeedItem>> FetchAsync(
        FeedSource source,
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        using var activity = s_activitySource.StartActivity("Fetch arXiv articles");
        activity?.SetTag("source.name", source.Name);
        activity?.SetTag("search.query", source.Url);

        try
        {
            // For arXiv, the Url field contains the search query
            // Examples: 
            //   "cat:cs.AI AND ti:neural"
            //   "all:machine learning"
            //   "au:smith AND cat:cs.LG"
            var queryUrl = BuildArxivQueryUrl(source.Url, DefaultMaxResults);
            activity?.SetTag("api.url", queryUrl);

            httpClient.Timeout = TimeSpan.FromSeconds(source.TimeoutSeconds);

            LogFetchingArxiv(source.Name, source.Url, queryUrl);

            // arXiv returns Atom feeds, so we can use FeedReader
            var feed = await FeedReader.ReadAsync(queryUrl, cancellationToken);

            var items = new List<RawFeedItem>();
            foreach (var item in feed.Items)
            {
                var url = item.Link ?? string.Empty;
                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                // arXiv items have rich metadata
                var content = item.Description ?? item.Content ?? string.Empty;

                // Extract arXiv ID from URL for reference
                var arxivId = ExtractArxivId(url);

                items.Add(new RawFeedItem
                {
                    Source = source.Name,
                    Title = item.Title ?? "(No Title)",
                    Url = url,
                    PublishedAt = item.PublishingDate ?? DateTime.UtcNow,
                    Content = content
                });

                LogArxivItem(arxivId, item.Title ?? "(No Title)");
            }

            activity?.SetTag("items.count", items.Count);
            LogFetchedArxiv(source.Name, items.Count);
            return items;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogArxivError(source.Name, ex.Message);
            throw;
        }
    }

    private static string BuildArxivQueryUrl(string searchQuery, int maxResults)
    {
        // Construct arXiv API URL with query parameters
        // API supports: search_query, start, max_results, sortBy, sortOrder
        var builder = new UriBuilder(ArxivApiBase);
        var query = HttpUtility.ParseQueryString(string.Empty);

        query["search_query"] = searchQuery;
        query["start"] = "0";
        query["max_results"] = maxResults.ToString();
        query["sortBy"] = "submittedDate"; // Sort by submission date
        query["sortOrder"] = "descending"; // Newest first

        builder.Query = query.ToString();
        return builder.ToString();
    }

    private static string ExtractArxivId(string url)
    {
        // Extract arXiv ID from URL like: http://arxiv.org/abs/2401.12345v1
        // or http://arxiv.org/abs/cs/0601001
        try
        {
            var uri = new Uri(url);
            var segments = uri.Segments;
            if (segments.Length > 0)
            {
                return segments[^1].TrimEnd('/');
            }
        }
        catchwd
        {
            // Fall through to return URL
        }

        return url;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "ArxivFeed: [{Source}] querying arXiv with '{Query}' -> {ApiUrl}")]
    private partial void LogFetchingArxiv(string source, string query, string apiUrl);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ArxivFeed: Found article {ArxivId}: {Title}")]
    private partial void LogArxivItem(string arxivId, string title);

    [LoggerMessage(Level = LogLevel.Information, Message = "ArxivFeed: [{Source}] fetched {Count} articles from arXiv")]
    private partial void LogFetchedArxiv(string source, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "ArxivFeed: [{Source}] error: {Error}")]
    private partial void LogArxivError(string source, string error);
}
