# The Sentient Architect — Implementation Log

## Status Legend
- ✅ Completed
- 🔄 In Progress
- 📋 Planned
- 🐛 Issue Found

## Phase 0 — Analysis & Design (Current)

| Item | Status | Notes |
|------|--------|-------|
| Define 4 pillars | ✅ | Semantic Brain, Architecture Consultant, Code Guardian, Trends Radar |
| Competitive landscape | ✅ | No unified tool exists; integration is our differentiator |
| Architecture pattern | ✅ | Clean Architecture, 6 projects (Domain, Application, Data, Data.Postgres, Infrastructure, API) |
| Entity model | ✅ | 15 entities across 4 pillars + shared |
| Database decision | ✅ | PostgreSQL + pgvector; IVectorStore interface for future migration |
| Runtime decision | ✅ | .NET 9 stable |
| ConversationSummary design | ✅ | Rolling summary for token cost management |
| UserProfile update mechanism | ✅ | Suggestion-based with user confirmation |
| Repo trust levels | ✅ | Internal (quality focus) vs External (full security scan) |
| Semantic Kernel agent design | ✅ | 3 agents (Knowledge, Consultant, Guardian) + 1 background job (Radar) |
| Semantic Kernel rule file | ✅ | .claude/rules/semantic-kernel.md created |
| API contracts | ✅ | 7 endpoint groups + 3 SignalR hubs. Minimal APIs with /api/v1/ versioning |
| Security & auth design | ✅ | ASP.NET Identity + JWT, Admin/User roles, content scope (personal + shared with approval) |
| Stack with versions | ✅ | Full NuGet stack documented in PROJECT_CONTEXT.md |

## Phase 1 — Semantic Brain
| Item | Status | Notes |
|------|--------|-------|
| Solution scaffold | ✅ | 6 projects: Domain, Application, Data, Data.Postgres, Infrastructure, API. .slnx format |
| Domain entities | ✅ | KnowledgeItem, KnowledgeEmbedding, Tag, KnowledgeItemTag, User, UserProfile, ContentPublishRequest, ProfileUpdateSuggestion, TokenUsageTracker. BaseEntity inheritance, private setters, behavior methods |
| Domain enums | ✅ | KnowledgeItemType, ProcessingStatus, TagCategory, PublishRequestStatus, SuggestionStatus, QuotaAction |
| Domain value objects | ✅ | VectorSearchResult record |
| Domain interfaces | ✅ | IKnowledgeRepository, IVectorStore, IEmbeddingService, ITagRepository (all in Application layer) |
| Identity (User entity) | ✅ | User POCO in Domain + ApplicationUser : IdentityUser\<Guid\> in Data. Table renamed to "Users" |
| EF Core DbContext | ✅ | ApplicationContext in Data, IdentityDbContext\<ApplicationUser\>, pgvector extension, ApplyConfigurationsFromAssembly |
| Identity table renaming | ✅ | All 7 AspNet* tables renamed (Users, Roles, UserRoles, RoleClaims, UserClaims, UserLogins, UserTokens) |
| Fluent API configs | ✅ | All entities configured. Enums as `.HasConversion<string>().HasMaxLength(50)`. JSONB for List\<string\>. FK via ADR-002 |
| pgvector setup | ✅ | vector(1536) hardcoded (ADR-001), HNSW index with cosine ops |
| ADR-001 | ✅ | Hardcoded vector(1536) for HNSW performance |
| ADR-002 | ✅ | FK via Fluent API without navigation property (cross-layer) |
| Ingestion pipeline | ✅ | IngestKnowledgeUseCase: chunk → embed (NullEmbeddingService) → pgvector |
| RAG query flow | ✅ | SearchKnowledgeUseCase: embed query → IVectorStore → fetch items |
| Auth endpoints | ✅ | POST /auth/register + /auth/login. JWT, Identity, IdentitySeeder |
| Knowledge endpoints | ✅ | POST/GET/DELETE /knowledge, GET /knowledge/search |
| Repository implementations | ✅ | KnowledgeRepository, TagRepository, PgVectorStore (raw SQL HNSW) |
| NullEmbeddingService | ✅ | Placeholder in Infrastructure/AI/ — still used as graceful fallback when OpenAI key absent |
| OpenAIEmbeddingService | ✅ | Real implementation in Infrastructure/AI/ — active when AI:OpenAI:ApiKey is configured |

