namespace Neuralium.Data.Models;

/// <summary>
/// Separated scoring for news items.
/// Recency score changes frequently based on time, while topic score is more stable.
/// This separation allows efficient recalculation and historical tracking.
/// </summary>
public sealed class NewsItemScore
{
    public int Id { get; set; }

    /// <summary>
    /// Reference to the news item being scored
    /// </summary>
    public int NewsItemId { get; set; }
    public NewsItem? NewsItem { get; set; }

    /// <summary>
    /// Time-based score (0.0 to 1.0) degrading over 7 days
    /// </summary>
    public double RecencyScore { get; set; }

    /// <summary>
    /// Topic relevance score (0.0 to 1.0) based on keyword matches and user feedback
    /// </summary>
    public double TopicScore { get; set; }

    /// <summary>
    /// User feedback score (-1.0 to 1.0) based on thumbs up/down votes
    /// </summary>
    public double UserFeedbackScore { get; set; }

    /// <summary>
    /// Combined trend score (weighted average of recency, topic, and feedback scores)
    /// </summary>
    public double ComputedTrendScore { get; set; }

    /// <summary>
    /// When this score was calculated
    /// </summary>
    public DateTime CalculatedAtUtc { get; set; }
}
