using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Neuralium.Data;
using Neuralium.Data.Models;
using OpenTelemetry.Trace;

namespace Neuralium.MigrationService;

/// <summary>
/// Background worker that applies EF Core migrations and seeds initial data.
/// Runs once at startup and then stops the application.
/// </summary>
public partial class Worker(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime hostApplicationLifetime,
    ILogger<Worker> logger) : BackgroundService
{
    public const string ActivitySourceName = "Migrations";
    private static readonly ActivitySource s_activitySource = new(ActivitySourceName);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var activity = s_activitySource.StartActivity("Migrating database", ActivityKind.Client);

        try
        {
            LogStarting();

            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NeuraliumDbContext>();

            await RunMigrationAsync(dbContext, stoppingToken);
            await SeedDataAsync(dbContext, stoppingToken);

            LogCompleted();
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            LogFailed(ex);
            throw;
        }

        hostApplicationLifetime.StopApplication();
    }

    private async Task RunMigrationAsync(NeuraliumDbContext dbContext, CancellationToken stoppingToken)
    {
        LogApplyingMigrations();

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            // Run migration in a transaction to avoid partial migration if it fails
            await dbContext.Database.MigrateAsync(stoppingToken);
        });

        LogMigrationsApplied();
    }

    private async Task SeedDataAsync(NeuraliumDbContext dbContext, CancellationToken stoppingToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(stoppingToken);

            // Seed feed sources if none exist
            if (!await dbContext.FeedSources.AnyAsync(stoppingToken))
            {
                LogSeeding();

                var feeds = new[]
                {
                    new FeedSource
                    {
                        Name = "TechCrunch",
                        Type = "Rss",
                        Url = "https://techcrunch.com/feed/",
                        Enabled = true,
                        TimeoutSeconds = 30,
                        CreatedAtUtc = DateTime.UtcNow
                    },
                    new FeedSource
                    {
                        Name = "Hacker News",
                        Type = "Rss",
                        Url = "https://news.ycombinator.com/rss",
                        Enabled = true,
                        TimeoutSeconds = 30,
                        CreatedAtUtc = DateTime.UtcNow
                    },
                    new FeedSource
                    {
                        Name = ".NET Blog",
                        Type = "Rss",
                        Url = "https://devblogs.microsoft.com/dotnet/feed/",
                        Enabled = true,
                        TimeoutSeconds = 30,
                        CreatedAtUtc = DateTime.UtcNow
                    },
                    new FeedSource
                    {
                        Name = "GitHub dotnet/aspire releases",
                        Type = "Atom",
                        Url = "https://github.com/dotnet/aspire/releases.atom",
                        Enabled = true,
                        TimeoutSeconds = 30,
                        CreatedAtUtc = DateTime.UtcNow
                    },
                    new FeedSource
                    {
                        Name = "arXiv AI Research",
                        Type = "Arxiv",
                        Url = "cat:cs.AI OR cat:cs.LG OR cat:cs.CL",
                        Enabled = true,
                        TimeoutSeconds = 45,
                        MinimumFetchIntervalMinutes = 240, // 4 hours - arXiv updates daily
                        CreatedAtUtc = DateTime.UtcNow
                    },
                    new FeedSource
                    {
                        Name = "arXiv Neural Networks",
                        Type = "Arxiv",
                        Url = "all:neural network OR all:deep learning",
                        Enabled = true,
                        TimeoutSeconds = 45,
                        MinimumFetchIntervalMinutes = 240,
                        CreatedAtUtc = DateTime.UtcNow
                    },
                    new FeedSource
                    {
                        Name = "arXiv Large Language Models",
                        Type = "Arxiv",
                        Url = "all:large language model OR all:LLM OR all:transformer",
                        Enabled = true,
                        TimeoutSeconds = 45,
                        MinimumFetchIntervalMinutes = 240,
                        CreatedAtUtc = DateTime.UtcNow
                    }
                };

                await dbContext.FeedSources.AddRangeAsync(feeds, stoppingToken);
                LogSeedComplete("feed sources", feeds.Length);
            }

            // Seed topic keywords if none exist
            if (!await dbContext.TopicKeywords.AnyAsync(stoppingToken))
            {
                var now = DateTime.UtcNow;
                var keywords = new List<TopicKeyword>
                {
                    // AI
                    new() { Topic = "AI", Keyword = "AI", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "AI", Keyword = "artificial intelligence", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "AI", Keyword = "machine learning", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "AI", Keyword = "ML", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "AI", Keyword = "deep learning", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "AI", Keyword = "neural network", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "AI", Keyword = "LLM", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "AI", Keyword = "GPT", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "AI", Keyword = "ChatGPT", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "AI", Keyword = "Claude", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "AI", Keyword = "Gemini", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "AI", Keyword = "OpenAI", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "AI", Keyword = "transformer", Enabled = true, CreatedAtUtc = now },

                    // .NET
                    new() { Topic = ".NET", Keyword = ".NET", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = ".NET", Keyword = "dotnet", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = ".NET", Keyword = "C#", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = ".NET", Keyword = "csharp", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = ".NET", Keyword = "ASP.NET", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = ".NET", Keyword = "Blazor", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = ".NET", Keyword = "MAUI", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = ".NET", Keyword = "Entity Framework", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = ".NET", Keyword = "EF Core", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = ".NET", Keyword = "Roslyn", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = ".NET", Keyword = "Visual Studio", Enabled = true, CreatedAtUtc = now },

                    // Azure
                    new() { Topic = "Azure", Keyword = "Azure", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Azure", Keyword = "Microsoft Cloud", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Azure", Keyword = "Azure Functions", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Azure", Keyword = "App Service", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Azure", Keyword = "Cosmos DB", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Azure", Keyword = "Azure SQL", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Azure", Keyword = "AKS", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Azure", Keyword = "Azure DevOps", Enabled = true, CreatedAtUtc = now },

                    // Cloud
                    new() { Topic = "Cloud", Keyword = "cloud", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Cloud", Keyword = "AWS", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Cloud", Keyword = "GCP", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Cloud", Keyword = "Google Cloud", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Cloud", Keyword = "serverless", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Cloud", Keyword = "container", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Cloud", Keyword = "Kubernetes", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Cloud", Keyword = "Docker", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Cloud", Keyword = "microservices", Enabled = true, CreatedAtUtc = now },

                    // DevOps
                    new() { Topic = "DevOps", Keyword = "DevOps", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "DevOps", Keyword = "CI/CD", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "DevOps", Keyword = "GitHub Actions", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "DevOps", Keyword = "GitLab CI", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "DevOps", Keyword = "Jenkins", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "DevOps", Keyword = "deployment", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "DevOps", Keyword = "pipeline", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "DevOps", Keyword = "infrastructure as code", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "DevOps", Keyword = "IaC", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "DevOps", Keyword = "Terraform", Enabled = true, CreatedAtUtc = now },

                    // Web
                    new() { Topic = "Web", Keyword = "web development", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Web", Keyword = "React", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Web", Keyword = "Vue", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Web", Keyword = "Angular", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Web", Keyword = "Next.js", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Web", Keyword = "TypeScript", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Web", Keyword = "JavaScript", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Web", Keyword = "HTML", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Web", Keyword = "CSS", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Web", Keyword = "frontend", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Web", Keyword = "backend", Enabled = true, CreatedAtUtc = now },

                    // Database
                    new() { Topic = "Database", Keyword = "database", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Database", Keyword = "SQL", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Database", Keyword = "PostgreSQL", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Database", Keyword = "MySQL", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Database", Keyword = "MongoDB", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Database", Keyword = "Redis", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Database", Keyword = "Elasticsearch", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Database", Keyword = "data warehouse", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Database", Keyword = "analytics", Enabled = true, CreatedAtUtc = now },

                    // Security
                    new() { Topic = "Security", Keyword = "security", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Security", Keyword = "cybersecurity", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Security", Keyword = "vulnerability", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Security", Keyword = "exploit", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Security", Keyword = "patch", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Security", Keyword = "authentication", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Security", Keyword = "authorization", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Security", Keyword = "encryption", Enabled = true, CreatedAtUtc = now },
                    new() { Topic = "Security", Keyword = "zero trust", Enabled = true, CreatedAtUtc = now }
                };

                await dbContext.TopicKeywords.AddRangeAsync(keywords, stoppingToken);
                LogSeedComplete("topic keywords", keywords.Count);
            }

            // Seed sample news item if none exist
            if (!await dbContext.NewsItems.AnyAsync(stoppingToken))
            {
                var seedItem = new NewsItem
                {
                    Source = "System",
                    Title = "Neuralium AI News Agent Initialized",
                    Url = "https://github.com/Neuralium",
                    UrlHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                    PublishedAtUtc = DateTime.UtcNow,
                    IngestedAtUtc = DateTime.UtcNow,
                    Summary = "The Neuralium AI news aggregation and trend analysis agent is now operational.",
                    RawContent = "Initial seed data created during database migration.",
                    TopicsJson = "[\"System\", \"Initialization\"]",
                    IsPublished = false
                };

                await dbContext.NewsItems.AddAsync(seedItem, stoppingToken);
                LogSeedComplete("news items", 1);
            }

            await dbContext.SaveChangesAsync(stoppingToken);
            await transaction.CommitAsync(stoppingToken);
        });
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting database migration...")]
    private partial void LogStarting();

    [LoggerMessage(Level = LogLevel.Information, Message = "Database migration completed successfully")]
    private partial void LogCompleted();

    [LoggerMessage(Level = LogLevel.Error, Message = "Database migration failed")]
    private partial void LogFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying EF Core migrations...")]
    private partial void LogApplyingMigrations();

    [LoggerMessage(Level = LogLevel.Information, Message = "Migrations applied successfully")]
    private partial void LogMigrationsApplied();

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeding initial data...")]
    private partial void LogSeeding();

    [LoggerMessage(Level = LogLevel.Information, Message = "Seed data created successfully: {Count} {Type}")]
    private partial void LogSeedComplete(string type, int count);
}
