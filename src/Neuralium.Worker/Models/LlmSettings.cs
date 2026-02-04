namespace Neuralium.Worker.Models;

/// <summary>
/// Configuration settings for LLM integration (Azure OpenAI, OpenAI, or local models).
/// </summary>
public sealed class LlmSettings
{
    /// <summary>
    /// Enable LLM enrichment (summarization and embeddings)
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// LLM provider: "AzureOpenAI", "OpenAI", or "Local"
    /// </summary>
    public string Provider { get; set; } = "AzureOpenAI";

    /// <summary>
    /// API endpoint URL (for Azure OpenAI or custom endpoints)
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// API key for authentication
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Deployment name (Azure OpenAI) or model name (OpenAI)
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// Embedding model name (optional, for semantic search)
    /// </summary>
    public string? EmbeddingModelName { get; set; }

    /// <summary>
    /// Enable classification for news items using LLM
    /// </summary>
    public bool EnableClassification { get; set; }

    /// <summary>
    /// Enable trend analysis (entity extraction and insights) using LLM
    /// </summary>
    public bool EnableTrendAnalysis { get; set; }

    /// <summary>
    /// Enable summarization for news items
    /// </summary>
    public bool EnableSummarization { get; set; } = true;

    /// <summary>
    /// Current version of the summarization algorithm.
    /// Increment this when making improvements (e.g., adding article fetching) to force regeneration.
    /// Version 0 = original (RSS only), Version 1 = with article fetching
    /// </summary>
    public int CurrentSummaryVersion { get; set; } = 1;

    /// <summary>
    /// Enable embeddings for semantic search
    /// </summary>
    public bool EnableEmbeddings { get; set; }

    /// <summary>
    /// Maximum tokens for generated summaries
    /// </summary>
    public int MaxSummaryTokens { get; set; } = 150;

    /// <summary>
    /// Temperature for LLM generation (0.0-1.0, higher = more creative)
    /// </summary>
    public float Temperature { get; set; } = 0.3f;

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum retry attempts for failed requests
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}
