using System.Diagnostics;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Neuralium.Worker.Models;
using OpenAI.Chat;
using OpenAI.Embeddings;

namespace Neuralium.Worker.Services;

/// <summary>
/// Azure OpenAI implementation of the LLM service with retry logic and telemetry.
/// </summary>
public partial class AzureOpenAIService : ILlmService
{
    private readonly ILogger<AzureOpenAIService> _logger;
    private readonly LlmSettings _settings;
    private readonly AzureOpenAIClient? _client;
    private readonly ActivitySource _activitySource;

    private const int MaxRetries = 3;
    private const int InitialBackoffMs = 1000;

    public AzureOpenAIService(
        ILogger<AzureOpenAIService> logger,
        IOptions<LlmSettings> settings,
        ActivitySource activitySource)
    {
        _logger = logger;
        _settings = settings.Value;
        _activitySource = activitySource;

        if (_settings.Enabled && !string.IsNullOrEmpty(_settings.Endpoint) && !string.IsNullOrEmpty(_settings.ApiKey))
        {
            _client = new AzureOpenAIClient(new Uri(_settings.Endpoint), new AzureKeyCredential(_settings.ApiKey));
            LogClientInitialized(_settings.Endpoint, _settings.ModelName ?? "(not set)");
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

        return await ExecuteWithRetryAsync(
            async () =>
            {
                var chatClient = _client.GetChatClient(_settings.ModelName);

                var response = await chatClient.CompleteChatAsync(
                    [new UserChatMessage(prompt)],
                    new ChatCompletionOptions
                    {
                        MaxOutputTokenCount = _settings.MaxSummaryTokens,
                        Temperature = _settings.Temperature
                    },
                    cancellationToken);

                var summary = response.Value.Content[0].Text?.Trim();

                activity?.SetTag("response.tokens", response.Value.Usage.TotalTokenCount);
                LogSummaryGenerated(response.Value.Usage.TotalTokenCount);

                return summary;
            },
            "summarization",
            cancellationToken);
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

        return await ExecuteWithRetryAsync(
            async () =>
            {
                var embeddingClient = _client.GetEmbeddingClient(_settings.EmbeddingModelName);

                var response = await embeddingClient.GenerateEmbeddingAsync(content, cancellationToken: cancellationToken);

                var embedding = response.Value.ToFloats().ToArray();

                activity?.SetTag("embedding.dimensions", embedding.Length);
                LogEmbeddingGenerated(embedding.Length);

                return embedding;
            },
            "embedding",
            cancellationToken);
    }

    public async Task<List<string>?> ClassifyTopicsAsync(
        string title,
        string content,
        List<string> availableTopics,
        List<string>? keywordTopics = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("ClassifyTopics");
        activity?.SetTag("title", title);
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

        return await ExecuteWithRetryAsync(
            async () =>
            {
                var chatClient = _client.GetChatClient(_settings.ModelName);

                var response = await chatClient.CompleteChatAsync(
                    [new UserChatMessage(prompt)],
                    new ChatCompletionOptions
                    {
                        MaxOutputTokenCount = 100,
                        Temperature = 0.1f // Low temperature for more deterministic classification
                    },
                    cancellationToken);

                var responseText = response.Value.Content[0].Text?.Trim();

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

                    activity?.SetTag("response.tokens", response.Value.Usage.TotalTokenCount);
                    activity?.SetTag("classified_topics.count", validTopics.Count);
                    LogTopicsClassified(title, validTopics.Count, string.Join(", ", validTopics));

                    return validTopics;
                }
                catch (System.Text.Json.JsonException ex)
                {
                    LogClassificationFailed($"JSON parse error: {ex.Message}. Response: {responseText}");
                    return null;
                }
            },
            "classification",
            cancellationToken);
    }

    public async Task<List<string>?> ExtractEntitiesAsync(
        string title,
        string content,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("ExtractEntities");
        activity?.SetTag("title", title);

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

        return await ExecuteWithRetryAsync(
            async () =>
            {
                var chatClient = _client.GetChatClient(_settings.ModelName);

                var response = await chatClient.CompleteChatAsync(
                    [new UserChatMessage(prompt)],
                    new ChatCompletionOptions
                    {
                        MaxOutputTokenCount = 100,
                        Temperature = 0.2f
                    },
                    cancellationToken);

                var responseText = response.Value.Content[0].Text?.Trim();

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
                    LogEntitiesExtracted(title, entities.Count);

                    return entities;
                }
                catch (System.Text.Json.JsonException ex)
                {
                    LogEntityExtractionFailed($"JSON parse error: {ex.Message}. Response: {responseText}");
                    return null;
                }
            },
            "entity extraction",
            cancellationToken);
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
        foreach (var (title, summary, topics, published) in articles.Take(20)) // Limit to 20 articles
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

