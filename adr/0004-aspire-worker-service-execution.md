# ADR-0004: Aspire Worker Service Execution Model

## Status
Accepted

## Context
Neuralium runs as a "daily one-shot worker" that executes the entire pipeline and exits. Requirements:
- Scheduled execution (daily, typically 6 AM)
- Idempotent: safe to run multiple times
- Observable: logs, metrics, traces
- Testable: can run manually or in CI/CD
- Production-ready: error handling, retry logic
- Cloud-agnostic: runs locally or in containers

## Decision
We will use **.NET Worker Service with Aspire orchestration**:
- `Neuralium.Worker` project as BackgroundService
- Aspire AppHost orchestrates Worker + API + PostgreSQL
- Execution modes:
  - **Development**: Run via Aspire (F5 in VS Code)
  - **Manual**: `dotnet run --project Worker` or `aspire run`
  - **Scheduled**: systemd timer or cron calling `podman-compose run --rm worker`
  - **Cloud**: Azure Container Instances scheduled job

## Rationale
**Advantages:**
- **Aspire native**: Full telemetry, service discovery, configuration management
- **Flexible triggers**: Manual, scheduled, API-triggered, or event-driven
- **Testable**: Easy to run in development and CI/CD
- **Observable**: Built-in OpenTelemetry integration via Aspire
- **Production patterns**: Graceful shutdown, health checks, retry logic
- **Cloud-agnostic**: Same code runs locally, systemd, Kubernetes, or Azure

**Trade-offs:**
- Slightly more complex than simple console app
- Requires understanding Worker Service pattern
- Aspire dependency (but provides immense value)

## Implementation Strategy
### Worker Service Structure
```csharp
public class NewsWorkerService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("Neuralium Worker starting...");
        
        try
        {
            // Run pipeline agents sequentially
            await _ingestAgent.RunAsync(ct);
            await _normalizeAgent.RunAsync(ct);
            await _dedupeAgent.RunAsync(ct);
            await _classifyAgent.RunAsync(ct);
            await _enrichAgent.RunAsync(ct);
            await _analyzeAgent.RunAsync(ct);
            await _publishAgent.RunAsync(ct);
            
            _logger.LogInformation("Pipeline completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline failed");
            throw; // Exit with error code
        }
    }
}
```

### Execution Modes
#### 1. Development (Aspire Dashboard)
```bash
cd src/Neuralium.AppHost
dotnet run
# or
aspire run
```

#### 2. Manual Trigger
```bash
cd src/Neuralium.Worker
dotnet run
```

#### 3. Scheduled (systemd)
```ini
# /etc/systemd/system/neuralium.timer
[Timer]
OnCalendar=daily
Persistent=true

[Install]
WantedBy=timers.target
```

```ini
# /etc/systemd/system/neuralium.service
[Service]
Type=oneshot
WorkingDirectory=/opt/neuralium
ExecStart=/usr/bin/podman-compose run --rm worker
```

#### 4. Cloud (Azure Container Instances)
```bash
az container create \
  --resource-group neuralium \
  --name neuralium-worker \
  --image neuralium-worker:latest \
  --restart-policy Never \
  --schedule "0 6 * * *"
```

### Idempotency & Error Handling
- **Idempotent operations**: Upsert semantics, URL-based deduplication
- **Transactional boundaries**: Each agent stage commits independently
- **Retry logic**: Exponential backoff for transient failures (HTTP, DB)
- **Circuit breaker**: Stop early if critical services unavailable
- **Checkpointing**: Track last successful run timestamp

### Observability
- **Logs**: Structured logging with Serilog
- **Metrics**: Custom metrics per agent (items processed, duration, errors)
- **Traces**: Distributed tracing via OpenTelemetry
- **Health**: Liveness/readiness probes for Kubernetes
- **Dashboard**: Real-time monitoring via Aspire DevUI

## Alternatives Considered
1. **Console App + External Scheduler**: Simpler but loses Aspire benefits, harder to test
2. **Background Service + API Trigger**: More flexible but requires API deployment
3. **Azure Functions / Serverless**: Cloud-native but vendor lock-in, cold start issues

## Consequences
- Create `Neuralium.Worker` project with Worker Service template
- Implement `NewsWorkerService` inheriting from `BackgroundService`
- Register pipeline agents in DI container
- Configure Aspire service defaults (telemetry, health checks, resilience)
- Create systemd timer/service files for Linux deployment
- Document execution modes in README
- Setup CI/CD to build container image
- Future: Add API trigger endpoint for on-demand execution

## References
- [.NET Worker Services](https://learn.microsoft.com/en-us/dotnet/core/extensions/workers)
- [Aspire Worker Service Integration](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/app-host-overview)
- [Background Task Patterns](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/background-tasks-with-ihostedservice)
- [systemd Timer Units](https://www.freedesktop.org/software/systemd/man/systemd.timer.html)
