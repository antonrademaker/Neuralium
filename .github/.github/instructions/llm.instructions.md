# LLM & AI Instructions

## Usage policy
- Always keyword-prefilter before invoking an LLM.
- LLM calls must be optional and configurable.
- Never assume an LLM response is correct.

## Prompts
- Prompts must be deterministic and short.
- Explicitly specify output format (JSON preferred).
- Handle malformed responses defensively.

## Cost & performance
- Minimize token usage.
- Cache results where applicable.
