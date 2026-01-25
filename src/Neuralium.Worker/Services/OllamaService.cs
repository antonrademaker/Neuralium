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
