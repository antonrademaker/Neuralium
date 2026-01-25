# arXiv Integration

## Overview
Added support for searching arXiv.org academic papers using a provider abstraction pattern that handles volatility and different source types.

## Architecture

### Feed Provider Abstraction
Created `IFeedProvider` interface to support multiple feed types with different characteristics:
- **Volatility handling**: Same query can return different results frequently (arXiv updates daily)
- **API differences**: URL construction varies by source type
- **Resilience**: Each provider handles its own error handling and retries

### Implementations

#### StandardFeedProvider
- Handles: RSS, Atom, GitHub, Reddit, Medium
- Uses CodeHollow.FeedReader directly
- Standard feed URLs

#### ArxivFeedProvider  
- Handles: arXiv API queries
- Constructs API URLs: `http://export.arxiv.org/api/query`
- Query format stored in FeedSource.Url field
- Returns Atom 1.0 feeds (compatible with FeedReader)

## Configuration

### FeedSource Model Updates
- **Type field**: Added "Arxiv" support (comment updated)
- **Url field**: For arXiv, contains search query instead of URL
  - Examples: `"cat:cs.AI OR cat:cs.LG OR cat:cs.CL"`, `"all:neural network OR all:deep learning"`

### arXiv Query Syntax
Supported field prefixes:
- `ti:` - Title
- `au:` - Author  
- `abs:` - Abstract
- `cat:` - Subject category
- `all:` - All fields

Operators: `AND`, `OR`, `ANDNOT`

### Seed Data
Added 3 arXiv sources to MigrationService:
1. **arXiv AI Research**: `cat:cs.AI OR cat:cs.LG OR cat:cs.CL` (4-hour rate limit)
2. **arXiv Neural Networks**: `all:neural network OR all:deep learning` (4-hour rate limit)
3. **arXiv Large Language Models**: `all:large language model OR all:LLM OR all:transformer` (4-hour rate limit)

## Rate Limiting
- arXiv recommends **240 minutes between requests** (4 hours)
- arXiv updates once daily (~midnight EST)
- Respects MinimumFetchIntervalMinutes on FeedSource

## Telemetry
Added ActivitySource tracing:
- `Neuralium.Worker.StandardFeedProvider`
- `Neuralium.Worker.ArxivFeedProvider`
- Registered in OpenTelemetry configuration

## API Details
- **Base URL**: `http://export.arxiv.org/api/query`
- **Parameters**:
  - `search_query`: Query string
  - `start`: Offset (default 0)
  - `max_results`: Limit (default 50)
  - `sortBy`: submittedDate
  - `sortOrder`: descending
- **Response format**: Atom 1.0 XML
- **Rate limit**: 3 seconds between requests (handled by our 4-hour minimum)

## Usage Example

### Adding arXiv Source via SQL
```sql
INSERT INTO feed_sources (Name, Type, Url, Enabled, TimeoutSeconds, MinimumFetchIntervalMinutes, CreatedAtUtc)
VALUES (
    'arXiv Quantum Computing',
    'Arxiv',
    'cat:quant-ph AND ti:quantum computing',
    true,
    45,
    240,
    NOW()
);
```

### Expected Log Output
```
info: Neuralium.Worker.IngestAgent: Processing 7 enabled sources
info: Neuralium.Worker.IngestAgent: Fetching [arXiv AI Research] (type: Arxiv) from cat:cs.AI OR cat:cs.LG OR cat:cs.CL
info: Neuralium.Worker.ArxivFeedProvider: [arXiv AI Research] querying arXiv with 'cat:cs.AI OR cat:cs.LG OR cat:cs.CL' -> http://export.arxiv.org/api/query?search_query=cat%3Acs.AI+OR+cat%3Acs.LG+OR+cat%3Acs.CL&start=0&max_results=50&sortBy=submittedDate&sortOrder=descending
info: Neuralium.Worker.ArxivFeedProvider: [arXiv AI Research] fetched 50 articles from arXiv
info: Neuralium.Worker.IngestAgent: [arXiv AI Research] fetched with 50 raw items
```

## Files Changed

### New Files
- `src/Neuralium.Worker/Agents/IFeedProvider.cs` - Provider interface
- `src/Neuralium.Worker/Agents/StandardFeedProvider.cs` - RSS/Atom provider
- `src/Neuralium.Worker/Agents/ArxivFeedProvider.cs` - arXiv API provider

### Modified Files
- `src/Neuralium.Worker/Agents/IngestAgent.cs` - Uses provider abstraction
- `src/Neuralium.Worker/Program.cs` - Registers providers in DI
- `src/Neuralium.Data/Models/FeedSource.cs` - Updated comments for arXiv
- `src/Neuralium.MigrationService/Worker.cs` - Added arXiv seed data

## Design Decisions

### Why Provider Abstraction?
1. **Volatility isolation**: arXiv has different update patterns than RSS feeds
2. **URL construction**: arXiv requires query → URL translation
3. **Future extensibility**: Easy to add Twitter, Reddit API, etc.
4. **Testability**: Can mock providers independently

### Why Store Query in Url Field?
1. **Simplicity**: No schema changes required
2. **Flexibility**: Field already has 2000 char limit
3. **Clarity**: Type field distinguishes interpretation
4. **Consistency**: Single configuration model for all sources

### Why 4-Hour Rate Limit for arXiv?
1. **arXiv updates daily**: More frequent fetching wastes resources
2. **Respectful**: Reduces load on arXiv servers
3. **API recommendation**: arXiv suggests 3-second delays between requests
4. **Development friendly**: Can be overridden per source if needed

## Next Steps
1. Run migration to apply AddRateLimitingFeedbackAndScoring schema
2. Start Aspire to test arXiv integration
3. Verify arXiv articles appear in pipeline output
4. Monitor telemetry in Aspire dashboard