## Phase 2 — Architecture Consultant
| Item | Status | Notes |
|------|--------|-------|
| Conversation entities | ✅ | Conversation + ConversationMessage. Behavior: AddMessage, UpdateSummary, Archive |
| EF configs + migration | ✅ | Migration: AddPhase2345Tables (2026-04-01) |
| UserProfile + suggestions | ✅ | UserProfile Update* methods, ProfileUpdateSuggestion Accept/Reject |
| Conversation use cases | ✅ | CreateConversation, GetConversations, ArchiveConversation |
| Profile use cases | ✅ | GetProfile, UpdateProfile, AcceptSuggestion, RejectSuggestion |
| Admin use cases | ✅ | ReviewPublishRequest (Approve/Reject) |
| Conversation/Profile endpoints | ✅ | Full CRUD. Admin policy: RequireAuthorization("Admin") |
| ConversationSummary compaction | 📋 | SummaryPlugin.SaveConversationSummaryAsync implemented — needs token threshold trigger |
| Semantic Kernel agent — Knowledge | ✅ | ChatCompletionAgent with SearchPlugin + IngestPlugin. Anthropic Claude via IChatClient bridge |
| Semantic Kernel agent — Consultant | ✅ | ChatCompletionAgent with ProfilePlugin + SummaryPlugin + SearchPlugin |
| OpenAI embeddings | ✅ | OpenAIEmbeddingService wrapping ITextEmbeddingGenerationService (text-embedding-3-small, 1536 dims) |
| Anthropic chat integration | ✅ | AnthropicClient → AsBuilder() → UseFunctionInvocation() → AsChatCompletionService() bridge |
| SignalR streaming | 📋 | |

## Phase 3 — Code Guardian
| Item | Status | Notes |
|------|--------|-------|
| RepositoryInfo entities | ✅ | RepositoryInfo, AnalysisReport, AnalysisFinding + enums |
| EF configs | ✅ | Included in AddPhase2345Tables migration |
| Use cases | ✅ | SubmitRepository, GetRepositories, GetAnalysisReport |
| Repository endpoints | ✅ | POST/GET /repositories, GET /repositories/{id}/reports, GET /reports/{id} |
| Git clone service | 📋 | ICodeAnalyzer not implemented yet |
| Roslyn analysis | 📋 | |
| Dependency scanner | 📋 | |
| AnalysisReport generation | 📋 | Background job — needs Roslyn + Git integration |

## Phase 4 — Trends Radar
| Item | Status | Notes |
|------|--------|-------|
| TechnologyTrend entities | ✅ | TechnologyTrend + TrendSnapshot + enums |
| EF configs | ✅ | Included in AddPhase2345Tables migration |
| Use cases | ✅ | GetTrends (with optional category filter), GetTrendSnapshots |
| Trend endpoints | ✅ | GET /trends, GET /trends/{id}/snapshots (public, no auth) |
| Source configuration | 📋 | Where to scan for trends (GitHub, HN, etc.) |
| Scanning background job | 📋 | IHostedService — needs AI + sources |
| Relevance scoring | 📋 | |

## Refactoring — Modernization (2026-03-31)

| # | Task | Status | Priority | Notes |
|---|------|--------|----------|-------|
| 1 | Fix connection string key | ✅ | Alta | Changed "DefaultConnection" → "Postgres" in appsettings |
| 2 | Enum string conversions | ✅ | Alta | `.HasConversion<string>().HasMaxLength(50)` in all 6 configs |
| 3 | Alinear docs | ✅ | Alta | CLAUDE.md, clean-architecture.md, entity-framework.md |
| 4 | BaseEntity abstracta | ✅ | Media | Id + CreatedAt centralized |
| 5 | Private setters + behavior methods | ✅ | Media | All entities modernized |
| 6 | Value Objects (TechStack, PatternList) | ⬜ | Media | Deferred to Phase 2 (Consultant Agent) |
| 7 | Métodos de comportamiento | ✅ | Media | MarkAsCompleted, Approve, Reject, etc. |
| 8 | IVectorStore scope fix | ✅ | Media | Added userId, tenantId, includeShared params |
| 9 | Evaluar ErrorOr vs Result | ⬜ | Baja | Pending decision |
| 10 | Vertical slices | ⬜ | Baja | Se aplica cuando arranque implementación de features |
| 11 | Centralizar Identity en Infra | ✅ | Alta | Desacoplar `AddIdentityCore` de PostgreSQL para pureza arquitectónica |

## Issues & Solutions
_None yet — will be populated during implementation._