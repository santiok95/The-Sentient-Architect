# The Sentient Architect — Project Context

## Vision

The Sentient Architect is an AI-powered developer knowledge management system built in .NET 9. It unifies four interconnected pillars into a single ecosystem where knowledge, code analysis, architectural reasoning, and trend monitoring feed into each other.

The core insight: these are not four separate apps — they are layers of the same system. Everything converges on a semantic knowledge base (the "Semantic Brain"), and specialized agents consume and enrich that base.

## Target User

Initially a single developer (personal tool), architected to scale to a small team. All entities include `UserId` and `TenantId` from day one to avoid future schema migrations.

## The Four Pillars

### Pillar 1 — Semantic Brain (central hub)
The knowledge base. Receives resources (articles, notes, docs, repository links), processes them, indexes them semantically, and enables natural language search by meaning, not keywords.

Key behaviors:
- Accepts multiple content types: articles, notes, documentation, repository references
- Automatically extracts, chunks, and vectorizes content on ingestion
- Provides semantic search (RAG pattern) for natural language queries
- All other pillars feed INTO this base and the Architecture Consultant reads FROM it

### Pillar 2 — Architecture Consultant (reasoning agent)
A specialized reasoning agent that consumes data from the Semantic Brain. It conducts multi-turn architectural consultations, remembering context within a session (Conversation) and across sessions (UserProfile).

Key behaviors:
- Multi-turn conversations with context retention
- Reads UserProfile before each session (known stack, patterns, constraints)
- Generates ConversationSummary every N messages to manage token costs
- Retrieves relevant KnowledgeItems via RAG to ground recommendations
- Produces ArchitectureRecommendations as structured, searchable outputs

### Pillar 3 — Code Guardian (auditor)
Analyzes repositories with two trust levels:
- **External repos**: full analysis (metadata + static analysis + security scan + dependency audit)
- **Internal repos**: quality-focused analysis (patterns, complexity, tech debt, outdated dependencies — relaxed security since you trust the code, but still scans third-party dependencies)

Key behaviors:
- Clones and analyzes repos asynchronously (background job)
- Uses Roslyn for C# static analysis
- Checks dependencies for known vulnerabilities
- Generates AnalysisReport with scored findings
- Results are stored as KnowledgeItems → searchable in the Semantic Brain

### Pillar 4 — Trends Radar (sentinel)
Background process that monitors technology sources and detects trends relevant to the user's stack. Last to implement — most complex.

Key behaviors:
- Periodic scraping/monitoring of configured sources
- Evaluates trend relevance against user's UserProfile
- Tracks trend evolution over time via TrendSnapshots
- Can store relevant trends as KnowledgeItems in the Semantic Brain

## Architecture

**Pattern**: Clean Architecture (4 layers)

### Domain Layer (no external dependencies)
- Entities, Value Objects, Enums
- Repository interfaces (`IKnowledgeRepository`, `IVectorStore`, etc.)
- Service interfaces (`IEmbeddingService`, `ICodeAnalyzer`, `ITrendScanner`)
- Domain rules and validation

### Application Layer (orchestration)
- Use Cases: `IngestKnowledgeItemUseCase`, `QueryKnowledgeBaseUseCase`, `AnalyzeRepositoryUseCase`, `GetArchitectureAdviceUseCase`, `ScanTrendsUseCase`
- DTOs and Mappers
- Semantic Kernel agent orchestration logic (not configuration)

### Infrastructure Layer (external world)
- `EFCore/` — DbContext, entity configurations, migrations (PostgreSQL)
- `VectorDB/` — `PgVectorStore` implementation of `IVectorStore` (pgvector initially, abstractable to Qdrant later)
- `AIServices/` — Semantic Kernel configuration, model connections, `IEmbeddingService` implementation
- `CodeAnalysis/` — Roslyn analyzers, NuGet audit, `ICodeAnalyzer` implementation
- `ExternalAPIs/` — GitHub API client, trend source clients
- `BackgroundJobs/` — Hosted services for async processing (ingestion pipeline, analysis, radar scans)

