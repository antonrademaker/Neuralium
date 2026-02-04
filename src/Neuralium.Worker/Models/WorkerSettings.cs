namespace Neuralium.Worker.Models;

/// <summary>
/// Configuration settings for the Worker service.
/// </summary>
public sealed class WorkerSettings
{
    /// <summary>
    /// Enable automatic backfilling of missing scores for existing news items
    /// </summary>
    public bool BackfillMissingScores { get; set; } = true;

    /// <summary>
    /// Maximum age in days for items to consider for backfilling
    /// </summary>
    public int BackfillMaxDays { get; set; } = 90;
}
