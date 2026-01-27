# Article Content Fetching Implementation

## Problem Statement

Hacker News RSS feeds provide minimal content - often just a link to comments instead of the actual article content. This results in poor summaries like:

```
<a href="https://news.ycombinator.com/item?id=46781707">Comments</a>
```

## Solution Overview

Implemented an **ArticleFetcherService** that:
1. Detects when RSS content is insufficient (link-only or very short)
2. Fetches the full article from the original URL
3. Extracts readable content using HtmlAgilityPack
4. Provides clean text for LLM summarization

## Architecture

### New Components

#### IArticleFetcherService
```csharp
Task<string?> FetchArticleContentAsync(string url, CancellationToken cancellationToken);
bool IsInsufficientContent(string? content);
```

#### ArticleFetcherService
- Uses HtmlAgilityPack for HTML parsing
- Heuristic-based content extraction (looks for `<article>`, `.article-content`, `<main>`, etc.)
- Handles paywalled domains (skips wsj.com, ft.com, etc.)
- 30-second timeout with error handling
- Limits content to ~2000 chars for LLM efficiency

### Integration with EnrichAgent

The EnrichAgent now:
1. Checks if `RawContent` is insufficient before summarization
2. If yes, fetches full article content from URL
3. Uses fetched content (or falls back to original) for summarization
4. Works for both new items and backfill processing

### Key Code Changes

**EnrichAgent.cs** (lines ~135-153):
```csharp
// Check if content is insufficient and try to fetch full article
var contentToSummarize = item.RawContent;
if (_articleFetcher.IsInsufficientContent(item.RawContent))
{
    LogFetchingFullArticle(item.Title);
    var fetchedContent = await _articleFetcher.FetchArticleContentAsync(item.Url, cancellationToken);
    if (!string.IsNullOrWhiteSpace(fetchedContent))
    {
        contentToSummarize = fetchedContent;
        LogFetchedArticleContent(item.Title, fetchedContent.Length);
    }
}

var content = $"{item.Title}\n\n{contentToSummarize ?? string.Empty}".Trim();
```

**Program.cs**:
```csharp
builder.Services.AddScoped<IArticleFetcherService, ArticleFetcherService>();
builder.Services.AddHttpClient("ArticleFetcher")
    .AddStandardResilienceHandler();
```

## Key Patterns from Microsoft Article

Based on [Microsoft Agent Framework: Using Background Responses](https://jamiemaguire.net/index.php/2026/01/24/microsoft-agent-framework-using-background-responses-to-create-an-ai-researcher-and-newsletter-publisher/):

### ✅ 1. **Background Processing Pattern**
- Long-running operations (article fetching + LLM) handled asynchronously
- Worker service already implements this via BackgroundService
- Pipeline processes batches without blocking

### ✅ 2. **Progress/Status Updates**
- ArticleFetcherService logs each fetch operation
- EnrichAgent logs when fetching articles
- OllamaService logs token generation
- All visible in Aspire dashboard console logs

### ✅ 3. **Tool/Function Pattern**
- Each service has a single responsibility:
  - `IArticleFetcherService` → fetch web content
  - `ILlmService` → generate summaries/embeddings
  - `IFeedProvider` → fetch feeds
- Services inject HttpClient via factory for resilience

### ✅ 4. **Explicit Instructions & Quality Checks**
**Content Validation**:
```csharp
// Check minimum length
if (strippedContent.Length < 150) return true;

// Detect link-only content
var htmlLinkPattern = @"^<a\s+href=[""'].*?[""']>.*?</a>$";
if (Regex.IsMatch(content, htmlLinkPattern)) return true;
```

**Extraction Heuristics**:
- Try multiple selectors (`<article>`, `.article-content`, `<main>`)
- Remove non-content elements (scripts, nav, footer)
- Limit output for LLM token efficiency

### ✅ 5. **Multi-Source Aggregation**
- StandardFeedProvider handles RSS/Atom feeds
- ArxivFeedProvider handles academic papers
- ArticleFetcherService handles web scraping
- All use same `IFeedProvider` abstraction

### ✅ 6. **Error Handling & Resilience**
```csharp
// Skip known paywalls
if (SkipDomains.Any(d => uri.Host.Contains(d))) return null;

// Handle timeouts gracefully
httpClient.Timeout = TimeSpan.FromSeconds(30);

// Continue processing on failure
catch (Exception ex) {
    LogFetchError(url, ex.Message);
    return null; // Graceful degradation
}
```

### ✅ 7. **Observability**
```csharp
using var activity = s_activitySource.StartActivity("Fetch article content");
activity?.SetTag("url", url);
activity?.SetTag("content.length", extractedContent.Length);
activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
```

## Testing

Unit tests updated to mock `IArticleFetcherService`:
```csharp
var agent = new EnrichAgent(
    _loggerMock.Object, 
    _llmServiceMock.Object, 
    _llmSettings, 
    _dbContext, 
    Mock.Of<IArticleFetcherService>()  // Add mock
);
```

## Dependencies Added

### Directory.Packages.props
```xml
<PackageVersion Include="HtmlAgilityPack" Version="1.11.71" />
```

### Neuralium.Worker.csproj
```xml
<PackageReference Include="HtmlAgilityPack" />
```

## Usage Example

When the worker processes a Hacker News item:

1. **Before** (insufficient content):
   ```
   Title: "Amazon to Shut Down All Amazon Go Stores"
   Content: "<a href='https://news.ycombinator.com/item?id=46781707'>Comments</a>"
   ```

2. **After** (article fetched):
   ```
   Title: "Amazon to Shut Down All Amazon Go Stores"
   Content: "Amazon announced today it will close all Amazon Go and Fresh stores...
            [2000 chars of actual article content]..."
   Summary: "Amazon is closing its physical Amazon Go and Fresh stores to focus on..."
   ```

## Configuration

No additional configuration needed. Service works automatically when:
- LLM is enabled (`Llm:Enabled: true`)
- Summarization is enabled (`Llm:EnableSummmarization: true`)
- RSS content is < 150 chars or link-only

## Performance Considerations

1. **Rate Limiting**: Respects HTTP 429 responses via Polly resilience handler
2. **Timeout**: 30-second max per article fetch
3. **Content Limits**: Extracts max 2000 chars to avoid overwhelming LLM
4. **Batch Processing**: Maintains 10-item batches for stability
5. **Paywall Skip**: Avoids wasting time on inaccessible content

## Future Enhancements

Based on Microsoft article patterns:

1. **Continuation Tokens**: If article fetching takes very long, implement polling pattern
2. **User Feedback**: Track which articles fail extraction for feed quality metrics
3. **Cache Layer**: Store fetched articles to avoid re-fetching same URLs
4. **Content Quality Scoring**: Rate extraction quality and filter low-quality results
5. **Alternative Extractors**: Try multiple libraries (Readability.NET, Mercury, etc.) if HtmlAgilityPack fails

## References

- [Microsoft Agent Framework Background Responses](https://jamiemaguire.net/index.php/2026/01/24/microsoft-agent-framework-using-background-responses-to-create-an-ai-researcher-and-newsletter-publisher/)
- [HtmlAgilityPack Documentation](https://html-agility-pack.net/)
- [Aspire HttpClient Resilience](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/networking-overview)
