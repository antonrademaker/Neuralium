using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Neuralium.Data.Models;
using Neuralium.Worker.Models;

namespace Neuralium.Worker.Agents;

/// <summary>
/// Normalizes raw feed items into canonical NewsItem format.
/// Removes tracking params, standardizes URLs, calculates hashes.
/// </summary>
public partial class NormalizeAgent(ILogger<NormalizeAgent> logger) : IAgent<PipelineContext, PipelineContext>
{
    private static readonly ActivitySource s_activitySource = new("Neuralium.Worker.NormalizeAgent");
    private static readonly string[] TrackingParams =
    [
        "utm_source", "utm_medium", "utm_campaign", "utm_term", "utm_content",
        "fbclid", "gclid", "msclkid", "_ga", "mc_cid", "mc_eid",
        "ref", "referrer", "source"
    ];

    public async Task<PipelineContext> ProcessAsync(PipelineContext input, CancellationToken cancellationToken)
    {
        using var activity = s_activitySource.StartActivity("Normalize items");
        LogNormalizeStarting(input.RawItems.Count);

        var ingestedAt = DateTime.UtcNow;

        foreach (var rawItem in input.RawItems)
        {
            try
            {
                // Normalize URL (remove tracking params)
                var normalizedUrl = NormalizeUrl(rawItem.Url);

                // Calculate SHA256 hash for deduplication
                var urlHash = ComputeSha256Hash(normalizedUrl);

                // Convert to NewsItem
                var newsItem = new NewsItem
                {
                    Source = rawItem.Source,
                    Title = rawItem.Title.Length > 500 ? rawItem.Title[..500] : rawItem.Title,
                    Url = normalizedUrl,
                    UrlHash = urlHash,
                    PublishedAtUtc = rawItem.PublishedAt.ToUniversalTime(),
                    IngestedAtUtc = ingestedAt,
                    RawContent = rawItem.Content?.Length > 2000 ? rawItem.Content[..2000] : rawItem.Content,
                    IsPublished = false
                };

                input.NormalizedItems.Add(newsItem);
            }
            catch (Exception ex)
            {
                LogNormalizationError(rawItem.Title, ex.Message);
                input.Metadata.Errors.Add($"[Normalize:{rawItem.Title}] {ex.Message}");
            }
        }

        await Task.CompletedTask;

        input.Metadata.StageItemCounts["Normalize"] = input.NormalizedItems.Count;
        LogNormalizeCompleted(input.NormalizedItems.Count);

        return input;
    }

    private static string NormalizeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url; // Return original if invalid
        }

        // Remove tracking query parameters
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        foreach (var param in TrackingParams)
        {
            query.Remove(param);
        }

        // Rebuild URL without tracking params
        var builder = new UriBuilder(uri)
        {
            Query = query.Count > 0 ? query.ToString() : string.Empty
        };

        return builder.Uri.ToString();
    }

    private static string ComputeSha256Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Normalize: Processing {Count} raw items...")]
    private partial void LogNormalizeStarting(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Normalize: Error processing '{Title}': {Error}")]
    private partial void LogNormalizationError(string title, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Normalize: Completed with {Count} normalized items")]
    private partial void LogNormalizeCompleted(int count);
}
