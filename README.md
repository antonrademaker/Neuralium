# Neuralium

**Neuralium** is a personal, open-source AI news and trend aggregation project created mostly with AI.

It continuously gathers, filters, and analyzes developments across the AI landscape — from enterprise platforms and research papers to open-source ecosystems and leadership perspectives — and distills them into daily insights and emerging trends.

Neuralium is designed to answer one core question:

> *What actually matters in AI right now — and what is starting to matter next?*

---

## ✨ What Neuralium Does

Neuralium runs as a **daily, automated agent** that:

- Aggregates AI-related news from trusted sources:
  - Official vendor blogs (Azure, AWS, GitHub, etc.)
  - Scientific research (arXiv)
  - Open-source ecosystems (GitHub releases, Hugging Face)
  - Community signals (Reddit, Medium)
- Normalizes and deduplicates content across sources
- Classifies items into well-defined AI topics
- Identifies **trends and momentum** over time
- Produces a **daily digest** and **trend report** in human-readable form

The focus is **signal over noise**, with explicit attention to:
- Agents & agentic systems
- GitHub Copilot & developer tooling
- Enterprise AI platforms (Azure AI Foundry, AWS Bedrock)
- Research breakthroughs
- Open source innovation
- Leadership, governance, and strategy

---

## 🧠 Philosophy

Neuralium is built with the following principles:

- **Idempotent by design**  
  Safe to run daily, repeatable, deterministic.

- **Feeds over scraping**  
  Prefer RSS/Atom and official APIs over fragile scraping.

- **Explainable trends**  
  Trends are based on observable signals, not opaque hype scores.

- **Enterprise-grade discipline**  
  Clean architecture, explicit contracts, strong observability.

- **Human-first output**  
  Clear summaries and context, not raw data dumps.

---

## 🏗️ Architecture (High-Level)

Neuralium follows an **agent-based workflow architecture** inspired by [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/):

**Pipeline Stages** (Each stage is an autonomous agent):

1. **Ingest Agent** – Fetch items from configured feeds  
2. **Normalize Agent** – Canonical URLs, timestamps, source identity  
3. **Dedupe Agent** – Prevent cross-source duplication  
4. **Classify Agent** – Topic tagging (hybrid: keywords + optional LLM)  
5. **Enrich Agent** – Summaries, entities, embeddings (optional)  
6. **Analyze Agent** – Trend detection (7d vs 30d momentum)  
7. **Publish Agent** – Daily digest and trend report

**Orchestration Pattern**: Workflow-as-Agent with sequential execution (see [ADR-0001](adr/0001-agent-workflow-orchestration.md))

The system runs as a **Worker Service** orchestrated by .NET Aspire, triggered daily via systemd timer or manually for development.

For detailed architecture decisions, see the [/adr](adr/) folder.

---

## ⚙️ Technology Stack

- **Language:** C# (.NET 10)
- **Runtime:** Linux containers (Podman)
- **Orchestration:** .NET Aspire
- **Execution:** Worker Service (scheduled via systemd timer)
- **Storage:** PostgreSQL (dev & prod) with Aspire.Npgsql integration
- **Configuration:** appsettings.json + environment variables
- **AI/LLM:** Optional hybrid classification (keywords + LLM refinement)
- **Development:** VS Code + C# Dev Kit + GitHub Copilot

Neuralium is intentionally **cloud-agnostic** and runs fully locally.

**Ke� Getting Started

See **[SETUP.md](SETUP.md)** for complete setup instructions with tiny, validated steps.

**Quick Start** (after prerequisites):
```bash
# Clone repository
git clone https://github.com/your-username/neuralium.git
cd neuralium

# Restore packages
dotnet restore

# Run with Aspire (starts Worker + API + PostgreSQL)
cd src/Neuralium.AppHost
dotnet run
# or: aspire run

# View Aspire Dashboard (opens automatically)
# https://localhost:15243
```

**Prerequisites**:
- .NET 10 SDK
- Docker Desktop or Podman
- VS Code with C# Dev Kit (recommended)

**Architecture Decisions**:
See [/adr](adr/) folder for detailed architectural decision records covering:
- Agent-based workflow orchestration
- Hybrid LLM classification strategy
- PostgreSQL production-ready storage
- Aspire Worker Service execution model

---

## 📁 Project Structure

```
Neuralium/
├── adr/                          # Architecture Decision Records
│   ├── 0001-agent-workflow-orchestration.md
│   ├── 0002-hybrid-llm-classification.md
│   ├── 0003-postgresql-production-ready-storage.md
│   └── 0004-aspire-worker-service-execution.md
├── src/
│   ├── Neuralium.AppHost/        # Aspire orchestration
│   ├── Neuralium.Worker/         # Pipeline agents & execution
│   └── Neuralium.Api/            # Read-only query API (future)
├── SETUP.md                      # Detailed setup guide
├── README.md                     # This file
└── LICENSE                       # BSD 3-Clause

```

---

## �y Integrations**:
- [Aspire.Hosting.PostgreSQL](https://learn.microsoft.com/en-us/dotnet/aspire/database/postgresql-integration) for database
- [Aspire.Npgsql](https://learn.microsoft.com/en-us/dotnet/aspire/database/postgresql-component) for data access
- Entity Framework Core for ORM
- OpenTelemetry for observability

---

## 📄 Project Status

🚧 **Active personal hobby project**

- Architecture-first
- Incrementally built
- No stability guarantees
- Interfaces and data models may evolve

Contributions are welcome, but expectations are intentionally modest.

---

## 📜 License

Neuralium is licensed under the **BSD 3-Clause License**.

You are free to use, modify, and redistribute this software — including for commercial purposes — **provided that attribution to Neuralium is retained**, as required by the license.

See [`LICENSE`](./LICENSE) for details.

---

## 🔖 Attribution

When referencing or redistributing this project, please retain attribution to:

**Neuralium — AI News & Trend Aggregation**  
<https://github.com/your-username/neuralium>

---

## 🧭 Disclaimer

Neuralium is a personal project.  
It is **not affiliated with or endorsed by** Microsoft, Amazon, Google, Anthropic, GitHub, or any other vendor referenced by the aggregated sources.

All trademarks and product names belong to their respective owners.

---

## 🚀 Why “Neuralium”?

The name *Neuralium* combines:
- *Neural* — modern artificial intelligence
- *-ium* — a classical suffix suggesting substance and structure

It reflects the project’s intent:  
**a foundational material for understanding the evolving AI landscape.**
