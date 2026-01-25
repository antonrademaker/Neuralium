using System.ComponentModel.DataAnnotations;

namespace Neuralium.Data.Models;

/// <summary>
/// Represents a configured news feed source (RSS, Atom, GitHub releases, arXiv, etc.)
/// For arXiv sources, the Url field contains the search query instead of a URL.
/// </summary>
public sealed class FeedSource
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty; // "Rss", "Atom", "GitHub", "Reddit", "Medium", "Arxiv"

    [Required]
    [MaxLength(2000)]
    public string Url { get; set; } = string.Empty; // For Arxiv: search query like "cat:cs.AI AND ti:neural"

    public bool Enabled { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Minimum interval in minutes between fetches. 
    /// Default is 60 minutes (1 hour) for development.
    /// </summary>
    public int MinimumFetchIntervalMinutes { get; set; } = 60;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? LastFetchedAtUtc { get; set; }
}
