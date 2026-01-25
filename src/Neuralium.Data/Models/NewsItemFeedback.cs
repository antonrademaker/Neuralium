using System.ComponentModel.DataAnnotations;

namespace Neuralium.Data.Models;

/// <summary>
/// User feedback on news items (thumbs up/down for relevance ranking).
/// </summary>
public sealed class NewsItemFeedback
{
    public int Id { get; set; }

    /// <summary>
    /// User identifier (email, username, or external ID)
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the news item being rated
    /// </summary>
    public int NewsItemId { get; set; }
    public NewsItem? NewsItem { get; set; }

    /// <summary>
    /// Feedback type: "ThumbsUp" or "ThumbsDown"
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string FeedbackType { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
