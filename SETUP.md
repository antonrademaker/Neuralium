# Neuralium Setup Guide

This guide provides step-by-step instructions to set up and run Neuralium locally. Each step is small, validated against Microsoft Learn documentation, and designed for success.

---

## 🎯 Architecture Overview

Neuralium uses an **agent-based workflow architecture** inspired by [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/):

```
┌─────────────┐     ┌──────────────┐     ┌─────────────┐
│   Ingest    │────▶│  Normalize   │────▶│   Dedupe    │
│    Agent    │     │    Agent     │     │   Agent     │
└─────────────┘     └──────────────┘     └─────────────┘
                                                │
┌─────────────┐     ┌──────────────┐           │
│   Publish   │◀────│   Analyze    │◀──────────┘
│    Agent    │     │    Agent     │
└─────────────┘     └──────────────┘
                           ▲
┌─────────────┐     ┌──────────────┐
│   Enrich    │────▶│   Classify   │
│    Agent    │     │    Agent     │
└─────────────┘     └──────────────┘
```

**Key Decisions** (see `/adr` folder for details):
- **Orchestration**: Agent-based with Workflow-as-Agent pattern ([ADR-0001](adr/0001-agent-workflow-orchestration.md))
- **Classification**: Hybrid keyword + optional LLM refinement ([ADR-0002](adr/0002-hybrid-llm-classification.md))
- **Storage**: PostgreSQL with Aspire integration ([ADR-0003](adr/0003-postgresql-production-ready-storage.md))
- **Execution**: .NET Worker Service with Aspire orchestration ([ADR-0004](adr/0004-aspire-worker-service-execution.md))

---

## 📋 Prerequisites

### Step 1: Install .NET 10 SDK

**What**: .NET 10 is required for building and running Neuralium.

