using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Neuralium.Data;
using Neuralium.Data.Models;
using Neuralium.Worker.Agents;
using Neuralium.Worker.Models;

namespace Neuralium.Tests.Agents;

public class ClassifyAgentTests : IDisposable
{
    private readonly Mock<ILogger<ClassifyAgent>> _loggerMock;
    private readonly NeuraliumDbContext _dbContext;

    public ClassifyAgentTests()
    {
        _loggerMock = new Mock<ILogger<ClassifyAgent>>();
        
        var options = new DbContextOptionsBuilder<NeuraliumDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NeuraliumDbContext(options);
    }

    [Fact(Skip = "EF Core InMemory doesn't support GroupBy in ClassifyAgent query")]
    public async Task ProcessAsync_WithMatchingKeywords_AssignsTopics()
    {
        // Arrange
        await SeedTopicKeywords();
        var agent = new ClassifyAgent(_loggerMock.Object, _dbContext);
        
        var context = new PipelineContext();
        context.UniqueItems.Add(new NewsItem
        {
            Title = "New AI Model Improves Machine Learning Performance",
            RawContent = "Recent advances in deep learning have led to better neural networks...",
            Url = "https://example.com/ai-news",
            Source = "test-source",
            UrlHash = "test-hash",
            PublishedAtUtc = DateTime.UtcNow
        });

        // Act
        var result = await agent.ProcessAsync(context, CancellationToken.None);

        // Assert
        result.ClassifiedItems.Should().HaveCount(1);
        var topics = JsonSerializer.Deserialize<List<string>>(result.ClassifiedItems[0].TopicsJson ?? "[]");
        topics.Should().Contain("ai");
        topics.Should().Contain("machine-learning");
    }

    [Fact(Skip = "EF Core InMemory doesn't support GroupBy in ClassifyAgent query")]
    public async Task ProcessAsync_WithUniqueItems_ClassifiesAll()
    {
        // Arrange
        await SeedTopicKeywords();
        var agent = new ClassifyAgent(_loggerMock.Object, _dbContext);
        
        var context = new PipelineContext();
        context.UniqueItems.Add(new NewsItem
        {
            Title = "Article about unrelated topic",
            RawContent = "Content about something else",
            Url = "https://example.com/random",
            Source = "test-source",
            UrlHash = "test-hash",
            PublishedAtUtc = DateTime.UtcNow
        });

        // Act
        var result = await agent.ProcessAsync(context, CancellationToken.None);

        // Assert  
        result.ClassifiedItems.Should().HaveCount(1);
    }

    private static readonly string[] s_aiKeywords = [ "ai", "artificial intelligence", "neural network" ];
    private static readonly string[] s_mlKeywords = [ "machine learning", "ml", "model training" ];
    private static readonly string[] s_blockchainKeywords = [ "blockchain", "distributed ledger" ];
    private static readonly string[] s_cloudKeywords = [ "cloud computing", "aws", "azure" ];

    private async Task SeedTopicKeywords()
    {
        foreach (var keyword in s_aiKeywords)
        {
            _dbContext.TopicKeywords.Add(new TopicKeyword { Topic = "ai", Keyword = keyword, Enabled = true });
        }
        foreach (var keyword in s_mlKeywords)
        {
            _dbContext.TopicKeywords.Add(new TopicKeyword { Topic = "machine-learning", Keyword = keyword, Enabled = true });
        }
        foreach (var keyword in s_blockchainKeywords)
        {
            _dbContext.TopicKeywords.Add(new TopicKeyword { Topic = "blockchain", Keyword = keyword, Enabled = true });
        }
        foreach (var keyword in s_cloudKeywords)
        {
            _dbContext.TopicKeywords.Add(new TopicKeyword { Topic = "cloud", Keyword = keyword, Enabled = true });
        }
        await _dbContext.SaveChangesAsync();
    }

    // TODO: Add tests for case sensitivity
    // - Verify keywords match regardless of case (AI, ai, Ai)
    
    // TODO: Add tests for keyword priority/weighting
    // - Test scoring when multiple keywords match
    // - Test minimum confidence threshold
    
    // TODO: Add tests for LLM-based classification (future)
    // - Mock LLM service responses
    // - Test fallback to keyword-based when LLM unavailable
    // - Test hybrid approach (keywords + LLM)
    
    // TODO: Add performance tests
    // - Large batches of items (1000+)
    // - Many topics/keywords (100+)

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