### Presentation Layer (API)
- REST API controllers (or Minimal APIs)
- SignalR hubs (LLM response streaming, progress notifications)
- Middleware (auth, error handling, rate limiting)
- Health checks

## Agent Design (Semantic Kernel)

The system uses 3 conversational agents (Semantic Kernel ChatCompletionAgent) + 1 background job. Each agent has its own Kernel instance with specific plugins.

### Agent 1 — Knowledge Agent (Semantic Brain)
The most-used agent. Handles everything that enters or exits the knowledge base.

Plugins:
- **SearchPlugin**: Receives natural language query → generates embedding → searches pgvector → fetches full metadata from PostgreSQL → returns ranked results with context. This plugin is also called by the Consultant Agent when it needs context (shared plugin).
- **IngestPlugin**: Receives content (URL, text, repo link) → determines type → triggers async ingestion pipeline (extract → chunk → embed → store). Returns tracking ID immediately, doesn't wait for completion.

### Agent 2 — Consultant Agent (Architecture Consultant)
The most intelligent agent. Reasons about architecture using context from the Knowledge Agent.

Plugins:
- **ProfilePlugin**: Reads UserProfile before each response (stack, patterns, constraints). After responding, detects patterns for ProfileUpdateSuggestion (e.g., "user mentioned Rust 4 times recently").
- **SummaryPlugin**: Monitors conversation length. When token count approaches threshold, generates ConversationSummary covering old messages. Manages the rolling context window.

Key behavior: The Consultant does NOT have its own search — it calls the Knowledge Agent's SearchPlugin to retrieve context. This avoids duplicating search logic and ensures a single source of truth.

Consultant's LLM context assembly:
1. UserProfile (persistent cross-session context)
2. Latest ConversationSummary (compressed history)
3. Last N recent messages (detailed recent context)
4. Retrieved KnowledgeItems via SearchPlugin (RAG context)

### Agent 3 — Guardian Agent (Code Guardian)
Activated when a repository is submitted for analysis. Not conversational — runs as a task.

Plugins:
- **RoslynPlugin**: Static analysis of C# code (cyclomatic complexity, cognitive complexity, code smells, anti-patterns)
- **DependencyPlugin**: Scans NuGet packages for known vulnerabilities, outdated versions, license incompatibilities
- **GitMetadataPlugin**: Reads repo metadata (stars, issues, commits, contributors, last activity, README quality)

When analysis completes, stores results as KnowledgeItem via the Knowledge Agent's IngestPlugin, making analysis findings searchable in the Semantic Brain.

### Trends Radar — Background Job (NOT an agent)
The Radar is not a conversational agent — it's an `IHostedService` running on a timer. It doesn't need multi-turn reasoning or conversation management. It scrapes configured sources, evaluates relevance against UserProfile, generates summaries via LLM, and stores relevant trends via IngestPlugin. Implemented as standard .NET background service with Semantic Kernel for LLM calls (summarization, relevance scoring) but without the Agent Framework overhead.

### Inter-Agent Communication
Agents communicate through shared plugins, not direct agent-to-agent messaging. The SearchPlugin and IngestPlugin act as the shared interface — any agent can search or store knowledge through them. This keeps the architecture simple and avoids complex orchestration patterns.

Auto function calling (`FunctionChoiceBehavior.Auto()`) lets the LLM decide when to invoke plugins based on their descriptions. The descriptions must be clear and specific for this to work reliably.

