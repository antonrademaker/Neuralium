# Test Instructions

## Goals
- Validate logic, not frameworks.
- Tests must be fast and deterministic.

## Rules
- No external network calls.
- Use fakes or in-memory providers.
- Avoid time-based flakiness (inject ITimeProvider).

## Coverage focus
- Dedupe logic
- Feed parsing edge cases
- Trend calculations
