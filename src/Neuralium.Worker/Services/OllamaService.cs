using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Neuralium.Worker.Models;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

namespace Neuralium.Worker.Services;

/// <summary>
/// Ollama implementation of the LLM service for local model inference.
/// </summary>
public partial class OllamaService : ILlmService, IDisposable
{
    private readonly ILogger<OllamaService> _logger;
    private readonly LlmSettings _settings;
    private readonly OllamaApiClient? _client;
    private readonly ActivitySource _activitySource;
    private bool _disposed;

    public OllamaService(
        ILogger<OllamaService> logger,
        IOptions<LlmSettings> settings,
        ActivitySource activitySource)
    {
        _logger = logger;
        _settings = settings.Value;
        _activitySource = activitySource;

        if (_settings.Enabled && !string.IsNullOrEmpty(_settings.Endpoint) && !string.IsNullOrEmpty(_settings.ModelName))
        {
            _client = new OllamaApiClient(new Uri(_settings.Endpoint), _settings.ModelName);
            LogClientInitialized(_settings.Endpoint, _settings.ModelName);
        }
        else
        {
            LogServiceDisabled();
        }
    }

    public async Task<string?> GenerateSummaryAsync(string content, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("GenerateSummary");
        activity?.SetTag("content.length", content.Length);

        if (!_settings.Enabled || !_settings.EnableSummarization)
        {
            LogSummarizationDisabled();
            return null;
        }

        if (_client == null)
        {
            LogClientNotInitialized("summary");
            return null;
        }

        var prompt = $@"Summarize the following news article in 2-3 concise sentences. Focus on the key facts and developments.

Article:
{content}

Summary:";

        try
        {
            var request = new ChatRequest
            {
                Model = _settings.ModelName ?? _client.SelectedModel,
                Messages = [new Message(ChatRole.User, prompt)],
                Stream = false,
                Options = new RequestOptions
                {
                    Temperature = _settings.Temperature
                }
            };

            var responses = await _client.ChatAsync(request, cancellationToken).ToListAsync(cancellationToken);
            var lastResponse = responses.LastOrDefault();
            var summary = lastResponse?.Message?.Content?.Trim();

            if (!string.IsNullOrEmpty(summary))
            {
                LogSummaryGenerated(summary.Length);
            }

            return summary;
        }
        catch (HttpRequestException ex)
        {
            LogHttpError("summarization", ex);
            return null;
        }
        catch (OperationCanceledException)
        {
            LogOperationCancelled("summarization");
            throw;
        }
        catch (Exception ex)
        {
            LogUnexpectedError("summarization", ex);
            return null;
        }
    }

    public async Task<float[]?> GenerateEmbeddingAsync(string content, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("GenerateEmbedding");
        activity?.SetTag("content.length", content.Length);

        if (!_settings.Enabled || !_settings.EnableEmbeddings)
        {
            LogEmbeddingsDisabled();
            return null;
        }

        if (_client == null)
        {
            LogClientNotInitialized("embedding");
            return null;
        }

        if (string.IsNullOrEmpty(_settings.EmbeddingModelName))
        {
            LogEmbeddingModelNotConfigured();
            return null;
        }

        try
        {
            var request = new EmbedRequest
            {
                Model = _settings.EmbeddingModelName,
                Input = [content]
            };

            var response = await _client.EmbedAsync(request, cancellationToken);

            if (response?.Embeddings != null && response.Embeddings.Count > 0)
            {
                var embedding = response.Embeddings[0];
                activity?.SetTag("embedding.dimensions", embedding.Length);
                LogEmbeddingGenerated(embedding.Length);
                return embedding;
            }

            return null;
        }
        catch (HttpRequestException ex)
        {
            LogHttpError("embedding", ex);
            return null;
        }
        catch (OperationCanceledException)
        {
            LogOperationCancelled("embedding");
            throw;
        }
        catch (Exception ex)
        {
            LogUnexpectedError("embedding", ex);
            return null;
        }
    }

