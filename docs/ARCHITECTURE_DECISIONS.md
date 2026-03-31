# The Sentient Architect — Architecture Decisions

## Layer Structure

```
SentientArchitect/
├── src/
│   ├── SentientArchitect.Domain/          # Entities, ValueObjects, IEntity/BaseEntity, Enums
│   ├── SentientArchitect.Application/     # Interfaces, Result pattern, Use Cases (Vertical Slice)
│   ├── SentientArchitect.Data/            # ApplicationContext, Entity Configurations, ApplicationUser
│   ├── SentientArchitect.Data.Postgres/   # pgvector configs, HNSW, Migrations, DI (strictly DB driver config)
│   ├── SentientArchitect.Infrastructure/  # Identity services (AddIdentityCore), TokenService, Seeder, DI
│   └── SentientArchitect.API/             # Minimal API endpoints, SignalR, Middleware
├── tests/
│   ├── SentientArchitect.UnitTests/
│   └── SentientArchitect.IntegrationTests/
└── docs/
```

### Dependency Rule
Domain ← Application ← Data ← Data.Postgres / Infrastructure ← API. No layer references a layer above it. Domain has ZERO external dependencies (no NuGet packages). Interfaces live in Application (the consumer), implementations in Data/Infrastructure.

## Domain Entities — Complete Model

### Shared / Cross-cutting

#### User (inherits IdentityUser\<Guid\>)
Uses ASP.NET Identity for authentication. Custom fields added on top.
| Property | Type | Notes |
|----------|------|-------|
| *(inherited from IdentityUser)* | | Id (Guid), Email, UserName, PasswordHash, EmailConfirmed, LockoutEnd, etc. |
| DisplayName | string | Required, custom field |
| TenantId | Guid | Multi-tenancy key; initially == UserId for solo users |
| CreatedAt | DateTime | UTC |

**Identity table renaming**: AspNetUsers → Users, AspNetRoles → Roles, AspNetUserRoles → UserRoles, AspNetRoleClaims → RoleClaims, AspNetUserClaims → UserClaims, AspNetUserLogins → UserLogins, AspNetUserTokens → UserTokens.

**Roles**:
- `Admin` — full access: shared ingestion, content review, user management, all queries
- `User` — personal ingestion, queries (personal + shared), request publication to shared space

#### UserProfile
Persistent, accumulative context. Read by the Architecture Consultant before every session.
| Property | Type | Notes |
|----------|------|-------|
| Id | Guid | PK |
| UserId | Guid | FK → User (1:1) |
| PreferredStack | List\<string\> | JSONB, e.g. ["C#", ".NET 9", "PostgreSQL"] |
| KnownPatterns | List\<string\> | JSONB, e.g. ["CQRS", "Clean Architecture"] |
| InfrastructureContext | string | Free text: "microservices on Azure with AKS" |
| TeamSize | string | "solo" / "small" / "medium" / "large" |
| ExperienceLevel | string | "junior" / "mid" / "senior" / "lead" |
| CustomNotes | string | Anything else the user wants the system to know |
| LastUpdatedAt | DateTime | UTC |

**Update mechanism**: AI detects patterns in conversations → creates ProfileUpdateSuggestion → user accepts/rejects. Never auto-updates without confirmation.

#### ProfileUpdateSuggestion
| Property | Type | Notes |
|----------|------|-------|
| Id | Guid | PK |
| UserId | Guid | FK → User |
| Field | string | Which UserProfile field to update |
| SuggestedValue | string | Proposed new value |
| Reason | string | Why the AI suggests this ("Mentioned Rust in 4 recent conversations") |
| Status | enum | Pending / Accepted / Rejected |
| DetectedInConversationId | Guid? | FK → Conversation (nullable) |
| CreatedAt | DateTime | UTC |

#### ContentPublishRequest
Controls quality of shared knowledge base. Users request publication → Admin reviews.
| Property | Type | Notes |
|----------|------|-------|
| Id | Guid | PK |
| KnowledgeItemId | Guid | FK → KnowledgeItem |
| RequestedByUserId | Guid | FK → User (who requested) |
| ReviewedByUserId | Guid? | FK → User (which Admin reviewed, null if pending) |
| Status | enum | Pending / Approved / Rejected |
| RequestReason | string? | Why the user thinks this is valuable for the team |
| RejectionReason | string? | Admin feedback on why it was rejected |
| CreatedAt | DateTime | UTC |
| ReviewedAt | DateTime? | UTC, null if pending |

