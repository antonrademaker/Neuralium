# ADR-0003: PostgreSQL Production-Ready Storage

## Status
Accepted

## Context
Neuralium requires persistent storage for:
- Raw and normalized news items
- Deduplication state
- Trend analysis history
- Classification results
- Published outputs

Storage must support:
- Structured data (entities, metadata)
- Text search (titles, summaries)
- Time-series queries (trend detection)
- Concurrent read/write (API + Worker)
- Production reliability

## Decision
We will use **PostgreSQL from the start** with Aspire integration:
- Aspire.Npgsql component for .NET integration
- Aspire.Hosting.PostgreSQL for local containerized development
- Production deployment via Azure PostgreSQL Flexible Server or container
- Entity Framework Core for data access

## Rationale
**Advantages:**
- **Production-ready**: No migration needed from dev to prod
- **Rich features**: Full-text search, JSONB, time-series extensions
- **Aspire integration**: Automatic connection string management, health checks, telemetry
- **Ecosystem**: Mature .NET support, extensive tooling
- **Cost-effective**: Open source, no licensing fees
- **Scalability**: Handles growth from personal project to production scale

**Trade-offs:**
- Heavier than SQLite for local development
- Requires container runtime (Docker/Podman)
- More complex backup/restore than file-based SQLite

## Implementation Strategy
### Local Development
```csharp
// In AppHost Program.cs
var postgres = builder.AddPostgres("postgres")
                     .WithDataVolume()  // Persistent storage
                     .WithPgAdmin();     // Admin UI

var db = postgres.AddDatabase("neuralium");

var worker = builder.AddProject<Projects.Neuralium_Worker>("worker")
                    .WithReference(db);

var api = builder.AddProject<Projects.Neuralium_Api>("api")
                 .WithReference(db);
```

### Data Access
- Entity Framework Core with Npgsql provider
- Repository pattern for testability
- Migrations for schema evolution
- Connection pooling for performance

### Schema Design
```sql
-- Core tables
news_items (id, source_id, url_hash, title, published_at, ...)
classifications (item_id, topic, confidence, method, ...)
trends (period, topic, momentum_7d, momentum_30d, ...)
published_digests (date, content, format, ...)

-- Indexes
url_hash (UNIQUE) for deduplication
published_at (BTREE) for time-series queries
title (GIN) for full-text search
```

## Alternatives Considered
1. **SQLite only**: Simplest but not production-ready, limited concurrency
2. **Dual-support (SQLite + PostgreSQL)**: Adds abstraction complexity, doubles testing surface
3. **In-memory**: Fast prototyping but loses data, not suitable for daily runs

## Consequences
- Add Aspire.Hosting.PostgreSQL package to AppHost
- Add Aspire.Npgsql package to Worker and Api projects
- Define EF Core DbContext with entities: NewsItem, Classification, Trend, Digest
- Create initial migration with schema
- Configure connection string via Aspire service discovery
- Setup automated backups for production (pg_dump, Azure Backup)
- Document local setup: requires Docker/Podman running
- Future: Add pgvector extension for semantic search

## References
- [Aspire PostgreSQL Integration](https://learn.microsoft.com/en-us/dotnet/aspire/database/postgresql-integration)
- [EF Core with PostgreSQL](https://learn.microsoft.com/en-us/ef/core/providers/npgsql/)
- [PostgreSQL Performance Tuning](https://www.postgresql.org/docs/current/performance-tips.html)
- [Azure PostgreSQL Flexible Server](https://learn.microsoft.com/en-us/azure/postgresql/flexible-server/overview)