        return await ExecuteWithRetryAsync(
            async () =>
            {
                var chatClient = _client.GetChatClient(_settings.ModelName);

                var response = await chatClient.CompleteChatAsync(
                    [new UserChatMessage(prompt)],
                    new ChatCompletionOptions
                    {
                        MaxOutputTokenCount = 200,
                        Temperature = 0.4f
                    },
                    cancellationToken);

                var insight = response.Value.Content[0].Text?.Trim();

                if (!string.IsNullOrEmpty(insight))
                {
                    activity?.SetTag("response.tokens", response.Value.Usage.TotalTokenCount);
                    LogTrendInsightsGenerated(articles.Count);
                }

                return insight;
            },
            "trend analysis",
            cancellationToken);
    }

    private async Task<T?> ExecuteWithRetryAsync<T>(
        Func<Task<T?>> operation,
        string operationName,
        CancellationToken cancellationToken)
    {
        var attempt = 0;
        var backoffMs = InitialBackoffMs;

        while (attempt < MaxRetries)
        {
            try
            {
                return await operation();
            }
            catch (RequestFailedException ex) when (ex.Status == 429) // Rate limit
            {
                attempt++;
                if (attempt >= MaxRetries)
                {
                    LogRateLimitExceeded(operationName, MaxRetries, ex);
                    return default;
                }

                var delay = backoffMs * (int)Math.Pow(2, attempt - 1);
                LogRateLimitRetry(operationName, delay, attempt, MaxRetries);

                await Task.Delay(delay, cancellationToken);
            }
            catch (RequestFailedException ex) when (ex.Status >= 500) // Server error
            {
                attempt++;
                if (attempt >= MaxRetries)
                {
                    LogServerErrorExceeded(operationName, MaxRetries, ex);
                    return default;
                }

                var delay = backoffMs * (int)Math.Pow(2, attempt - 1);
                LogServerErrorRetry(operationName, delay, attempt, MaxRetries, ex);

                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                LogOperationCancelled(operationName);
                throw;
            }
            catch (Exception ex)
            {
                LogUnexpectedError(operationName, ex);
                return default;
            }
        }

        return default;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Azure OpenAI client initialized: Endpoint={Endpoint}, Model={Model}")]
    private partial void LogClientInitialized(string endpoint, string model);

    [LoggerMessage(Level = LogLevel.Information, Message = "Azure OpenAI service disabled or not configured")]
    private partial void LogServiceDisabled();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Summarization disabled via configuration")]
    private partial void LogSummarizationDisabled();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Embeddings disabled via configuration")]
    private partial void LogEmbeddingsDisabled();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cannot generate {Type}: Azure OpenAI client not initialized")]
    private partial void LogClientNotInitialized(string type);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cannot generate embedding: EmbeddingModelName not configured")]
    private partial void LogEmbeddingModelNotConfigured();

    [LoggerMessage(Level = LogLevel.Information, Message = "Generated summary: {Tokens} tokens used")]
    private partial void LogSummaryGenerated(int tokens);

    [LoggerMessage(Level = LogLevel.Information, Message = "Generated embedding: {Dimensions} dimensions")]
    private partial void LogEmbeddingGenerated(int dimensions);

    [LoggerMessage(Level = LogLevel.Error, Message = "LLM {Operation} failed after {Attempts} attempts due to rate limiting")]
    private partial void LogRateLimitExceeded(string operation, int attempts, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "LLM {Operation} rate limited, retrying in {Delay}ms (attempt {Attempt}/{Max})")]
    private partial void LogRateLimitRetry(string operation, int delay, int attempt, int max);

    [LoggerMessage(Level = LogLevel.Error, Message = "LLM {Operation} failed after {Attempts} attempts due to server error")]
    private partial void LogServerErrorExceeded(string operation, int attempts, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "LLM {Operation} server error, retrying in {Delay}ms (attempt {Attempt}/{Max})")]
    private partial void LogServerErrorRetry(string operation, int delay, int attempt, int max, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "LLM {Operation} cancelled")]
    private partial void LogOperationCancelled(string operation);

    [LoggerMessage(Level = LogLevel.Error, Message = "LLM {Operation} failed with unexpected error")]
    private partial void LogUnexpectedError(string operation, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Classification disabled via configuration")]
    private partial void LogClassificationDisabled();

    [LoggerMessage(Level = LogLevel.Information, Message = "Classified '{Title}' into {Count} topics: {Topics}")]
    private partial void LogTopicsClassified(string title, int count, string topics);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Classification failed: {Reason}")]
    private partial void LogClassificationFailed(string reason);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Trend analysis disabled via configuration")]
    private partial void LogTrendAnalysisDisabled();

    [LoggerMessage(Level = LogLevel.Information, Message = "Extracted {Count} entities from '{Title}'")]
    private partial void LogEntitiesExtracted(string title, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Entity extraction failed: {Reason}")]
    private partial void LogEntityExtractionFailed(string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Generated trend insights for {Count} articles")]
    private partial void LogTrendInsightsGenerated(int count);
}
