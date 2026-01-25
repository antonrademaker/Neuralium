namespace Neuralium.Worker.Agents;

/// <summary>
/// Base interface for all pipeline agents.
/// Each agent processes data and passes results to the next stage.
/// </summary>
public interface IAgent<TInput, TOutput>
{
    /// <summary>
    /// Process input data and return transformed output.
    /// </summary>
    /// <param name="input">Input data to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processed output data</returns>
    Task<TOutput> ProcessAsync(TInput input, CancellationToken cancellationToken);
}

/// <summary>
/// Agent that produces output without requiring input (e.g., Ingest from external sources)
/// </summary>
public interface ISourceAgent<TOutput>
{
    /// <summary>
    /// Generate output data from external sources.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated output data</returns>
    Task<TOutput> ProduceAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Agent that consumes input without producing output (e.g., Publish to files)
/// </summary>
public interface ISinkAgent<TInput>
{
    /// <summary>
    /// Consume input data and perform final action.
    /// </summary>
    /// <param name="input">Input data to consume</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ConsumeAsync(TInput input, CancellationToken cancellationToken);
}
