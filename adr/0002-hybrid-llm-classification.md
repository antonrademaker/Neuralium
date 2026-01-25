# ADR-0002: Hybrid LLM Classification Strategy

## Status
Accepted

## Context
Neuralium needs to classify news items into AI topics. Classification quality directly impacts trend detection accuracy. We need to balance:
- Cost (LLM API calls)
- Latency (processing speed)
- Accuracy (classification quality)
- Resilience (no external dependencies for core function)

## Decision
We will use a **Hybrid (Keyword Filter + LLM Refinement)** approach:
1. **Stage 1 - Keyword Prefilter**: Fast, deterministic keyword matching assigns initial topics
2. **Stage 2 - LLM Refinement** (optional): LLM enhances classification for ambiguous cases
3. **Configuration**: LLM usage is configurable via environment variable (default: OFF)
4. **Fallback**: If LLM fails, keyword results are retained

## Rationale
**Advantages:**
- **Cost-efficient**: LLM only processes items that pass keyword filter (~20-30% of items)
- **Fast**: Keyword matching is instant; total pipeline time reduced
- **Resilient**: Works completely offline with keywords alone
- **High-quality**: LLM refines edge cases and multi-label classification
- **Gradual adoption**: Start keyword-only, enable LLM later

**Trade-offs:**
- More complex classification logic (two-stage pipeline)
- Requires maintaining keyword lists alongside LLM prompts
- Potential inconsistency between keyword and LLM results

## Implementation Strategy
### Keyword Classification
- Topic-specific keyword lists in YAML configuration
- Simple exact/substring matching
- Multiple topics allowed per item
- Fast path: no external calls

### LLM Refinement (Optional)
- Environment variable: `NEURALIUM_ENABLE_LLM=true`
- Triggered only if keyword confidence < threshold OR item matches multiple topics
- LLM prompt includes keyword results as context
- Retry with exponential backoff
- Circuit breaker pattern for API failures

### Fallback Strategy
```
Keyword Match → High Confidence? → Use Keyword Result
                ↓ Low Confidence
                LLM Available? → Yes → LLM Refine → Success? → Use LLM Result
                ↓ No                   ↓ Fail
                Use Keyword Result     Use Keyword Result
```

## Alternatives Considered
1. **Keyword-only**: Cheapest but lower quality for ambiguous content
2. **Optional LLM**: More flexible but doesn't optimize LLM usage
3. **LLM-first with fallback**: Higher cost and latency; no offline operation

## Consequences
- Create `KeywordClassifier` class with configurable keyword lists
- Create `LlmClassifier` class with retry/circuit breaker logic
- Implement `HybridClassifier` orchestrating both approaches
- Configuration: `classification.yaml` for keywords, env vars for LLM
- Metrics: Track keyword vs LLM usage, accuracy, latency
- Future: Add confidence scores, active learning, keyword auto-update

## References
- [Semantic Kernel AI Orchestration](https://learn.microsoft.com/en-us/semantic-kernel/overview/)
- [Cost-Effective LLM Strategies](https://learn.microsoft.com/en-us/azure/architecture/ai-ml/guide/genai-cost-optimization)
- [Circuit Breaker Pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/circuit-breaker)