**Publication flow**:
1. User ingests content → stored as personal (`TenantId = userId`)
2. User requests "publish to team" → creates ContentPublishRequest (Pending)
3. Admin reviews → Approved: KnowledgeItem.TenantId changes to shared tenantId, re-indexed in shared space. Rejected: stays personal, user gets feedback.

**Search scope logic**:
- User queries: returns personal items (TenantId == userId) + shared items (TenantId == team tenantId)
- Admin queries: returns all items across all scopes

#### TokenUsageTracker
Prevents runaway LLM costs. Tracks daily token consumption per user.
| Property | Type | Notes |
|----------|------|-------|
| Id | Guid | PK |
| UserId | Guid | FK → User |
| TenantId | Guid | For tenant-level quota enforcement |
| Date | DateOnly | The day being tracked |
| TokensConsumed | long | Total tokens used this day (input + output) |
| DailyQuota | long | Configurable limit; default from tenant settings |
| QuotaAction | enum | Warn / DegradeModel / Block |
| LastUpdatedAt | DateTime | UTC |

**Behavior**: Incremented after every LLM call. When TokensConsumed approaches DailyQuota (80%), system warns the user. At 100%, based on QuotaAction: DegradeModel switches from GPT-4o to GPT-4o-mini; Block stops LLM calls until next day. Admin can override per-user.

### Pillar 1 — Semantic Brain

#### KnowledgeItem
Central entity. All knowledge flows through this.
| Property | Type | Notes |
|----------|------|-------|
| Id | Guid | PK |
| UserId | Guid | FK → User |
| TenantId | Guid | Multi-tenancy |
| Title | string | Required |
| SourceUrl | string? | Nullable — manual notes have no URL |
| OriginalContent | string | Raw extracted text |
| Summary | string? | AI-generated at ingestion time |
| Type | enum | Article / Note / Documentation / RepositoryReference / TrendReport |
| Tags | → Tag (M:N) | Via KnowledgeItemTag join table |
| ProcessingStatus | enum | Pending / Processing / Completed / Failed |
| CreatedAt | DateTime | UTC |
| UpdatedAt | DateTime | UTC |

**Storage policy for OriginalContent by Type**:
- Article / Note / Documentation: full raw text stored (typically small, <50KB)
- RepositoryReference: NOT full source code. Stores: processed README + AI-generated project summary + code fragments that triggered analysis findings. Full code accessed via GitUrl on demand.
- TrendReport: AI-generated summary with source citations

#### KnowledgeEmbedding
One KnowledgeItem → many embeddings (one per chunk).
| Property | Type | Notes |
|----------|------|-------|
| Id | Guid | PK |
| KnowledgeItemId | Guid | FK → KnowledgeItem |
| ChunkIndex | int | Position within the document |
| ChunkText | string | The text of this specific chunk |
| Embedding | vector | pgvector type; dimensionality depends on model |
| CreatedAt | DateTime | UTC |

**Why separate from KnowledgeItem?**
1. Long documents are split into 500-1000 token chunks for precise embeddings
2. Semantic search returns specific chunks, not whole documents
3. Re-embedding (model change) only touches this table

#### Tag
| Property | Type | Notes |
|----------|------|-------|
| Id | Guid | PK |
| Name | string | Unique within category |
| Category | enum | Technology / Pattern / Language / Framework / Concept / Custom |
| IsAutoGenerated | bool | AI-generated vs user-created |

#### KnowledgeItemTag (join table)
| Property | Type | Notes |
|----------|------|-------|
| KnowledgeItemId | Guid | FK → KnowledgeItem |
| TagId | Guid | FK → Tag |

### Pillar 2 — Architecture Consultant

#### Conversation
A consultation session.
| Property | Type | Notes |
|----------|------|-------|
| Id | Guid | PK |
| UserId | Guid | FK → User |
| Title | string | AI-generated from first message |
| Objective | string? | What the user wants to solve |
| Status | enum | Active / Completed / Archived |
| CreatedAt | DateTime | UTC |
| LastMessageAt | DateTime | UTC |

#### ConversationMessage
| Property | Type | Notes |
|----------|------|-------|
| Id | Guid | PK |
| ConversationId | Guid | FK → Conversation |
| Role | enum | User / Assistant / System |
| Content | string | Message text |
| RetrievedContextIds | Guid[] | KnowledgeItem IDs used as RAG context for this response |
| TokensUsed | int? | For cost tracking |
| CreatedAt | DateTime | UTC |

