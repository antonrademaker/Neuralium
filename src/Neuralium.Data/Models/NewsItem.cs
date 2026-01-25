using System.ComponentModel.DataAnnotations;

namespace Neuralium.Data.Models;

/// <summary>
/// Represents a single news article from any source (RSS, GitHub, Reddit, Medium).
/// Normalized from various feed formats into a canonical structure.
/// </summary>
public sealed class NewsItem
{
    public int Id { get; set; }

    /// <summary>
    /// Source identifier (e.g., "TechCrunch", "GitHub:dotnet/aspire", "r/dotnet")
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Source { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Canonical URL after normalization (redirects resolved, tracking params removed)
    /// </summary>
    [Required]
    [MaxLength(2000)]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// SHA256 hash of normalized URL for deduplication
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string UrlHash { get; set; } = string.Empty;

    public DateTime PublishedAtUtc { get; set; }

    public DateTime IngestedAtUtc { get; set; }

    /// <summary>
    /// Optional LLM-generated summary
    /// </summary>
    [MaxLength(1000)]
    public string? Summary { get; set; }

    /// <summary>
    /// Raw content from feed (truncated to first ~500 chars)
    /// </summary>
    [MaxLength(2000)]
    public string? RawContent { get; set; }

    /// <summary>
    /// JSON array of classification labels (e.g., ["AI", "Azure", ".NET"])
    /// </summary>
    [MaxLength(500)]
    public string? TopicsJson { get; set; }

    /// <summary>
    /// JSON array of extracted entities (e.g., ["ChatGPT", "Microsoft", "C#"])
    /// </summary>
    [MaxLength(1000)]
    public string? EntitiesJson { get; set; }

    /// <summary>
    /// Embedding vector for semantic search (stored as JSON array of floats)
    /// </summary>
    public string? EmbeddingJson { get; set; }

    /// <summary>
    /// Trend score calculated by TrendEngine (7d vs 30d momentum, entity deltas, cluster growth)
    /// </summary>
    public double? TrendScore { get; set; }

    /// <summary>
    /// Whether this item passed classification filters and was published
    /// </summary>
    public bool IsPublished { get; set; }
}
