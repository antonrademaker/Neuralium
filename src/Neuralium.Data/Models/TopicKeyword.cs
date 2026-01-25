using System.ComponentModel.DataAnnotations;

namespace Neuralium.Data.Models;

/// <summary>
/// Represents a topic with associated keywords for classification
/// </summary>
public sealed class TopicKeyword
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Topic { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Keyword { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }
}
