# The Sentient Architect

## Project Overview
AI-powered developer knowledge management system with 4 pillars: Semantic Brain (RAG knowledge base), Architecture Consultant (reasoning agent), Code Guardian (repo analysis), Trends Radar (tech monitoring). All pillars feed into a shared semantic knowledge base.

## Tech Stack
- .NET 9, C# 13
- PostgreSQL 16+ with pgvector extension
- Entity Framework Core 9
- Semantic Kernel (AI orchestration)
- Roslyn (C# static analysis)
- SignalR (real-time streaming)

## Architecture
Clean Architecture with 6 projects. Dependency flows inward only.

- `src/SentientArchitect.Domain/` — Entities, Enums, IEntity. ZERO NuGet, ZERO exceptions.
- `src/SentientArchitect.Application/` — Interfaces, Result pattern, Use Cases (Vertical Slice when ready).
- `src/SentientArchitect.Data/` — ApplicationContext, Entity Configurations, ApplicationUser.
- `src/SentientArchitect.Data.Postgres/` — PostgreSQL-specific (pgvector, HNSW), Migrations, DesignTimeFactory, DI.
- `src/SentientArchitect.Infrastructure/` — Identity services (TokenService, UserAccessor, Seeder), DI.
- `src/SentientArchitect.API/` — Minimal API endpoints, SignalR hubs, middleware, ResultExtensions.

## Commands
```
dotnet build src/SentientArchitect.slnx
dotnet test tests/
dotnet ef migrations add <Name> -p src/SentientArchitect.Data.Postgres -s src/SentientArchitect.API
```

## Coding Conventions
- ALL entities inherit `BaseEntity` (Id + CreatedAt) except join tables with composite keys
- Entities: private setters, private parameterless constructor for EF Core, behavior methods for state changes
- Result pattern for all Application services (no exceptions for business logic)
- Fluent API for all EF Core configurations (never data annotations)
- Enums stored as string via `.HasConversion<string>().HasMaxLength(50)` in EF configs
- `List<string>` with JSONB for string collections (never `string[]`)
- All DateTime properties stored as UTC
- Guid PKs generated client-side
- Async/await throughout; suffix async methods with `Async`
- Every entity includes `UserId` and `TenantId` for multi-tenancy

## Data Access — NO Repository Pattern
- **NO** `IXxxRepository` interfaces. `DbContext` already IS the repository + unit of work.
- Inject `IApplicationDbContext` directly in use cases (Jason Taylor style).
- Use EF Core LINQ directly: `db.KnowledgeItems.Add(...)`, `db.KnowledgeItems.FirstOrDefaultAsync(...)`.
- `AsNoTracking()` on all read-only queries in endpoints/use cases.
- `IVectorStore` is kept — it's a real abstraction over pgvector, not a CRUD wrapper.

## Authentication & Authorization
- ASP.NET Identity with JWT bearer tokens
- `User` POCO in Domain (no IdentityUser dependency). `ApplicationUser : IdentityUser<Guid>` in Data.
- Identity tables renamed (no "AspNet" prefix)
- Two roles: `Admin` (full access, content review) and `User` (personal ingestion, queries, publication requests)
- Content scope: Personal (`TenantId = userId`) + Shared (`TenantId = org tenantId`)
- User content starts personal; reaches shared only through Admin-approved `ContentPublishRequest`
- SearchPlugin returns personal + shared results for User role; all scopes for Admin

## Agents (Semantic Kernel)
3 conversational agents + 1 background job:
- **Knowledge Agent**: SearchPlugin + IngestPlugin. Handles all knowledge storage and retrieval.
- **Consultant Agent**: ProfilePlugin + SummaryPlugin + shared SearchPlugin. Multi-turn architecture consultations.
- **Guardian Agent**: RoslynPlugin + DependencyPlugin + GitMetadataPlugin. Repo static analysis.
- **Trends Radar**: IHostedService (NOT an agent). Background job on timer.

Agents share SearchPlugin and IngestPlugin — no duplicated search/storage logic.

## IMPORTANT Rules
- Domain layer: ZERO NuGet dependencies, ZERO exceptions (no `throw`)
- Result pattern: ALL Application services return `Result` or `Result<T>`
- NEVER execute code from analyzed repositories — static analysis only
- All vector operations go through `IVectorStore` interface (not direct pgvector calls)
- ConversationSummary must be generated before token count exceeds threshold
- UserProfile updates require explicit user confirmation via ProfileUpdateSuggestion
- Read `.claude/rules/coding-standards.md` for full coding rules

## Detailed Context
For full entity model and relationships: read `docs/ARCHITECTURE_DECISIONS.md`
For project vision, data flows, and risks: read `docs/PROJECT_CONTEXT.md`
For API endpoints, request/response models, and SignalR hubs: read `docs/API_CONTRACTS.md`
For implementation progress: read `docs/IMPLEMENTATION_LOG.md`