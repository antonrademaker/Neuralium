# Storage Instructions

## Storage targets
- Postgres

## Rules
- Never hardcode connection strings.
- Migrations must be forward-only.
- Queries must be async.
- Use timebased uuid's for keys
- Indexes required for:
  - Url hash
  - Published date
  - Topic/entity lookups

## Data integrity
- Enforce uniqueness at DB level where possible.
- Prefer explicit transactions for multi-step writes.
