namespace Neuralium.Worker.Configuration;

/// <summary>
/// Configuration for keyword-based classification.
/// Maps to "Classification" section in appsettings.json
/// </summary>
public class ClassificationConfiguration
{
    public List<TopicKeywords> Topics { get; set; } = [];
}

/// <summary>
/// Topic with associated keywords for matching
/// </summary>
public class TopicKeywords
{
    public required string Topic { get; init; }
    public List<string> Keywords { get; init; } = [];
}
