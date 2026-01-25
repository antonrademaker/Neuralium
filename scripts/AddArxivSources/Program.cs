// Simple utility to add arXiv sources to the database
// Run with: dotnet run --project scripts/AddArxivSources.csproj

using Microsoft.EntityFrameworkCore;
using Neuralium.Data;
using Neuralium.Data.Models;

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__neuralium")
    ?? "Host=localhost;Port=61853;Database=neuralium;Username=postgres;Password=DevelopmentPassword123!";

Console.WriteLine("Adding arXiv sources to Neuralium database...");
Console.WriteLine($"Connection: {connectionString.Replace("Password=DevelopmentPassword123!", "Password=***")}");

var optionsBuilder = new DbContextOptionsBuilder<NeuraliumDbContext>();
optionsBuilder.UseNpgsql(connectionString);

using var dbContext = new NeuraliumDbContext(optionsBuilder.Options);

// Check if arXiv sources already exist
var existingArxivCount = await dbContext.FeedSources
    .Where(s => s.Type == "Arxiv")
    .CountAsync();

if (existingArxivCount > 0)
{
    Console.WriteLine($"✓ Found {existingArxivCount} existing arXiv source(s). Skipping insert.");
}
else
{
    var arxivSources = new[]
    {
        new FeedSource
        {
            Name = "arXiv AI Research",
            Type = "Arxiv",
            Url = "cat:cs.AI OR cat:cs.LG OR cat:cs.CL",
            Enabled = true,
            TimeoutSeconds = 45,
            MinimumFetchIntervalMinutes = 240,
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

    await dbContext.FeedSources.AddRangeAsync(arxivSources);
    await dbContext.SaveChangesAsync();

    Console.WriteLine($"✓ Successfully added {arxivSources.Length} arXiv sources");
}

// Show all arXiv sources
Console.WriteLine("\narXiv sources in database:");
var arxivSourcesList = await dbContext.FeedSources
    .Where(s => s.Type == "Arxiv")
    .OrderBy(s => s.Id)
    .ToListAsync();

foreach (var source in arxivSourcesList)
{
    Console.WriteLine($"  [{source.Id}] {source.Name}: {source.Url}");
    Console.WriteLine($"      Enabled: {source.Enabled}, Min Interval: {source.MinimumFetchIntervalMinutes}min");
}

Console.WriteLine("\n✓ Done! Run the worker to test arXiv integration.");
