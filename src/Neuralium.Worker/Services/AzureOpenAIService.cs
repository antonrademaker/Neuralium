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
}
