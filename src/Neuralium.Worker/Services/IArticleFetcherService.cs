namespace Neuralium.Worker.Services;

/// <summary>
/// Service for fetching and extracting readable content from web article URLs.
/// Used to enrich news items that have minimal RSS content (e.g., Hacker News links).
/// </summary>
public interface IArticleFetcherService
{
    /// <summary>
    /// Fetches the article content from a URL and extracts the main text.
    /// Returns null if the content cannot be fetched or extracted.
    /// </summary>
    /// <param name="url">The article URL to fetch</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Extracted article text, or null if fetch/extraction fails</returns>
    Task<string?> FetchArticleContentAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines if content appears to be insufficient (just HTML links, very short, etc.)
    /// and would benefit from fetching the full article.
    /// </summary>
    /// <param name="content">The RSS feed content/description</param>
    /// <returns>True if content should be enriched by fetching the full article</returns>
    bool IsInsufficientContent(string? content);
}
