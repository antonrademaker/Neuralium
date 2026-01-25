# Architecture Decision Records (ADR)

This directory contains records of architectural decisions made for Neuralium.

## What is an ADR?

An Architecture Decision Record (ADR) captures an important architectural decision along with its context and consequences. ADRs help maintain institutional knowledge and rationale for key design choices.

## Format

Each ADR follows this structure:
- **Status**: Proposed | Accepted | Deprecated | Superseded
- **Context**: The issue motivating this decision
- **Decision**: The change being proposed or enacted
- **Rationale**: Why this decision was made (advantages/trade-offs)
- **Alternatives Considered**: Other options that were evaluated
- **Consequences**: Expected outcomes and follow-up work
- **References**: Links to documentation, patterns, or related resources

## Index

| ADR | Title | Status | Date |
|-----|-------|--------|------|
| [0001](0001-agent-workflow-orchestration.md) | Agent-Based Workflow Orchestration | Accepted | 2026-01-24 |
| [0002](0002-hybrid-llm-classification.md) | Hybrid LLM Classification Strategy | Accepted | 2026-01-24 |
| [0003](0003-postgresql-production-ready-storage.md) | PostgreSQL Production-Ready Storage | Accepted | 2026-01-24 |
| [0004](0004-aspire-worker-service-execution.md) | Aspire Worker Service Execution Model | Accepted | 2026-01-24 |

## Key Decisions Summary

### Orchestration: Agent-Based Workflow
- **Why**: Modularity, composability, observability, clear separation of concerns
- **Pattern**: Workflow-as-Agent inspired by Microsoft Agent Framework
- **Trade-off**: More architectural complexity vs easier evolution and testing

### Classification: Hybrid Keywords + LLM
- **Why**: Cost-efficient, fast, resilient, high-quality
- **Approach**: Keyword prefilter → Optional LLM refinement
- **Trade-off**: Two-stage complexity vs optimized LLM usage

### Storage: PostgreSQL from Start
- **Why**: Production-ready, rich features, excellent Aspire integration
- **Choice**: No dev/prod split (same database everywhere)
- **Trade-off**: Heavier local setup vs no migration pain

### Execution: Aspire Worker Service
- **Why**: Flexible triggers, full observability, testable, cloud-agnostic
- **Pattern**: BackgroundService with one-shot execution
- **Trade-off**: Slightly more complex vs production-ready patterns

## Workflow Inspiration

The agent-based architecture is inspired by patterns from:
- [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/)
- [Agent Design Patterns](https://learn.microsoft.com/en-us/azure/architecture/ai-ml/guide/ai-agent-design-patterns)
- [Rajeev Singh's Agent Framework Workflows](https://singhrajeev.com/2026/01/18/microsoft-agent-framework-workflows-the-next-step-in-building-intelligent-multi-agent-ai-systems/)

Key workflow patterns used:
- **Sequential Pipeline**: Predictable step-by-step execution
- **Workflow-as-Agent**: Entire pipeline encapsulated as reusable component
- **Explicit Routing**: Clear message flow between agents
- **Built-in Observability**: OpenTelemetry tracing and Aspire DevUI

## Future ADRs

Potential upcoming decisions:
- Concurrent classification with fan-out/fan-in pattern
- Vector database integration for semantic search
- Multi-LLM strategy (fallback, comparison, ensemble)
- API design for query endpoints
- Deployment strategies (Azure Container Instances, Kubernetes)

## Contributing

When making new architectural decisions:
1. Create a new ADR file: `000X-brief-decision-title.md`
2. Use the template from existing ADRs
3. Update this README index
4. Reference ADR in relevant documentation
5. Mark previous ADRs as "Superseded" if applicable
