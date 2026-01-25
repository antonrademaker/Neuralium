using Neuralium.Data.Models;
using Neuralium.Worker.Models;

namespace Neuralium.Worker.Agents;

/// <summary>
/// Abstraction for fetching content from different feed types.
/// Handles volatility in feed formats and API differences.
/// </summary>
public interface IFeedProvider
{
    /// <summary>
    /// Determines if this provider can handle the given source type.
    /// </summary>
    bool CanHandle(FeedSource source);

    /// <summary>
    /// Fetches items from the feed source.
    /// Handles retries and API-specific error handling internally.
    /// </summary>
    Task<IReadOnlyList<RawFeedItem>> FetchAsync(
        FeedSource source,
        HttpClient httpClient,
        CancellationToken cancellationToken);
}
