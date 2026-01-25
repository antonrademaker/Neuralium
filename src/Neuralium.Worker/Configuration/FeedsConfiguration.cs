namespace Neuralium.Worker.Configuration;

/// <summary>
/// Configuration for news feed sources.
/// Maps to "Feeds" section in appsettings.json
/// </summary>
public class FeedsConfiguration
{
    public List<FeedSource> Sources { get; set; } = [];
}

/// <summary>
/// Individual feed source configuration
/// </summary>
public class FeedSource
{
    public required string Name { get; init; }
    public required string Type { get; init; } // "Rss", "Atom", "GitHub", "Reddit", "Medium"
    public required string Url { get; init; }
    public bool Enabled { get; init; } = true;
    public int TimeoutSeconds { get; init; } = 30;
}