## Key Technical Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Runtime | .NET 9 stable | Avoid preview bugs; trivial migration to .NET 10 LTS later |
| Database | PostgreSQL + pgvector | Single DB for relational + vector data; simpler ops for initial scale |
| Vector abstraction | `IVectorStore` interface | Swap to Qdrant/Milvus later without touching Application/Domain |
| AI orchestration | Semantic Kernel | Native .NET, Microsoft-backed, plugin architecture fits our agent design |
| Code analysis | Roslyn (C#) + dependency scanners | Static analysis without code execution — no sandbox needed |
| Architecture pattern | Clean Architecture | Decoupled layers allow infrastructure changes without business logic rewrites |
| Multi-tenancy | `UserId` + `TenantId` on all entities | Baked in from day one; initially TenantId == UserId |
| Authentication | ASP.NET Identity + JWT | Identity handles user management, hashing, roles; JWT for API auth |
| Authorization | Role-based (Admin, User) | Admin: full access + content review. User: personal ingestion + queries + publication requests |
| Content scope | Personal + Shared with approval | Users create personal content; publication to shared space requires Admin approval via ContentPublishRequest |
| Conversation management | Rolling summary + UserProfile | ConversationSummary every N messages to control token costs; UserProfile for cross-session memory |
| Profile updates | Suggestion-based (not automatic) | AI suggests profile changes → user confirms → prevents "identity pollution" from exploratory chats |
| Repo trust levels | Internal vs External | External gets full security scan; Internal focuses on quality/debt |

## Content Scope Model

Knowledge exists in two scopes:
- **Personal**: `TenantId = userId`. Only visible to the owning user. Created by default when any user ingests content.
- **Shared**: `TenantId = team/org tenantId`. Visible to all users in the tenant. Content reaches this scope only through Admin approval.

**Why this matters**: Prevents low-quality content from polluting the shared knowledge base. A junior developer can explore and save whatever they want in their personal space, but the team's shared brain stays curated. The Admin acts as quality gatekeeper.

**Search behavior**: When a user queries, SearchPlugin returns results from their personal scope + the shared scope. Admin can query all scopes.

## Data Flow Patterns

### Ingestion Flow (async)
1. User submits resource (URL, text, repo link)
2. System determines type and creates KnowledgeItem (status: Pending)
3. Background job: extract content, generate summary, auto-tag
4. Background job: chunk content → generate embeddings → store in pgvector
5. If repo: clone → run Code Guardian analysis → store AnalysisReport
6. Status → Completed (or Failed with error details)

### Query Flow (RAG — semi-synchronous)
1. User asks a question in natural language
2. Generate embedding of the question
3. Search pgvector for top-N similar chunks → get KnowledgeItem IDs
4. Fetch full metadata from PostgreSQL for those items
5. Assemble context (retrieved items + UserProfile + ConversationSummary)
6. Send to LLM via Semantic Kernel
7. Stream response back via SignalR
8. Store ConversationMessage with RetrievedContextIds for traceability

### Analysis Flow (async)
1. User submits repo URL with trust level (Internal/External)
2. Background job: clone repo to temp directory
3. Run analyzers based on trust level
4. Generate AnalysisReport with scored findings
5. Store results; create/update KnowledgeItem with analysis summary
6. If security issues found on External repo → flag and notify user

## Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| LLM API costs escalate | ConversationSummary to reduce token usage; token tracking per message; configurable model selection |
| pgvector performance at scale | Abstract behind IVectorStore; monitor query times; migrate to dedicated Vector DB if needed |
| Malicious code in external repos | Static analysis ONLY — never execute repo code; sandboxed temp directory for cloning; delete after analysis |
| Embedding model changes | KnowledgeEmbedding separated from KnowledgeItem; re-embedding is a bulk operation that doesn't touch source data |
| Stale knowledge base | Trends Radar provides freshness; user can trigger re-analysis of repos; content freshness indicators |
| Scope creep | Implement pillar by pillar: Semantic Brain → Consultant → Guardian → Radar |

## Implementation Order

1. **Semantic Brain** (foundation — everything depends on this)
   - Domain entities + EF Core setup
   - pgvector integration
   - Ingestion pipeline (articles/notes)
   - Basic RAG query flow

2. **Architecture Consultant** (highest user value after Brain)
   - Conversation management
   - UserProfile + suggestion system
   - ConversationSummary compaction
   - Semantic Kernel agent with RAG context

3. **Code Guardian** (extends the Brain with repo analysis)
   - Repository cloning + metadata extraction
   - Roslyn static analysis
   - Dependency vulnerability scanning
   - AnalysisReport generation + storage as KnowledgeItem

4. **Trends Radar** (background enrichment — last priority)
   - Source configuration
   - Periodic scanning jobs
   - Relevance scoring against UserProfile
   - TrendSnapshot tracking