#### ConversationSummary
Rolling summary to manage token costs in long sessions.
| Property | Type | Notes |
|----------|------|-------|
| Id | Guid | PK |
| ConversationId | Guid | FK → Conversation |
| SummaryText | string | Compressed summary of covered messages |
| CoveredMessageRange | string | "Messages 1-15" — human-readable range |
| KeyDecisions | string[] | Decisions made so far in this conversation |
| OpenQuestions | string[] | Unresolved points |
| CreatedAt | DateTime | UTC |

**How it works**: Every N messages (or when token count exceeds threshold), the system generates a summary covering old messages. The LLM context becomes: UserProfile + latest ConversationSummary + last N recent messages. Old messages are still stored but not sent to the LLM.

#### ArchitectureRecommendation
Structured output from the Consultant, searchable separately from chat history.
| Property | Type | Notes |
|----------|------|-------|
| Id | Guid | PK |
| ConversationId | Guid | FK → Conversation |
| Problem | string | What problem this solves |
| RecommendedPatterns | string[] | e.g. ["CQRS", "Event Sourcing"] |
| ProposedStack | string[] | e.g. ["RabbitMQ", "MassTransit"] |
| TradeOffs | string | Free text explaining trade-offs |
| DiagramUrl | string? | If a diagram was generated |
| Confidence | enum | High / Medium / Low |
| CreatedAt | DateTime | UTC |

### Pillar 3 — Code Guardian

#### RepositoryInfo
Extends KnowledgeItem with repo-specific data (TPT inheritance pattern).
| Property | Type | Notes |
|----------|------|-------|
| Id | Guid | PK |
| KnowledgeItemId | Guid | FK → KnowledgeItem (1:0..1) |
| GitUrl | string | Repository clone URL |
| DefaultBranch | string | main / master / etc |
| PrimaryLanguage | string | Detected primary language |
| LastCommitDate | DateTime? | From Git metadata |
| Stars | int? | GitHub stars (if available) |
| OpenIssues | int? | Open issue count |
| License | string? | MIT / Apache / etc |
| TrustLevel | enum | Internal / External |
| LastAnalyzedAt | DateTime? | When last analysis ran |

#### AnalysisReport
One repo can have multiple reports over time (re-analysis).
| Property | Type | Notes |
|----------|------|-------|
| Id | Guid | PK |
| RepositoryInfoId | Guid | FK → RepositoryInfo |
| AnalysisType | enum | Full / SecurityOnly / QualityOnly / DependenciesOnly |
| OverallHealthScore | decimal | 0-100 |
| SecurityScore | decimal | 0-100 |
| QualityScore | decimal | 0-100 |
| MaintainabilityScore | decimal | 0-100 |
| ExecutedAt | DateTime | UTC |
| AnalysisDurationSeconds | int | Performance tracking |

**Versioning**: Multiple AnalysisReports per RepositoryInfo enables tracking improvement over time ("Security score went from 60 to 85 in 3 months").

#### AnalysisFinding
Individual issue found during analysis.
| Property | Type | Notes |
|----------|------|-------|
| Id | Guid | PK |
| AnalysisReportId | Guid | FK → AnalysisReport |
| Severity | enum | Critical / High / Medium / Low / Info |
| Category | enum | Security / Performance / CodeSmell / Dependency / Pattern / TechDebt |
| Title | string | Short description: "Vulnerable dependency: Newtonsoft.Json < 13.0" |
| Description | string | Detailed explanation |
| FilePath | string? | Where in the repo |
| Recommendation | string | What to do about it |
| IsResolved | bool | User can mark as resolved |

### Pillar 4 — Trends Radar

#### TechnologyTrend
| Property | Type | Notes |
|----------|------|-------|
| Id | Guid | PK |
| Name | string | "Aspire", "HTMX", "Semantic Kernel" |
| Category | enum | Language / Framework / Tool / Pattern / Platform / Library |
| TractionLevel | enum | Emerging / Growing / Mainstream / Declining |
| RelevanceScore | decimal | 0-100, based on user's stack/profile |
| Summary | string | AI-generated summary |
| Sources | string[] | Source URLs |
| KnowledgeItemId | Guid? | FK → KnowledgeItem (optional — stored in Brain if relevant enough) |
| FirstDetectedAt | DateTime | UTC |
| LastUpdatedAt | DateTime | UTC |

