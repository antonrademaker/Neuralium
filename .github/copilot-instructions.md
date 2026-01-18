# Copilot Instructions: AI News Agent

## Tech stack
- Language: C# (.NET 10), nullable enabled, implicit usings
- Runtime: Linux container (Podman), target ubuntu base image
- Storage: Postgres
- Config: environment variables
- Scheduling: systemd timer calls `podman-compose run --rm worker` or manual trigger using a script

## Architecture
Pipeline stages:
1) Ingest (RSS/Atom, GitHub releases Atom, Reddit RSS, Medium RSS)
2) Normalize (canonical URL, timestamps, source)
3) Dedupe (URL hash + title similarity)
4) Classify (keyword prefilter, then optional LLM multi-label)
5) Summarize (optional LLM)
6) Embed (optional)
7) TrendEngine (7d vs 30d momentum, entity deltas, cluster growth)
8) Publish (Markdown output)

## Coding rules
- Use dependency injection; no static singletons.
- Use HttpClientFactory, retries with exponential backoff for transient failures.
- No web scraping unless explicitly requested; prefer feeds/APIs.
- All I/O is async. Avoid blocking calls.
- Structured logging with OTel /  Microsoft.Extensions.Logging.
- Validate inputs; fail fast with clear exceptions.
- Include unit tests for each new component.
- Design for volatility

## Data contracts
- NewsItem: Id, Source, Title, Url, PublishedAtUtc, Summary?, Topics[], Entities[]
- Store raw feed item + normalized item.
