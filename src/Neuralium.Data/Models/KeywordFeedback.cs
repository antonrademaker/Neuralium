using System.ComponentModel.DataAnnotations;

namespace Neuralium.Data.Models;

/// <summary>
/// User feedback on classification keywords (thumbs up/down for relevance).
/// Helps improve keyword quality and topic matching over time.
/// </summary>
public sealed class KeywordFeedback
{
    public int Id { get; set; }

    /// <summary>
    /// User identifier (email, username, or external ID)
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the keyword being rated
    /// </summary>
    public int TopicKeywordId { get; set; }
    public TopicKeyword? TopicKeyword { get; set; }

    /// <summary>
    /// Feedback type: "ThumbsUp" or "ThumbsDown"
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string FeedbackType { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