#### TrendSnapshot
Point-in-time capture for tracking evolution.
| Property | Type | Notes |
|----------|------|-------|
| Id | Guid | PK |
| TrendId | Guid | FK → TechnologyTrend |
| TractionLevel | enum | Same enum as TechnologyTrend |
| MentionCount | int | Mentions across sources in this period |
| SentimentScore | decimal | -1.0 to 1.0 |
| SnapshotDate | DateTime | UTC |

## Key Relationships

- User 1:1 UserProfile
- User 1:N KnowledgeItem
- User 1:N Conversation
- User 1:N ProfileUpdateSuggestion
- User 1:N ContentPublishRequest (as requester)
- User 1:N ContentPublishRequest (as reviewer)
- KnowledgeItem 1:N KnowledgeEmbedding
- KnowledgeItem M:N Tag (via KnowledgeItemTag)
- KnowledgeItem 1:0..1 RepositoryInfo (TPT — extends if type is RepositoryReference)
- KnowledgeItem 1:N ContentPublishRequest
- RepositoryInfo 1:N AnalysisReport
- AnalysisReport 1:N AnalysisFinding
- Conversation 1:N ConversationMessage
- Conversation 1:N ConversationSummary
- Conversation 1:N ArchitectureRecommendation
- TechnologyTrend 1:N TrendSnapshot
- TechnologyTrend 0..1:1 KnowledgeItem (optional storage in Brain)

## Semantic Kernel Agent Architecture

### Agents (ChatCompletionAgent instances)

| Agent | Kernel Plugins | Responsibility |
|-------|---------------|----------------|
| Knowledge Agent | SearchPlugin, IngestPlugin | Ingestion pipeline, semantic search, RAG retrieval |
| Consultant Agent | ProfilePlugin, SummaryPlugin + shared SearchPlugin | Multi-turn architecture consultations with context |
| Guardian Agent | RoslynPlugin, DependencyPlugin, GitMetadataPlugin | Repository static analysis and scoring |

### Shared Plugin Pattern
SearchPlugin and IngestPlugin are registered as shared services. The Consultant Agent and Guardian Agent call them through their Kernel's plugin registry. This avoids duplicating search/storage logic and ensures all agents interact with the same knowledge base.

### Plugin Specifications

#### SearchPlugin (Knowledge Agent — shared)
```
[KernelFunction] SearchByMeaning(string query, int maxResults = 5)
→ Generates embedding → pgvector similarity search → fetches metadata → returns ranked KnowledgeItem summaries with IDs

[KernelFunction] SearchByTag(string[] tags, int maxResults = 10)
→ Filters KnowledgeItems by tags → returns matches
```

#### IngestPlugin (Knowledge Agent — shared)
```
[KernelFunction] IngestContent(string title, string content, string? sourceUrl, string type)
→ Creates KnowledgeItem (Pending) → queues background processing → returns tracking ID

[KernelFunction] IngestRepository(string gitUrl, string trustLevel)
→ Creates KnowledgeItem + RepositoryInfo → queues clone + analysis → returns tracking ID
```

#### ProfilePlugin (Consultant Agent)
```
[KernelFunction] GetUserProfile()
→ Returns current UserProfile (stack, patterns, constraints, notes)

[KernelFunction] SuggestProfileUpdate(string field, string value, string reason)
→ Creates ProfileUpdateSuggestion (Pending) → user confirms later
```

#### SummaryPlugin (Consultant Agent)
```
[KernelFunction] GetConversationContext(Guid conversationId)
→ Returns latest ConversationSummary + recent messages (within token budget)

[KernelFunction] CompactConversation(Guid conversationId)
→ Generates summary of old messages → stores ConversationSummary → trims context
```

#### RoslynPlugin (Guardian Agent)
```
[KernelFunction] AnalyzeCodeQuality(string repoPath)
→ Runs Roslyn analyzers → returns findings (complexity, smells, patterns)
```

#### DependencyPlugin (Guardian Agent)
```
[KernelFunction] ScanDependencies(string repoPath, string trustLevel)
→ Reads package files → checks vulnerability DBs → returns findings
```

#### GitMetadataPlugin (Guardian Agent)
```
[KernelFunction] ExtractMetadata(string repoPath, string? gitUrl)
→ Reads git history + GitHub API → returns metadata (stars, issues, activity)
```

### Trends Radar (IHostedService, not an agent)
Runs as `TrendScannerBackgroundService : BackgroundService` on configurable timer (e.g., daily). Uses Semantic Kernel for LLM calls (summarization, relevance scoring) but not the Agent Framework. Stores results via IngestPlugin.

### Operational Controls

