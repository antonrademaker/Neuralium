using System.ComponentModel.DataAnnotations;

namespace Neuralium.Api.Models;

/// <summary>
/// Request model for submitting user feedback on news items or keywords.
/// </summary>
public sealed class SubmitFeedbackRequest
{
    /// <summary>
    /// User identifier (email, username, or external ID)
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Feedback type: "ThumbsUp" or "ThumbsDown"
    /// </summary>
    [Required]
    [MaxLength(20)]
    [RegularExpression("^(ThumbsUp|ThumbsDown)$", ErrorMessage = "FeedbackType must be 'ThumbsUp' or 'ThumbsDown'")]
    public string FeedbackType { get; set; } = string.Empty;
}