    public async Task<List<string>?> ClassifyTopicsAsync(
        string title,
        string url,
        string content,
        List<string> availableTopics,
        List<string>? keywordTopics = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("ClassifyTopics");
        activity?.SetTag("title", title);
        activity?.SetTag("url", url);
        activity?.SetTag("available_topics.count", availableTopics.Count);

        if (!_settings.Enabled || !_settings.EnableClassification)
        {
            LogClassificationDisabled();
            return null;
        }

        if (_client == null)
        {
            LogClientNotInitialized("classification");
            return null;
        }

        // Build prompt for classification
        var topicsString = string.Join(", ", availableTopics);
        var keywordContext = keywordTopics != null && keywordTopics.Count > 0
            ? $"\n\nKeyword matching suggested these topics: {string.Join(", ", keywordTopics)}\nConsider these suggestions but make your own determination based on the content."
            : "";

        var prompt = $@"You are a news article classifier. Classify the following article into one or more relevant topics from the list provided.

Available Topics: {topicsString}

Title: {title}

Content: {content.Substring(0, Math.Min(content.Length, 1000))}{keywordContext}

Instructions:
- Select all relevant topics that apply (the article may belong to multiple topics)
- Only use topics from the available list
- Be precise and avoid over-classification
- Return ONLY a JSON array of topic names, nothing else

Example response: [""AI"", ""Machine Learning""]

Your classification:";

        try
        {
            var request = new ChatRequest
            {
                Model = _settings.ModelName ?? _client.SelectedModel,
                Messages = [new Message(ChatRole.User, prompt)],
                Stream = false,
                Options = new RequestOptions
                {
                    Temperature = 0.1f // Low temperature for more deterministic classification
                }
            };

            var responses = await _client.ChatAsync(request, cancellationToken).ToListAsync(cancellationToken);
            var lastResponse = responses.LastOrDefault();
            var responseText = lastResponse?.Message?.Content?.Trim();

            if (string.IsNullOrEmpty(responseText))
            {
                LogClassificationFailed("Empty response from LLM");
                return null;
            }

            // Parse JSON array from response
            try
            {
                // Remove markdown code blocks if present
                responseText = responseText.Replace("```json", "").Replace("```", "").Trim();

                var topics = System.Text.Json.JsonSerializer.Deserialize<List<string>>(responseText);

                if (topics == null || topics.Count == 0)
                {
                    LogClassificationFailed("No topics in response");
                    return null;
                }

                // Validate that returned topics are in available list (case-insensitive)
                var validTopics = topics
                    .Where(t => availableTopics.Any(at => at.Equals(t, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                activity?.SetTag("classified_topics.count", validTopics.Count);
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    var topicsList = string.Join(", ", validTopics);
                    LogTopicsClassified(title, url, validTopics.Count, topicsList);
                }

                return validTopics;
            }
            catch (System.Text.Json.JsonException ex)
            {
                LogClassificationFailed($"JSON parse error: {ex.Message}. Response: {responseText}");
                return null;
            }
        }
        catch (HttpRequestException ex)
        {
            LogHttpError("classification", ex);
            return null;
        }
        catch (OperationCanceledException)
        {
            LogOperationCancelled("classification");
            throw;
        }
        catch (Exception ex)
        {
            LogUnexpectedError("classification", ex);
            return null;
        }
    }

    public async Task<List<string>?> ExtractEntitiesAsync(
        string title,
        string url,
        string content,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("ExtractEntities");
        activity?.SetTag("title", title);
        activity?.SetTag("url", url);

        if (!_settings.Enabled || !_settings.EnableTrendAnalysis)
        {
            LogTrendAnalysisDisabled();
            return null;
        }

        if (_client == null)
        {
            LogClientNotInitialized("entity extraction");
            return null;
        }

        var prompt = $@"Extract key entities from this AI/technology news article. Focus on:
- Companies and organizations
- Products and platforms  
- Technologies and frameworks
- Programming languages
- Key people (if mentioned prominently)

Title: {title}

Content: {content.Substring(0, Math.Min(content.Length, 1500))}

Instructions:
- Return ONLY a JSON array of entity names
- Use proper capitalization (e.g., ""OpenAI"", ""ChatGPT"", ""Microsoft"")
- Include 3-10 most relevant entities
- Be precise and avoid duplicates

Example: [""OpenAI"", ""GPT-4"", ""Microsoft"", ""Azure""]

Your response:";

        try
        {
            var request = new ChatRequest
            {
                Model = _settings.ModelName ?? _client.SelectedModel,
                Messages = [new Message(ChatRole.User, prompt)],
                Stream = false,
                Options = new RequestOptions
                {
                    Temperature = 0.2f
                }
            };

            var responses = await _client.ChatAsync(request, cancellationToken).ToListAsync(cancellationToken);
            var lastResponse = responses.LastOrDefault();
            var responseText = lastResponse?.Message?.Content?.Trim();

            if (string.IsNullOrEmpty(responseText))
            {
                LogEntityExtractionFailed("Empty response from LLM");
                return null;
            }

            try
            {
                responseText = responseText.Replace("```json", "").Replace("```", "").Trim();
                var entities = System.Text.Json.JsonSerializer.Deserialize<List<string>>(responseText);

                if (entities == null || entities.Count == 0)
                {
                    LogEntityExtractionFailed("No entities in response");
                    return null;
                }

                activity?.SetTag("entities.count", entities.Count);
                LogEntitiesExtracted(title, url, entities.Count);

                return entities;
            }
            catch (System.Text.Json.JsonException ex)
            {
                LogEntityExtractionFailed($"JSON parse error: {ex.Message}. Response: {responseText}");
                return null;
            }
        }
        catch (HttpRequestException ex)
        {
            LogHttpError("entity extraction", ex);
            return null;
        }
        catch (OperationCanceledException)
        {
            LogOperationCancelled("entity extraction");
            throw;
        }
        catch (Exception ex)
        {
            LogUnexpectedError("entity extraction", ex);
            return null;
        }
    }

    public async Task<string?> AnalyzeTrendInsightsAsync(
        List<(string Title, string Summary, List<string> Topics, DateTime Published)> articles,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("AnalyzeTrendInsights");
        activity?.SetTag("articles.count", articles.Count);

        if (!_settings.Enabled || !_settings.EnableTrendAnalysis)
        {
            LogTrendAnalysisDisabled();
            return null;
        }

        if (_client == null)
        {
            LogClientNotInitialized("trend analysis");
            return null;
        }

        if (articles.Count == 0)
        {
            return null;
        }

        // Build article summaries for analysis
        var articleSummaries = new System.Text.StringBuilder();
        foreach (var (title, summary, topics, published) in articles.Take(20))
        {
            var topicsStr = string.Join(", ", topics);
            articleSummaries.AppendLine($"- [{published:yyyy-MM-dd}] {title}");
            articleSummaries.AppendLine($"  Topics: {topicsStr}");
            if (!string.IsNullOrEmpty(summary))
            {
                articleSummaries.AppendLine($"  Summary: {summary}");
            }
            articleSummaries.AppendLine();
        }

        var prompt = $@"Analyze these recent AI/technology articles and identify emerging trends, patterns, or themes.

Articles:
{articleSummaries}

Provide a concise trend analysis (2-3 sentences) that:
- Identifies the most significant emerging patterns
- Notes any momentum or clustering around specific topics
- Highlights what's gaining attention

Your analysis:";

        try
        {
            var request = new ChatRequest
            {
                Model = _settings.ModelName ?? _client.SelectedModel,
                Messages = [new Message(ChatRole.User, prompt)],
                Stream = false,
                Options = new RequestOptions
                {
                    Temperature = 0.4f
                }
            };

            var responses = await _client.ChatAsync(request, cancellationToken).ToListAsync(cancellationToken);
            var lastResponse = responses.LastOrDefault();
            var insight = lastResponse?.Message?.Content?.Trim();

            if (!string.IsNullOrEmpty(insight))
            {
                LogTrendInsightsGenerated(articles.Count);
            }

            return insight;
        }
        catch (HttpRequestException ex)
        {
            LogHttpError("trend analysis", ex);
            return null;
        }
        catch (OperationCanceledException)
        {
            LogOperationCancelled("trend analysis");
            throw;
        }
        catch (Exception ex)
        {
            LogUnexpectedError("trend analysis", ex);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ollama client initialized: Endpoint={Endpoint}, Model={Model}")]
    private partial void LogClientInitialized(string endpoint, string model);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ollama service disabled or not configured")]
    private partial void LogServiceDisabled();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Summarization disabled via configuration")]
    private partial void LogSummarizationDisabled();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Embeddings disabled via configuration")]
    private partial void LogEmbeddingsDisabled();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cannot generate {Type}: Ollama client not initialized")]
    private partial void LogClientNotInitialized(string type);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cannot generate embedding: EmbeddingModelName not configured")]
    private partial void LogEmbeddingModelNotConfigured();

    [LoggerMessage(Level = LogLevel.Information, Message = "Generated summary: {Length} characters")]
    private partial void LogSummaryGenerated(int length);

    [LoggerMessage(Level = LogLevel.Information, Message = "Generated embedding: {Dimensions} dimensions")]
    private partial void LogEmbeddingGenerated(int dimensions);

    [LoggerMessage(Level = LogLevel.Warning, Message = "LLM {Operation} failed with HTTP error (is Ollama running?)")]
    private partial void LogHttpError(string operation, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "LLM {Operation} cancelled")]
    private partial void LogOperationCancelled(string operation);

    [LoggerMessage(Level = LogLevel.Error, Message = "LLM {Operation} failed with unexpected error")]
    private partial void LogUnexpectedError(string operation, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Classification disabled via configuration")]
    private partial void LogClassificationDisabled();

    [LoggerMessage(Level = LogLevel.Information, Message = "Classified '{Title}' ({Url}) into {Count} topics: {Topics}")]
    private partial void LogTopicsClassified(string title, string url, int count, string topics);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Classification failed: {Reason}")]
    private partial void LogClassificationFailed(string reason);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Trend analysis disabled via configuration")]
    private partial void LogTrendAnalysisDisabled();

    [LoggerMessage(Level = LogLevel.Information, Message = "Extracted {Count} entities from '{Title}' ({Url})")]
    private partial void LogEntitiesExtracted(string title, string url, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Entity extraction failed: {Reason}")]
    private partial void LogEntityExtractionFailed(string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Generated trend insights for {Count} articles")]
    private partial void LogTrendInsightsGenerated(int count);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _activitySource.Dispose();
        _client?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
