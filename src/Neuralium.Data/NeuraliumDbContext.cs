using Microsoft.EntityFrameworkCore;
using Neuralium.Data.Models;

namespace Neuralium.Data;

/// <summary>
/// EF Core DbContext for the Neuralium AI news agent.
/// Manages news items, classifications, and trend analysis.
/// </summary>
public class NeuraliumDbContext : DbContext
{
    public NeuraliumDbContext(DbContextOptions<NeuraliumDbContext> options)
        : base(options)
    {
    }

    public DbSet<NewsItem> NewsItems => Set<NewsItem>();
    public DbSet<FeedSource> FeedSources => Set<FeedSource>();
    public DbSet<TopicKeyword> TopicKeywords => Set<TopicKeyword>();
    public DbSet<NewsItemFeedback> NewsItemFeedbacks => Set<NewsItemFeedback>();
    public DbSet<KeywordFeedback> KeywordFeedbacks => Set<KeywordFeedback>();
    public DbSet<NewsItemScore> NewsItemScores => Set<NewsItemScore>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<NewsItem>(entity =>
        {
            entity.ToTable("news_items");
            entity.HasKey(e => e.Id);

            // Indexes for common queries
            entity.HasIndex(e => e.UrlHash).IsUnique(); // Deduplication
            entity.HasIndex(e => e.PublishedAtUtc); // Time-based queries
            entity.HasIndex(e => e.Source); // Filter by source
            entity.HasIndex(e => e.IsPublished); // Published items only
            entity.HasIndex(e => e.TrendScore); // Top trending items

            // Property configurations
            entity.Property(e => e.Source).HasMaxLength(200);
            entity.Property(e => e.Title).HasMaxLength(500);
            entity.Property(e => e.Url).HasMaxLength(2000);
            entity.Property(e => e.UrlHash).HasMaxLength(64).IsFixedLength();
            // Summary and RawContent have no length constraints (use text type)
            entity.Property(e => e.TopicsJson).HasMaxLength(2000);
            entity.Property(e => e.EntitiesJson).HasMaxLength(5000);
            entity.Property(e => e.PublishedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(e => e.IngestedAtUtc).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<FeedSource>(entity =>
        {
            entity.ToTable("feed_sources");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Enabled);
            entity.Property(e => e.CreatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(e => e.LastFetchedAtUtc).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<TopicKeyword>(entity =>
        {
            entity.ToTable("topic_keywords");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Topic);
            entity.HasIndex(e => e.Enabled);
            entity.Property(e => e.CreatedAtUtc).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<NewsItemFeedback>(entity =>
        {
            entity.ToTable("news_item_feedbacks");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.NewsItemId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.NewsItemId }).IsUnique(); // One feedback per user per item
            entity.Property(e => e.CreatedAtUtc).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<KeywordFeedback>(entity =>
        {
            entity.ToTable("keyword_feedbacks");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TopicKeywordId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.TopicKeywordId }).IsUnique(); // One feedback per user per keyword
            entity.Property(e => e.CreatedAtUtc).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<NewsItemScore>(entity =>
        {
            entity.ToTable("news_item_scores");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.NewsItemId).IsUnique(); // One score per news item
            entity.HasIndex(e => e.ComputedTrendScore); // Query by score
            entity.HasIndex(e => e.CalculatedAtUtc); // Query by calculation time
            entity.Property(e => e.CalculatedAtUtc).HasColumnType("timestamp with time zone");
        });
    }
}
