# LLM Configuration Guide

## Overview
The Worker supports optional LLM integration for enriching news items with AI-generated summaries and embeddings. This is configured via `appsettings.json` or environment variables.

## Configuration

### appsettings.json
```json
{
  "Llm": {
    "Enabled": false,
    "Provider": "AzureOpenAI",
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiKey": "your-api-key-here",
    "ModelName": "gpt-4o",
    "EmbeddingModelName": "text-embedding-3-small",
    "EnableSummarization": true,
    "EnableEmbeddings": false,
    "MaxSummaryTokens": 150,
    "TimeoutSeconds": 30,
    "MaxRetries": 3
  }
}
```

### appsettings.local.json (recommended for secrets)
Create `appsettings.local.json` (already in .gitignore) with your actual credentials:
```json
{
  "Llm": {
    "Enabled": true,
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiKey": "your-actual-api-key"
  }
}
```

### Environment Variables
Can also configure via environment variables (useful for production):
- `Llm__Enabled=true`
- `Llm__Provider=AzureOpenAI`
- `Llm__Endpoint=https://your-resource.openai.azure.com/`
- `Llm__ApiKey=your-api-key`
- `Llm__ModelName=gpt-4o`

## Providers

### Azure OpenAI (Recommended)
```json
{
  "Llm": {
    "Provider": "AzureOpenAI",
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiKey": "your-azure-openai-key",
    "ModelName": "gpt-4o",
    "EmbeddingModelName": "text-embedding-3-small"
  }
}
```

**Setup:**
1. Create Azure OpenAI resource in Azure Portal
2. Deploy models: gpt-4o (or gpt-4o-mini) for summarization, text-embedding-3-small for embeddings
3. Copy endpoint URL and API key from Azure Portal
4. Set deployment names to match ModelName/EmbeddingModelName

### OpenAI
```json
{
  "Llm": {
    "Provider": "OpenAI",
    "ApiKey": "sk-proj-...",
    "ModelName": "gpt-4o",
    "EmbeddingModelName": "text-embedding-3-small"
  }
}
```

**Setup:**
1. Get API key from https://platform.openai.com/api-keys
2. No endpoint needed (uses api.openai.com)
3. Model names: gpt-4o, gpt-4o-mini, gpt-3.5-turbo

### Ollama (Local Models)
```json
{
  "Llm": {
    "Provider": "Ollama",
    "Endpoint": "http://localhost:11434",
    "ModelName": "llama3.2",
    "EmbeddingModelName": "nomic-embed-text"
  }
}
```

**Setup:**
1. Install Ollama: https://ollama.com/download
2. Pull models: `ollama pull llama3.2` and `ollama pull nomic-embed-text`
3. Start Ollama: `ollama serve` (or it runs automatically)
4. No API key required for local Ollama

**Available Models:**
- Chat: llama3.2, llama3.1, phi3:mini, mistral, qwen2.5
- Embedding: nomic-embed-text, all-minilm

## Settings Reference

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Enabled` | bool | false | Master switch for LLM features |
| `Provider` | string | "AzureOpenAI" | "AzureOpenAI", "OpenAI", or "Ollama" |
| `Endpoint` | string? | null | API endpoint URL (Azure OpenAI, Ollama, or custom) |
| `ApiKey` | string? | null | API authentication key (not required for Ollama) |
| `ModelName` | string? | "gpt-4o" | Model for summarization |
| `EmbeddingModelName` | string? | "text-embedding-3-small" | Model for embeddings |
| `EnableSummarization` | bool | true | Generate summaries for news items |
| `EnableEmbeddings` | bool | false | Generate embeddings for semantic search |
| `MaxSummaryTokens` | int | 150 | Maximum tokens for generated summaries |
| `Temperature` | float | 0.3 | Temperature for LLM generation (0.0-1.0) |
| `MaxSummaryTokens` | int | 150 | Maximum tokens in generated summaries |
| `TimeoutSeconds` | int | 30 | Request timeout |
| `MaxRetries` | int | 3 | Retry attempts for failed requests |

## Cost Considerations

### Azure OpenAI Pricing (as of 2026)
- **gpt-4o**: $5/1M input tokens, $15/1M output tokens
- **gpt-4o-mini**: $0.15/1M input tokens, $0.60/1M output tokens
- **text-embedding-3-small**: $0.02/1M tokens

### Estimated Costs
For 100 news items per day with 500-word articles:
- Summarization (gpt-4o-mini, 150 tokens/summary): ~$0.10/day
- Embeddings (text-embedding-3-small): ~$0.05/day
- **Total: ~$4.50/month**

## Usage

### Enable LLM For Development
1. Create `appsettings.local.json` with your credentials
2. Set `Enabled: true`
3. Configure either Azure OpenAI or OpenAI settings
4. Run worker: `dotnet run --project src/Neuralium.Worker`

### Disable LLM (Default)
The worker runs without LLM by default:
- `Enabled: false` in appsettings.json
- EnrichAgent passes items through unchanged
- No API calls or costs incurred

## Troubleshooting

### "Endpoint or ApiKey not configured"
- Check `appsettings.local.json` has correct Endpoint and ApiKey
- Verify Provider matches your setup (AzureOpenAI vs OpenAI)

### "Unauthorized" or 401 errors
- Verify ApiKey is correct and not expired
- For Azure OpenAI: Check key from Azure Portal → Keys and Endpoint
- For OpenAI: Regenerate key at https://platform.openai.com/api-keys

### Timeouts
- Increase `TimeoutSeconds` for slower responses
- Check network connectivity to Endpoint
- Verify model is deployed (Azure OpenAI)

### Rate Limiting
- Azure OpenAI has deployment-level rate limits (TPM/RPM)
- Consider increasing quota in Azure Portal
- Implement exponential backoff (already built-in with MaxRetries)

## Best Practices

1. **Use appsettings.local.json for secrets** - Already in .gitignore
2. **Start with gpt-4o-mini** - Cheaper, faster, good quality for summaries
3. **Enable summarization first** - Embeddings optional unless building search
4. **Monitor costs** - Azure Portal → Cost Management
5. **Production secrets** - Use Azure Key Vault or environment variables
6. **Rate limiting** - Azure Quotas → Adjust TPM/RPM as needed

## Implementation Status

- ✅ Configuration model (LlmSettings.cs)
- ✅ appsettings.json structure
- ⏳ ILlmService interface (next step)
- ⏳ AzureOpenAIService implementation
- ⏳ EnrichAgent integration
