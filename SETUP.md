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
- **Fix**: Ensure PostgreSQL container is running: `docker ps | grep postgres`
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

**Inspired by**: [Microsoft Agent Framework Workflows Journey](https://singhrajeev.com/2026/01/18/microsoft-agent-framework-workflows-the-next-step-in-building-intelligent-multi-agent-ai-systems/)
