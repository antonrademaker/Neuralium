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
}
