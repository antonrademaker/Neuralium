using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Neuralium.Data;
using Neuralium.Data.Models;
using Neuralium.Worker.Agents;
using Neuralium.Worker.Models;

namespace Neuralium.Tests.Agents;

public class DedupeAgentTests : IDisposable
{
    private readonly Mock<ILogger<DedupeAgent>> _loggerMock;
    private readonly NeuraliumDbContext _dbContext;

    public DedupeAgentTests()
    {
        _loggerMock = new Mock<ILogger<DedupeAgent>>();
        
        var options = new DbContextOptionsBuilder<NeuraliumDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NeuraliumDbContext(options);
    }

    [Fact]
    public async Task ProcessAsync_WithDuplicateUrlHashes_RemovesDuplicates()
    {
        // Arrange
        var agent = new DedupeAgent(_loggerMock.Object, _dbContext);
        var context = new PipelineContext();
        
        var urlHash = "abc123hash";
        context.NormalizedItems.Add(new NewsItem
        {
            Title = "Article 1",
            Url = "https://example.com/article",
            Source = "test-source",
            UrlHash = urlHash,
            PublishedAtUtc = DateTime.UtcNow.AddHours(-2)
        });
        context.NormalizedItems.Add(new NewsItem
        {
            Title = "Article 1 - Duplicate",
            Url = "https://example.com/article",
            Source = "test-source",
            UrlHash = urlHash,
            PublishedAtUtc = DateTime.UtcNow.AddHours(-1)
        });

        // Act
        var result = await agent.ProcessAsync(context, CancellationToken.None);

        // Assert
        result.UniqueItems.Should().HaveCount(1);
        result.UniqueItems[0].Title.Should().Be("Article 1");
    }

    [Fact]
    public async Task ProcessAsync_WithUniqueItems_KeepsAllItems()
    {
        // Arrange
        var agent = new DedupeAgent(_loggerMock.Object, _dbContext);
        var context = new PipelineContext();
        
        context.NormalizedItems.Add(new NewsItem
        {
            Title = "Unique Article 1",
            Url = "https://example.com/article1",
            Source = "test-source",
            UrlHash = "hash1",
            PublishedAtUtc = DateTime.UtcNow
        });
        context.NormalizedItems.Add(new NewsItem
        {
            Title = "Unique Article 2",
            Url = "https://example.com/article2",
            Source = "test-source",
            UrlHash = "hash2",
            PublishedAtUtc = DateTime.UtcNow
        });

        // Act
        var result = await agent.ProcessAsync(context, CancellationToken.None);

        // Assert
        result.UniqueItems.Should().HaveCount(2);
    }

    // TODO: Add tests for database deduplication
    // - Test that items already in DB are filtered out
    // - Test that URL hash comparison is case-sensitive
    // - Test handling of items with null/empty hash

    // TODO: Add tests for edge cases
    // - Empty input list
    // - Items with missing required fields

    // TODO: Add performance tests
    // - Large batches (1000+ items)
    // - Database query optimization

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
