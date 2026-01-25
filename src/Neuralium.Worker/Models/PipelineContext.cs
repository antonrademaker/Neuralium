using Neuralium.Data.Models;

namespace Neuralium.Worker.Models;

/// <summary>
/// Context object passed through the pipeline stages.
/// Accumulates data and metadata as it flows through agents.
/// </summary>
public class PipelineContext
{
    /// <summary>
    /// Collection of raw news items from ingest stage
    /// </summary>
    public List<RawFeedItem> RawItems { get; set; } = [];

    /// <summary>
    /// Normalized news items ready for processing
    /// </summary>
    public List<NewsItem> NormalizedItems { get; set; } = [];

    /// <summary>
    /// Items after deduplication
    /// </summary>
    public List<NewsItem> UniqueItems { get; set; } = [];

    /// <summary>
    /// Classified and enriched items
    /// </summary>
    public List<NewsItem> ClassifiedItems { get; set; } = [];

    /// <summary>
    /// Final items with trend analysis
    /// </summary>
    public List<NewsItem> AnalyzedItems { get; set; } = [];

    /// <summary>
    /// Pipeline execution metadata
    /// </summary>
    public PipelineMetadata Metadata { get; set; } = new();
}

/// <summary>
/// Temporary model for raw feed items before normalization
/// </summary>
public class RawFeedItem
{
    public required string Source { get; init; }
    public required string Title { get; init; }
    public required string Url { get; init; }
    public DateTime PublishedAt { get; init; }
    public string? Content { get; init; }
}

/// <summary>
/// Metadata about pipeline execution
/// </summary>
public class PipelineMetadata
{
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public Dictionary<string, int> StageItemCounts { get; set; } = new();
    public List<string> Errors { get; set; } = [];

    /// <summary>
    /// Pending scores to be associated with NewsItems after they are saved.
    /// Populated by AnalyzeAgent, consumed by PublishAgent.
    /// </summary>
    public List<NewsItemScore> PendingScores { get; set; } = [];
}
