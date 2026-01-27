# Summary Versioning System

## Overview

The summary versioning system tracks which news items have outdated summaries and need regeneration. This is essential when improving the summarization algorithm (e.g., adding article content fetching).

## How It Works

### Version Field

**NewsItem.SummaryVersion (int)**
- Tracks the version of the algorithm that generated the summary
- Default value: 0 (for existing items before migration)
- Updated to CurrentSummaryVersion when a summary is generated

**LlmSettings.CurrentSummaryVersion (int)**
- Defines the current version of the summarization algorithm
- Default: 1 (introduced with article fetching)
- Increment this value to force regeneration of all summaries

### Version History

| Version | Description | Release Date |
|---------|-------------|--------------|
| 0 | Original summaries (RSS content only, before article fetching) | Initial release |
| 1 | Summaries with full article content fetching for Hacker News and minimal RSS feeds | January 27, 2026 |

## Regeneration Logic

The EnrichAgent automatically regenerates summaries when:

1. **Missing Summary**: `item.Summary` is null or empty
2. **Outdated Version**: `item.SummaryVersion < CurrentSummaryVersion`

```csharp
var needsSummary = string.IsNullOrEmpty(item.Summary) || 
                  item.SummaryVersion < _llmSettings.CurrentSummaryVersion;
```

## Configuration

### appsettings.json
```json
{
  "Llm": {
    "CurrentSummaryVersion": 1,
    "EnableSummarization": true
  }
}
```

### Incrementing Version

To force regeneration of all summaries:

1. **Update appsettings.json**:
   ```json
   "CurrentSummaryVersion": 2
   ```

2. **Restart worker**: New run will detect all items with `SummaryVersion < 2` and regenerate them

## Migration

The `AddSummaryVersioning` migration:
- Adds `SummaryVersion` column to `news_items` table
- Sets default value to 0 for existing items
- All existing items will be regenerated on next worker run (if `CurrentSummaryVersion > 0`)

```bash
# Apply migration
dotnet ef database update --project src/Neuralium.MigrationService
```

## Monitoring

### Check Items Needing Regeneration

```sql
-- Count items with outdated summaries
SELECT COUNT(*) 
FROM news_items 
WHERE summary_version < 1 
  AND (summary IS NOT NULL OR raw_content IS NOT NULL);

-- List outdated items
SELECT id, title, source, summary_version, 
       LEFT(summary, 50) as current_summary
FROM news_items 
WHERE summary_version < 1
ORDER BY published_at_utc DESC
LIMIT 10;
```

### Track Progress

```sql
-- Summary version distribution
SELECT summary_version, COUNT(*) as count
FROM news_items
GROUP BY summary_version
ORDER BY summary_version;
```

## Example Scenarios

### Scenario 1: Initial Migration
**Before migration:**
- All existing items: `SummaryVersion = NULL` (doesn't exist)
- All have summaries based on minimal RSS content

**After migration:**
- All items: `SummaryVersion = 0` (default)
- `CurrentSummaryVersion = 1` (config)
- Next worker run regenerates all 300+ items with article fetching

### Scenario 2: Future Improvement
**New feature:** Add entity extraction to summaries

**Steps:**
1. Update code to include entities in summarization
2. Update config: `CurrentSummaryVersion: 2`
3. Restart worker
4. All items with `SummaryVersion < 2` regenerated automatically

### Scenario 3: Selective Regeneration
**Problem:** Only want to regenerate Hacker News items

**Solution:**
```sql
-- Reset version for specific source
UPDATE news_items 
SET summary_version = 0 
WHERE source = 'Hacker News';

-- Next run regenerates only those items
```

## Performance Considerations

### Backfill Query
```csharp
var itemsMissingSummaries = await _dbContext.NewsItems
    .Where(item => (item.Summary == null || item.Summary == string.Empty) ||
                   item.SummaryVersion < _llmSettings.CurrentSummaryVersion)
    .Where(item => item.RawContent != null && item.RawContent != string.Empty)
    .OrderBy(item => item.PublishedAtUtc)
    .Take(10)  // Batch size
    .ToListAsync(cancellationToken);
```

### Batch Processing
- Processes 10 items per batch
- Saves to database after each batch
- Continues until no more outdated items found

### Timing Estimates
With 300 items and ~5 seconds per item (article fetch + LLM):
- 300 items ÷ 10 per batch = 30 batches
- 30 batches × 5 seconds × 10 items = ~25 minutes total

## Best Practices

### 1. Test Before Full Regeneration
```sql
-- Test with small subset
UPDATE news_items 
SET summary_version = 0 
WHERE id IN (SELECT id FROM news_items LIMIT 5);
```

### 2. Monitor Progress
Check Aspire dashboard console logs:
```
Backfill: Generated summary for: [Title]
Backfill: Fetched article for '[Title]' (1876 chars)
Backfill: Saved batch of summaries (10 items)
```

### 3. Version Documentation
Always document why version was incremented in this file.

### 4. Gradual Rollout
For large datasets:
1. Set `CurrentSummaryVersion = 1` in dev/staging first
2. Verify quality of regenerated summaries
3. Roll out to production

## Troubleshooting

### Issue: Summaries Not Regenerating
**Check:**
```sql
SELECT summary_version, current_summary_version 
FROM news_items LIMIT 1;
```
Compare with:
```json
"CurrentSummaryVersion": 1  // from appsettings.json
```

### Issue: Slow Regeneration
**Solutions:**
- Increase batch size (but watch memory)
- Temporarily disable embeddings
- Run during off-peak hours

### Issue: Failed Regenerations
**Monitor logs for:**
```
ArticleFetcher: Timeout fetching [URL]
ArticleFetcher: Failed to extract content from [URL]
```
Items with failed fetches keep old summary but get version updated.

## Future Enhancements

1. **Priority Queue**: Regenerate recent items first
2. **Selective Sources**: Only regenerate specific sources
3. **Quality Metrics**: Track improvement in summary quality by version
4. **A/B Testing**: Compare old vs new summaries before full rollout
5. **Version Metadata**: Store version change history and reasons

## References

- [NewsItem.cs](../src/Neuralium.Data/Models/NewsItem.cs) - Model definition
- [EnrichAgent.cs](../src/Neuralium.Worker/Agents/EnrichAgent.cs) - Regeneration logic
- [LlmSettings.cs](../src/Neuralium.Worker/Models/LlmSettings.cs) - Configuration
- [Migration: AddSummaryVersioning](../src/Neuralium.MigrationService/Migrations/20260127165556_AddSummaryVersioning.cs)
