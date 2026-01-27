using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Neuralium.Data;
using Neuralium.Data.Models;
using Neuralium.Worker.Agents;
using Neuralium.Worker.Models;
using Neuralium.Worker.Services;

namespace Neuralium.Tests.Agents;

public class EnrichAgentTests : IDisposable
{
    private readonly Mock<ILogger<EnrichAgent>> _loggerMock;
    private readonly Mock<ILlmService> _llmServiceMock;
    private readonly IOptions<LlmSettings> _llmSettings;
    private readonly NeuraliumDbContext _dbContext;

    public EnrichAgentTests()
    {
        _loggerMock = new Mock<ILogger<EnrichAgent>>();
        _llmServiceMock = new Mock<ILlmService>();

        _llmSettings = Options.Create(new LlmSettings
        {
            Enabled = true,
            EnableSummarization = true,
            EnableEmbeddings = true,
            Provider = "Mock",
            ModelName = "test-model",
            Temperature = 0.3f
        });

        var options = new DbContextOptionsBuilder<NeuraliumDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NeuraliumDbContext(options);
    }

    [Fact]
    public async Task ProcessAsync_WithValidItems_GeneratesSummariesAndEmbeddings()
    {
        // Arrange
        var agent = new EnrichAgent(_loggerMock.Object, _llmServiceMock.Object, _llmSettings, _dbContext, Mock.Of<IArticleFetcherService>());

        var embeddingData = new float[] { 0.1f, 0.2f, 0.3f };
        _llmServiceMock
            .Setup(x => x.GenerateSummaryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Generated summary");

        _llmServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddingData);

        var context = new PipelineContext();
        context.ClassifiedItems.Add(new NewsItem
        {
            Title = "Test Article",
            Url = "https://example.com/test",
            Source = "test-source",
            UrlHash = "test-hash",
            RawContent = "Test content for summarization",
            PublishedAtUtc = DateTime.UtcNow
        });

        // Act
        var result = await agent.ProcessAsync(context, CancellationToken.None);

        // Assert
        result.ClassifiedItems.Should().HaveCount(1);
        result.ClassifiedItems[0].Summary.Should().Be("Generated summary");
        result.ClassifiedItems[0].EmbeddingJson.Should().NotBeNullOrEmpty();
        _llmServiceMock.Verify(x => x.GenerateSummaryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _llmServiceMock.Verify(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WithLlmDisabled_SkipsEnrichment()
    {
        // Arrange
        var disabledSettings = Options.Create(new LlmSettings { Enabled = false });
        var agent = new EnrichAgent(_loggerMock.Object, _llmServiceMock.Object, disabledSettings, _dbContext, Mock.Of<IArticleFetcherService>());

        var context = new PipelineContext();
        context.ClassifiedItems.Add(new NewsItem
        {
            Title = "Test Article",
            Url = "https://example.com/test",
            Source = "test-source",
            UrlHash = "test-hash",
            RawContent = "Test content",
            PublishedAtUtc = DateTime.UtcNow
        });

        // Act
        var result = await agent.ProcessAsync(context, CancellationToken.None);

        // Assert
        result.ClassifiedItems[0].Summary.Should().BeNull();
        result.ClassifiedItems[0].EmbeddingJson.Should().BeNull();
        _llmServiceMock.Verify(x => x.GenerateSummaryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _llmServiceMock.Verify(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WithExistingSummary_SkipsSummaryGeneration()
    {
        // Arrange
        var agent = new EnrichAgent(_loggerMock.Object, _llmServiceMock.Object, _llmSettings, _dbContext, Mock.Of<IArticleFetcherService>());

        var embeddingData = new float[] { 0.1f, 0.2f };
        _llmServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddingData);

        var context = new PipelineContext();
        context.ClassifiedItems.Add(new NewsItem
        {
            Title = "Test Article",
            Url = "https://example.com/test",
            Source = "test-source",
            UrlHash = "test-hash",
            Summary = "Existing summary",
            RawContent = "Test content",
            PublishedAtUtc = DateTime.UtcNow
        });

        // Act
        var result = await agent.ProcessAsync(context, CancellationToken.None);

        // Assert
        result.ClassifiedItems[0].Summary.Should().Be("Existing summary");
        _llmServiceMock.Verify(x => x.GenerateSummaryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _llmServiceMock.Verify(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // TODO: Add tests for LLM service failures and error handling

    // TODO: Add tests for backfill functionality
    // - Test that items missing summaries in DB are processed
    // - Test batch size limiting (100 items max)
    // - Test backfill with both missing summaries and embeddings
    // - Test backfill error handling

    // TODO: Add tests for metrics
    // - Verify ObservableGauge callbacks return correct counts
    // - Verify Counter increments on successful operations
    // - Verify error counter increments on failures

    // TODO: Add integration tests
    // - Test with real database (not in-memory)
    // - Test concurrent processing scenarios
    // - Test cancellation token handling

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