**Validation**: Microsoft Learn - [Install .NET](https://learn.microsoft.com/en-us/dotnet/core/install/)

**Windows**:
```powershell
winget install Microsoft.DotNet.SDK.10
```

**macOS**:
```bash
brew install dotnet@10
```

**Linux**:
```bash
# See Microsoft Learn for distribution-specific instructions
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 10.0
```

**Verify**:
```bash
dotnet --version
# Should output: 10.0.x
```

✅ **Current Status**: .NET 10.0.102 installed and verified

---

### Step 2: Install Container Runtime

**What**: Required for running PostgreSQL and other containerized services locally.

**Validation**: Microsoft Learn - [Aspire Container Runtime](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling#container-runtime)

⚠️ **Current Status**: No container runtime detected (Docker/Podman not installed)

**Note**: PostgreSQL is currently commented out in AppHost. For now, you can develop without containers, but you'll need a container runtime when we add PostgreSQL.

**Install when ready**:

**Windows**:
```powershell
winget install Docker.DockerDesktop
```

**macOS**:
```bash
brew install --cask docker
```

**Linux** (Podman recommended):
```bash
# Ubuntu/Debian
sudo apt-get install podman podman-compose

# Fedora/RHEL
sudo dnf install podman podman-compose
```

**Verify**:
```bash
docker --version
# or
podman --version
```

**Start Docker Desktop** (Windows/macOS):
- Launch Docker Desktop application
- Wait for "Docker is running" status

---

✅ **Current Status**: VS Code detected (you're using it now!)

**Verify C# Dev Kit Extension**:
1. Open VS Code Extensions (`Ctrl+Shift+X` / `Cmd+Shift+X`)
2. Search for "C# Dev Kit"
3. Ensure `ms-dotnettools.csdevkit` is installed

If not installed:
- Click **Install** on C# Dev Kit extension
- Restart VS Code+X` (macOS)
3. Search for "C# Dev Kit"
4. Click **Install** on `ms-dotnettools.csdevkit`

**Verify**:
- Open Command Palette (`Ctrl+Shift+P` / `Cmd+Shift+P`)
- Type "C#: Show Output" - should work without errors

---

### Step 4: Install Aspire CLI (Optional but Recommended)

**What**: Provides `aspire` commands for easier project management.

**Validation**: Microsoft Learn - [Aspire CLI](https://learn.microsoft.com/en-us/dotnet/aspire/cli/overview)

**Install**:
```bash
⚠️ **Current Status**: Aspire CLI not installed (optional)

**Install when ready**:
```bash
dotnet tool install aspire.cli
```

**Verify**:
```bash
dotnet aspire --version
```

**Note**: You can use `dotnet run` instead of `aspire run` - CLI is purely for convenience

## 🚀 Project Setup

✅ **Current Status**: Repository already cloned at `D:\repos\Neuralium`

---

### Step 6: Restore NuGet Packages

✅ **Current Status**: NuGet packages already restored

**Verify**:
```bash
dotnet restore
```

**Expected Output**:
```
Restore succeeded in X.Xs
```

---

### Step 7: Run Aspire AppHost

✅ **Current Status**: Aspire AppHost is RUNNING

**What**: Starts the entire Neuralium stack (Worker + API) with Aspire Dashboard.

**Validation**: Microsoft Learn - [Run Aspire Apps](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/app-host-overview#run-the-app-host)

**Detected Ports**:
- ✅ Dashboard Frontend: `https://localhost:15243`
- ✅ Dashboard HTTP: `http://localhost:15000`
- ✅ OTLP Endpoint: `https://localhost:16243`
- ✅ Resource Service: `https://localhost:17000`

**Current Configuration**:
- Worker: `Neuralium.Worker` project
- API: `Neuralium.Api` project
- PostgreSQL: Not yet configured (commented out)

**To restart**:
```bash
cd src/Neuralium.AppHost
dotnet run
```

---

### Step 8: Verify Dashboard Access

✅ **Current Status**: Dashboard should be accessible

**Open Aspire Dashboard**:
- **HTTPS**: https://localhost:15243
- **HTTP**: http://localhost:15000

**What you should see**:
- ✅ **worker**: Running or Completed (one-shot execution)
- ✅ **api**: Running
- ⚠️ **postgres**: Not yet added (coming in next steps)

**Explore Dashboard Tabs**:
- **Resources**: View all services and their status
- **Console Logs**: Check Worker and API output
- **Traces**: See OpenTelemetry distributed tracing (when telemetry is added)
- **Metrics**: Monitor performance counters

**Troubleshooting**:
- **Can't access dashboard?** Check if ports 15243 or 15000 are available
- **HTTPS certificate error?** Use HTTP profile: http://localhost:15000
- **Services not starting?** Check Console Logs tab for errorsutput
- **Traces**: See OpenTelemetry distributed tracing
- **Metrics**: Monitor performance

---

### Step 10: Access PostgreSQL (Optional)

**Using pgAdmin** (if configured in AppHost):
- Open: `http://localhost:5050` (port may vary)
- Login with credentials from AppHost configuration

**Using p9: Add PostgreSQL to AppHost (Next Step)

⚠️ **Current Status**: PostgreSQL not yet configured

**What's Next**: We'll add PostgreSQL integration to enable data persistence.

**Preview** (what we'll add to AppHost):
```csharp
var postgres = builder.AddPostgres("postgres")
                      .WithDataVolume()
                      .WithPgAdmin();

var db = postgres.AddDatabase("neuralium");

var worker = builder.AddProject("worker", "../Neuralium.Worker/Neuralium.Worker.csproj")
                    .WithReference(db);

var api = builder.AddProject("api", "../Neuralium.Api/Neuralium.Api.csproj")
                 .WithReference(db);
```

**Prerequisites** (before adding PostgreSQL):
- ⚠️ Container runtime must be installed (Docker Desktop or Podman)
- See Step 2 above

**When PostgreSQL is added, you'll be able to**:
- Access pgAdmin at: `http://localhost:5050`
- Connect via psql CLI
- View data in Aspire Dashboard Step 11: Manual Worker Execution

**What**: Run the worker pipeline directly (bypasses Aspire orchestration).

```bash
cd src/Neuralium.Worker
dotnet run
```

**Expected Output**:
```
[IngestAgent] Starting ingestion from 5 sources...
[IngestAgent] Fetched 127 items
[NormalizeAgent] Normalizing 127 items...
[DedupeAgent] Found 12 duplicates, 115 unique items
[ClassifyAgent] Classified 115 items into 8 topics
[EnrichAgent] Enrichment skipped (LLM disabled)
[AnalyzeAgent] Detected 3 trending topics
[PublishAgent] Published daily digest to output/digest-2026-01-24.md
Pipeline completed successfully in 12.3s
```

---

## 🛠️ Development Workflow

### Step 12: Configure LLM (Optional)

**What**: Enable optional LLM classification refinement.

**Edit**: `src/Neuralium.Worker/appsettings.Development.json`

```json
{
  "Classification": {
    "EnableLLM": true,
    "Provider": "AzureOpenAI",
    "Endpoint": "https://your-instance.openai.azure.com/",
    "ApiKey": "your-key-here",
    "Model": "gpt-4"
  }
}
```

**Environment Variable** (alternative):
```bash
export NEURALIUM_ENABLE_LLM=true
```

**Validation**: Microsoft Learn - [Semantic Kernel Configuration](https://learn.microsoft.com/en-us/semantic-kernel/get-started/)

---

### Step 13: Run Tests (Coming Soon)

```bash
dotnet test
```

---

### Step 14: Build Container Images

**What**: Build production-ready container images for deployment.

```bash
dotnet publish src/Neuralium.Worker -c Release -r linux-x64 --self-contained
podman build -t neuralium-worker:latest -f src/Neuralium.Worker/Dockerfile .
```

---

## 📊 Observability

### Step 15: View Telemetry

**Logs**:
- Aspire Dashboard → Console Logs → Select resource
- Structured logs with correlation IDs

**Traces**:
- Aspire Dashboard → Traces
- See entire pipeline execution flow
- Agent-level timing and dependencies

**Metrics**:
- Aspire Dashboard → Metrics
- Custom metrics: items processed, classification accuracy, LLM usage

**DevUI Visualization** (inspired by Agent Framework):
- Visual graph of agent workflow
- Real-time execution progress
- Debugging and optimization insights

---

## 🐧 Production Deployment (Linux)

### Step 16: Systemd Timer Setup

**What**: Schedule daily execution using systemd.

**Create Timer**: `/etc/systemd/system/neuralium.timer`
```ini
[Unit]
Description=Neuralium Daily News Aggregation

[Timer]
OnCalendar=daily
Persistent=true

[Install]
WantedBy=timers.target
```

**Create Service**: `/etc/systemd/system/neuralium.service`
```ini
[Unit]
Description=Neuralium Worker
After=network.target

[Service]
Type=oneshot
User=neuralium
WorkingDirectory=/opt/neuralium
ExecStart=/usr/bin/podman-compose run --rm worker
Environment="NEURALIUM_ENABLE_LLM=false"
```

**Enable & Start**:
```bash
sudo systemctl daemon-reload
sudo systemctl enable neuralium.timer
sudo systemctl start neuralium.timer
sudo systemctl list-timers --all  # Verify
```

**Validation**: Microsoft Learn - [Background Services](https://learn.microsoft.com/en-us/dotnet/core/extensions/workers)

---

## 🔍 Troubleshooting

### Common Issues

**Issue**: "No connection could be made to the database"
- **Fix**: Ensure PostgreSQL container is running: `podman ps | grep postgres`
- Check connection string in Aspire Dashboard

**Issue**: "Aspire dashboard won't start"
- **Fix**: Check for port conflicts in `launchSettings.json`
- Try HTTP profile: Select "Aspire (http)" in VS Code

**Issue**: "Worker completes but no output"
- **Fix**: Check logs in Aspire Dashboard Console Logs
- Verify feeds in configuration: `appsettings.json`

**Issue**: "LLM classification fails"
- **Fix**: LLM is optional; keyword classification always works
- Check API key configuration
- Verify network connectivity to Azure OpenAI

---

## 📚 Next Steps

1. **Configure Feed Sources**: Edit `appsettings.json` to add/remove RSS feeds
2. **Customize Topics**: Update keyword lists in `classification.yaml`
3. **Enable LLM**: Configure Azure OpenAI for enhanced classification
4. **Schedule Production**: Setup systemd timer or Azure Container Instances
5. **Explore Agents**: Dive into agent implementations in `src/Neuralium.Worker/Agents`

---

## 🔗 References

- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/)
- [Agent Workflow Patterns](https://learn.microsoft.com/en-us/azure/architecture/ai-ml/guide/ai-agent-design-patterns)
- [PostgreSQL with Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/database/postgresql-integration)
- [Worker Services in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/workers)

---

## 🎓 Learning Path

**Recommended Order**:
1. Start with simple sequential pipeline (no agents yet)
2. Refactor to agent-based architecture
3. Add concurrent processing for classification
4. Implement LLM integration with fallback
5. Add trend detection and analysis
6. Setup production deployment

---

## 📅 Development Roadmap

### ✅ Phase 1: Core Features (COMPLETED)

#### 1.1 OpenTelemetry Integration ✅
- ActivitySource tracing across Worker + 7 agents + 2 providers
- Structured logging with EventIds and categories
- Dashboard telemetry at https://localhost:15243
- **Files**: Worker/Program.cs, all Agent classes

#### 1.2 Rate Limiting ✅
- `MinimumFetchIntervalMinutes` property on FeedSource (default: 60)
- Enforcement in IngestAgent with remaining time logging
- arXiv sources configured for 240-minute intervals (4 hours)
- **Files**: Data/Models/FeedSource.cs, Worker/Agents/IngestAgent.cs

#### 1.3 User Feedback System ✅
- `NewsItemFeedback` entity with unique constraint (UserId + NewsItemId)
- `KeywordFeedback` entity with unique constraint (UserId + TopicKeywordId)
- Database migration applied successfully
- **Files**: Data/Models/NewsItemFeedback.cs, Data/Models/KeywordFeedback.cs
- **Migration**: 20260125110235_AddRateLimitingFeedbackAndScoring.cs

#### 1.4 Separated Scoring ✅
- `NewsItemScore` entity with 4 independent components
- RecencyScore (7-day decay), TopicScore (keyword count), UserFeedbackScore (aggregated), ComputedTrendScore (weighted: 70/20/10)
- AnalyzeAgent queries NewsItemFeedback and calculates aggregate scores
- PublishAgent saves items then scores with proper FK relationships
- **Files**: Data/Models/NewsItemScore.cs, Worker/Agents/AnalyzeAgent.cs, Worker/Agents/PublishAgent.cs

#### 1.5 arXiv Integration ✅
- Provider abstraction: IFeedProvider interface
- StandardFeedProvider (RSS/Atom/GitHub/Reddit/Medium)
- ArxivFeedProvider (query-based API searches)
- 3 arXiv sources in seed data (AI Research, Neural Networks, LLMs)
- Within-batch deduplication fix for overlapping results
- **Files**: Worker/Agents/IFeedProvider.cs, Worker/Agents/StandardFeedProvider.cs, Worker/Agents/ArxivFeedProvider.cs
- **Documentation**: docs/arxiv-integration.md

#### 1.6 Feedback API ✅
- **POST /api/feedback/newsitem/{id}** - Submit news item feedback
- **POST /api/feedback/keyword/{id}** - Submit keyword feedback
- FeedbackController with validation, update/create logic, structured logging
- Swagger documentation at http://localhost:5000/swagger
- Returns 201 Created, 200 OK, 404 Not Found appropriately
- **Files**: Api/Controllers/FeedbackController.cs, Api/Models/SubmitFeedbackRequest.cs, Api/Program.cs
- **Packages**: Swashbuckle.AspNetCore 7.2.0

#### 1.7 LLM Configuration ✅
- LlmSettings model with comprehensive options
- appsettings.json Llm section (disabled by default)
- Documentation: providers (Azure OpenAI, OpenAI, Local), cost estimates, troubleshooting
- **Files**: Worker/Models/LlmSettings.cs, Worker/appsettings.json
- **Documentation**: docs/llm-configuration.md

---

### 🔄 Phase 2: LLM Integration (IN PROGRESS)

#### 2.1 Service Interface & Implementation
**Status**: Not started  
**Estimated**: 1-2 hours

**Tasks**:
- [ ] Create `ILlmService` interface in Worker/Services/
  - `Task<string?> GenerateSummaryAsync(string content, CancellationToken ct)`
  - `Task<float[]?> GenerateEmbeddingAsync(string content, CancellationToken ct)`
- [ ] Implement `AzureOpenAIService` or `OpenAIService`
  - Add NuGet: `Azure.AI.OpenAI` or `Betalgo.OpenAI`
  - Configure from LlmSettings (Endpoint, ApiKey, ModelName)
  - Implement retry logic with exponential backoff
  - Add ActivitySource tracing for LLM calls
  - Handle rate limits and errors gracefully
- [ ] Register in Program.cs
  - `builder.Services.Configure<LlmSettings>(builder.Configuration.GetSection("Llm"))`
  - `builder.Services.AddScoped<ILlmService, AzureOpenAIService>()`
  - Conditional registration based on Enabled flag

**Files to Create**:
- Worker/Services/ILlmService.cs
- Worker/Services/AzureOpenAIService.cs (or OpenAIService.cs)

**Files to Modify**:
- Worker/Program.cs (service registration)
- Directory.Packages.props (add Azure.AI.OpenAI)

#### 2.2 EnrichAgent Integration
**Status**: Not started  
**Estimated**: 30 minutes

**Tasks**:
- [ ] Update EnrichAgent constructor to inject `ILlmService` and `IOptions<LlmSettings>`
- [ ] Add summarization logic:
  - Check if LlmSettings.Enabled && EnableSummarization
  - Call GenerateSummaryAsync for items without summaries
  - Store result in item.Summary
  - Log success/failure with item count
- [ ] Add embedding logic (optional):
  - Check if LlmSettings.Enabled && EnableEmbeddings
  - Call GenerateEmbeddingAsync
  - Store as JSON in item.EmbeddingJson
- [ ] Handle errors gracefully (continue pipeline if LLM fails)

**Files to Modify**:
- Worker/Agents/EnrichAgent.cs

#### 2.3 Testing & Validation
**Status**: Not started  
**Estimated**: 30 minutes

**Tasks**:
- [ ] Create appsettings.local.json with test credentials
- [ ] Enable LLM: Set Enabled=true, configure Endpoint/ApiKey
- [ ] Run worker with real feed data
- [ ] Verify summaries generated and saved to database
- [ ] Check ActivitySource traces in Aspire dashboard
- [ ] Monitor costs in Azure Portal (if using Azure OpenAI)
- [ ] Test with Enabled=false (should work as before, no API calls)

---

### 🧪 Phase 3: End-to-End Testing (PENDING)

#### 3.1 Feedback Workflow Test
**Status**: Not started  
**Estimated**: 30 minutes

**Tasks**:
- [ ] Insert test news items via SQL or run worker
- [ ] Submit feedback via API:
  ```bash
  curl -X POST http://localhost:5000/api/feedback/newsitem/1 \
    -H "Content-Type: application/json" \
    -d '{"userId":"test-user","feedbackType":"ThumbsUp"}'
  ```
- [ ] Verify feedback saved: `SELECT * FROM news_item_feedbacks;`
- [ ] Run worker again (re-score existing items)
- [ ] Verify UserFeedbackScore updated in news_item_scores table
- [ ] Check AnalyzeAgent logs for "Loaded feedback for X of Y items"
- [ ] Test update scenario (submit opposite feedback, verify 200 OK)
- [ ] Test keyword feedback endpoint similarly

#### 3.2 Integration Test
**Status**: Not started  
**Estimated**: 30 minutes

**Tasks**:
- [ ] Clear database (restart Aspire or manual DELETE)
- [ ] Run worker: Full pipeline from scratch
- [ ] Verify 7 stages complete successfully:
  - Ingest: All 7 sources (4 RSS + 3 arXiv)
  - Normalize: All items processed
  - Dedupe: Database + batch duplicates removed
  - Classify: Topics assigned
  - Enrich: Summaries generated (if LLM enabled)
  - Analyze: Scores calculated with feedback aggregation
  - Publish: Items + scores saved, Markdown generated
- [ ] Check output/news-{timestamp}.md
- [ ] Verify telemetry in Aspire dashboard
- [ ] Submit feedback, re-run worker, verify scores updated

---

### 📋 Phase 4: Future Enhancements (BACKLOG)

#### 4.1 Query API Endpoints
**Estimated**: 2-3 hours

- GET /api/news?topic={topic}&days={days} - Recent news items with filtering
- GET /api/news/{id} - Single news item with full details and score
- GET /api/trends?window={window} - Trending topics over time window
- GET /api/keywords - List all classification keywords with feedback stats

**Benefits**:
- Enable web UI consumption
- Support external integrations
- Provide data for analytics

#### 4.2 Web UI
**Estimated**: 4-6 hours

- Simple frontend for browsing news items (React/Blazor)
- Thumbs up/down buttons connected to feedback API
- Display ComputedTrendScore and individual score components
- Filter by topic, sort by recency/trend score
- Responsive design for mobile/desktop

**Technology Options**:
- Blazor Server (integrated with ASP.NET Core)
- React + Vite (separate SPA)
- Next.js (SSR for better SEO)

#### 4.3 Advanced Features
**Estimated**: 8-12 hours

- **Semantic Search**: Use embeddings for vector similarity search
- **User Preferences**: Per-user topic weights, source filters, notification settings
- **Email Digest**: Generate and send daily/weekly email summaries
- **Trend Detection**: 7d vs 30d momentum, entity deltas, cluster growth
- **Keyword Quality**: Improve keywords based on KeywordFeedback aggregation
- **Multi-language Support**: Translate summaries, support non-English feeds

#### 4.4 Operational Improvements
**Estimated**: 4-6 hours

- **Production Deployment**: Docker Compose, Kubernetes manifests, systemd timer
- **Monitoring Dashboards**: Grafana + Prometheus integration
- **Alert Rules**: Pipeline failures, high error rates, API quotas
- **Backup/Restore**: Automated database backups with point-in-time recovery
- **Performance Optimization**: Parallel processing, Redis caching, database indexing
- **Security**: Authentication (JWT), authorization (RBAC), rate limiting, HTTPS enforcement

---

## 📊 Current Status Summary

**Completed**: 7 major features (OpenTelemetry, Rate Limiting, Feedback System, Separated Scoring, arXiv Integration, Feedback API, LLM Configuration)

**In Progress**: LLM service implementation (ILlmService + provider implementations)

**Pending**: End-to-end testing (feedback workflow + integration tests)

**Total Estimated Remaining**: 3-4 hours for Phase 2-3

### System Health
- ✅ **Build**: Successful (1.7s, 0 errors, 0 warnings)
- ✅ **API**: Running at http://localhost:5000 with Swagger UI
- ✅ **Database**: Healthy (Postgres + pgAdmin running)
- ✅ **Worker**: Pipeline tested successfully (70 items → 2 new unique items)
- ✅ **Dashboard**: http://localhost:15000 (Aspire telemetry)

### Key Metrics
- **arXiv Integration**: 150 articles fetched across 3 queries, proper deduplication
- **Feedback API**: 2 endpoints, Swagger documented, 200/201/404 responses
- **Scoring**: 4 components (recency, topic, feedback, trend), proper FK relationships
- **Rate Limiting**: 4-hour intervals for arXiv, 1-hour default for RSS feeds

### Next Immediate Steps
1. **Implement ILlmService + AzureOpenAIService** (~1-2 hrs)
   - Create interface with summarization and embedding methods
   - Implement Azure OpenAI integration with retry logic
   - Register services conditionally based on LlmSettings.Enabled
2. **Integrate into EnrichAgent** (~30 min)
   - Inject ILlmService and configuration
   - Add conditional summarization logic
   - Handle errors gracefully
3. **Test Feedback Workflow** (~30 min)
   - Submit feedback via API
   - Re-run worker to verify score updates
   - Validate end-to-end integration

---

## 🔧 Development Commands

### Build & Test
```bash
# Build solution
dotnet build

# Run tests (when added)
dotnet test

# Clean build artifacts
dotnet clean
```

### Run Locally
```bash
# Run entire stack with Aspire
dotnet run --project src/Neuralium.AppHost

# Run worker standalone
dotnet run --project src/Neuralium.Worker

# Run API standalone
dotnet run --project src/Neuralium.Api
```

### Database Operations
```bash
# Add new migration
dotnet ef migrations add MigrationName --project src/Neuralium.Data --startup-project src/Neuralium.MigrationService

# Apply migrations (via Aspire - runs automatically)
# Or manually:
dotnet run --project src/Neuralium.MigrationService
```

### Testing Endpoints
```bash
# Check API health
curl http://localhost:5000/health

# View Swagger UI
open http://localhost:5000/swagger

# Submit news item feedback
curl -X POST http://localhost:5000/api/feedback/newsitem/1 \
  -H "Content-Type: application/json" \
  -d '{"userId":"test-user","feedbackType":"ThumbsUp"}'

# Submit keyword feedback
curl -X POST http://localhost:5000/api/feedback/keyword/1 \
  -H "Content-Type: application/json" \
  -d '{"userId":"test-user","feedbackType":"ThumbsDown"}'
```

---

## 📖 Documentation

- **Architecture**: See `/adr` folder for Architecture Decision Records
- **arXiv Integration**: [docs/arxiv-integration.md](docs/arxiv-integration.md)
- **LLM Configuration**: [docs/llm-configuration.md](docs/llm-configuration.md)
- **API Documentation**: http://localhost:5000/swagger (when running)
- **Copilot Instructions**: [.github/copilot-instructions.md](.github/copilot-instructions.md)
- **Agents Instructions**: [AGENTS.md](AGENTS.md) (Aspire-specific guidance)

---

**Inspired by**: [Microsoft Agent Framework Workflows Journey](https://singhrajeev.com/2026/01/18/microsoft-agent-framework-workflows-the-next-step-in-building-intelligent-multi-agent-ai-systems/)
