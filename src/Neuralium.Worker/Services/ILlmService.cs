namespace Neuralium.Worker.Services;

/// <summary>
/// Service for interacting with Large Language Models (LLMs) for summarization and embeddings.
/// </summary>
public interface ILlmService
{
    /// <summary>
    /// Generates a concise summary of the provided content using an LLM.
    /// </summary>
    /// <param name="content">The content to summarize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated summary, or null if summarization fails.</returns>
    Task<string?> GenerateSummaryAsync(string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an embedding vector for the provided content.
    /// </summary>
    /// <param name="content">The content to embed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The embedding vector as a float array, or null if embedding fails.</returns>
    Task<float[]?> GenerateEmbeddingAsync(string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Classifies content into topics using an LLM.
    /// </summary>
    /// <param name="title">The title of the content.</param>
    /// <param name="content">The content to classify.</param>
    /// <param name="availableTopics">List of available topics to choose from.</param>
    /// <param name="keywordTopics">Topics suggested by keyword matching (for hybrid approach).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of classified topics, or null if classification fails.</returns>
    Task<List<string>?> ClassifyTopicsAsync(string title, string content, List<string> availableTopics, List<string>? keywordTopics = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts key entities (companies, products, technologies, people) from content.
    /// </summary>
    /// <param name="title">The title of the content.</param>
    /// <param name="content">The content to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of extracted entities, or null if extraction fails.</returns>
    Task<List<string>?> ExtractEntitiesAsync(string title, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes a group of related articles to identify trend insights.
    /// </summary>
    /// <param name="articles">List of article summaries to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Trend insight summary, or null if analysis fails.</returns>
    Task<string?> AnalyzeTrendInsightsAsync(List<(string Title, string Summary, List<string> Topics, DateTime Published)> articles, CancellationToken cancellationToken = default);
}
