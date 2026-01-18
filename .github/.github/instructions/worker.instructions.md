# Worker Instructions — Daily Agent

## Purpose
The Worker runs once per day and must be safe to rerun.

## Rules
- Idempotent by design.
- No interactive input.
- Fail fast on configuration errors.
- Degrade gracefully if data is missing (e.g. insufficient history).

## Pipeline
- Each stage must be independently testable.
- No stage should directly call another stage's internals.
- Stages communicate via domain models only.

## Scheduling
- Assume execution via `podman-compose run --rm worker`.