**Guardian concurrency**: `CodeAnalysisBackgroundService` uses `SemaphoreSlim(maxConcurrent: 1)` (configurable). Each analysis has a `CancellationToken` with timeout (default: 10 minutes). Queue system for multiple pending analyses — FIFO processing.

**Token quota enforcement**: Every LLM call (agent response, embedding generation, summarization) increments `TokenUsageTracker`. Middleware checks quota before processing agent requests. At 80%: warning notification. At 100%: action based on `QuotaAction` setting (Warn/DegradeModel/Block).

### Agent Instantiation Pattern (Infrastructure Layer)
```csharp
// Each agent gets its own Kernel with specific plugins
var knowledgeKernel = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion(modelId, apiKey)
    .Build();
knowledgeKernel.Plugins.Add(KernelPluginFactory.CreateFromObject(searchPlugin));
knowledgeKernel.Plugins.Add(KernelPluginFactory.CreateFromObject(ingestPlugin));

var knowledgeAgent = new ChatCompletionAgent
{
    Name = "KnowledgeAgent",
    Instructions = "You are a knowledge management assistant...",
    Kernel = knowledgeKernel,
    Arguments = new KernelArguments(
        new OpenAIPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() })
};
```

## EF Core Strategy

- **Inheritance**: Table-Per-Type (TPT) for KnowledgeItem → RepositoryInfo
- **Fluent API** over attributes for all configurations
- **String collections** (PreferredStack, KnownPatterns, etc.) stored as PostgreSQL `jsonb`
- **Vector type** via `Pgvector.EntityFrameworkCore` NuGet package
- **Indexes**: HNSW index on KnowledgeEmbedding.Embedding for fast similarity search
- **Enums**: Stored as string via `.HasConversion<string>().HasMaxLength(50)` for readability and refactoring safety
- **Guid PKs**: Generated client-side with `Guid.NewGuid()` or sequential GUIDs for index performance
- **UTC timestamps**: All DateTime properties stored as UTC; conversion in EF Core value converters

## Architecture Decision Records (ADRs)

### ADR-001: Hardcoded vector(1536) for Embedding Column

**Status**: Accepted  
**Date**: 2026-03-30  
**Context**: The `KnowledgeEmbedding.Embedding` column needs a pgvector column type. We use `text-embedding-3-small` (1536 dimensions) as the default embedding model.

**Decision**: Hardcode `vector(1536)` in the EF Core Fluent API configuration instead of using an untyped `vector` column.

**Rationale**:
- HNSW indexes perform better with a known, fixed dimensionality — the index builder can optimize memory layout and distance calculations.
- Using an untyped `vector` would allow mixed-dimension vectors in the same table, making similarity search meaningless (comparing 768-dim with 1536-dim vectors is invalid).
- The project's own `vector-db.md` rule states: *"all embeddings MUST use the same model — mixing models makes similarity search meaningless"*. A typed column enforces this at the database level.

**Trade-off**: If we change embedding models (e.g., to `text-embedding-3-large` at 3072 dimensions), a migration is required to alter the column type AND all existing embeddings must be re-generated. This is acceptable because changing embedding models already requires re-embedding everything (it's inherently a breaking operation).

---

### ADR-002: FK via Fluent API Without Navigation Property (Cross-Layer References)

**Status**: Accepted  
**Date**: 2026-03-30  
**Context**: `KnowledgeItem` (Domain layer) has a `Guid UserId` property referencing `User` (Infrastructure layer, inherits `IdentityUser<Guid>`). Clean Architecture forbids Domain from referencing Infrastructure, so `KnowledgeItem` cannot have a `User` navigation property.

**Decision**: Configure the foreign key constraint via Fluent API in Infrastructure (`HasOne<User>().WithMany().HasForeignKey(x => x.UserId)`) without any navigation property on the Domain entity.

**Rationale**:
- **Domain purity preserved**: `KnowledgeItem` only knows about `Guid UserId` — no IdentityUser dependency leaks into Domain.
- **Referential integrity enforced**: The FK constraint exists in the database (created by EF Core migrations), preventing orphaned records.
- **EF Core supports this pattern**: `HasOne<T>()` without a navigation property is a first-class EF Core feature for exactly this use case.
- **Querying still works**: We can join via `UserId` in LINQ queries from Infrastructure/Application layers.

**Trade-off**: We lose the convenience of `Include(x => x.User)` eager loading on `KnowledgeItem`. This is acceptable because User data is typically loaded separately (e.g., from the auth context or a dedicated query), not eagerly with every KnowledgeItem fetch.