# ADR-0001: Agent-Based Workflow Orchestration

## Status
Accepted

## Context
Neuralium processes AI news through a multi-stage pipeline: Ingest → Normalize → Dedupe → Classify → Enrich → Analyze → Publish. We need to choose an orchestration pattern that supports:
- Clear separation of concerns
- Modularity and reusability
- Observability and debugging
- Future expansion to concurrent/parallel processing

## Decision
We will use **Agent-based orchestration with Workflow-as-Agent pattern** inspired by Microsoft Agent Framework:
- Each pipeline stage is encapsulated as an autonomous agent
- Agents communicate through well-defined message contracts
- The entire pipeline can be composed as a workflow and reused
- Individual agents can be tested, replaced, or enhanced independently

## Rationale
**Advantages:**
- **Modularity**: Each stage is a self-contained agent with clear responsibilities
- **Composability**: Agents can be nested, reused, and orchestrated flexibly
- **Observability**: Built-in telemetry and DevUI visualization (via Aspire)
- **Evolution path**: Easy to add concurrent processing, conditional routing, or human-in-the-loop steps
- **Industry alignment**: Follows Microsoft Agent Framework patterns and best practices

**Trade-offs:**
- More architectural complexity than simple sequential code
- Requires agent framework abstractions (but .NET has excellent support)
- Initial learning curve for team members

## Alternatives Considered
1. **Sequential Pipeline (Simple)**: Easiest to implement but harder to extend
2. **Concurrent Fan-Out/Fan-In**: Faster but more complex; premature for MVP
3. **Hybrid Sequential + Selective Concurrency**: Good balance but adds conditional complexity

## Consequences
- Create `INewsAgent` interface for pipeline stage contracts
- Implement agents for: IngestAgent, NormalizeAgent, DedupeAgent, ClassifyAgent, EnrichAgent, AnalyzeAgent, PublishAgent
- Use Aspire's built-in orchestration for agent execution
- Leverage OpenTelemetry for agent-level tracing
- Future: Easy to add parallel classification, multi-LLM enrichment, or approval workflows

## References
- [Microsoft Agent Framework Workflows](https://learn.microsoft.com/en-us/agent-framework/user-guide/workflows/orchestrations/overview)
- [Agent Design Patterns](https://learn.microsoft.com/en-us/azure/architecture/ai-ml/guide/ai-agent-design-patterns)
- [Workflow-as-Agent Pattern](https://singhrajeev.com/2026/01/18/microsoft-agent-framework-workflows-the-next-step-in-building-intelligent-multi-agent-ai-systems/)
