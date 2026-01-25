using System.Diagnostics;
using Neuralium.Worker.Models;

namespace Neuralium.Worker.Agents;

/// <summary>
/// Enriches news items with summaries, entities, and embeddings.
/// Optional LLM-based summarization and entity extraction (not yet implemented).
/// Currently passes through all items unchanged.
/// </summary>
public partial class EnrichAgent(ILogger<EnrichAgent> logger) : IAgent<PipelineContext, PipelineContext>
{
    private static readonly ActivitySource s_activitySource = new("Neuralium.Worker.EnrichAgent");
    public async Task<PipelineContext> ProcessAsync(PipelineContext input, CancellationToken cancellationToken)
    {
        using var activity = s_activitySource.StartActivity("Enrich items");
        LogEnrichStarting(input.ClassifiedItems.Count);

        // Note: LLM-based enrichment can be added here in the future:
        // - Generate summaries using OpenAI/Azure OpenAI
        // - Extract named entities (companies, products, people)
        // - Generate embeddings for semantic search

        await Task.CompletedTask;

        input.Metadata.StageItemCounts["Enrich"] = input.ClassifiedItems.Count;
        LogEnrichCompleted(input.ClassifiedItems.Count);

        return input;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Enrich: Processing {Count} classified items... (LLM enrichment not yet implemented)")]
    private partial void LogEnrichStarting(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Enrich: Completed - passed through {Count} items")]
    private partial void LogEnrichCompleted(int count);
}
