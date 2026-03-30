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
Clean Architecture with 4 layers. Dependency flows inward only: Domain ← Application ← Infrastructure ← Presentation.

- `src/SentientArchitect.Domain/` — Entities, ValueObjects, Interfaces, Enums. ZERO external dependencies.
- `src/SentientArchitect.Application/` — Use Cases, DTOs, Mappers, Agent orchestration logic.
- `src/SentientArchitect.Infrastructure/` — EF Core, pgvector, Semantic Kernel config, Roslyn, external APIs, background jobs.
- `src/SentientArchitect.API/` — REST controllers, SignalR hubs, middleware.

## Commands
```
dotnet build src/SentientArchitect.sln
dotnet test tests/
dotnet ef migrations add <Name> --project src/SentientArchitect.Infrastructure --startup-project src/SentientArchitect.API
```

## Coding Conventions
- Use Fluent API for all EF Core configurations (never data annotations on entities)
- Domain entities have private setters; modification through methods
- All DateTime properties stored as UTC
- Guid PKs generated client-side
- Async/await throughout; suffix async methods with `Async`
- Interfaces in Domain, implementations in Infrastructure
- Every entity includes `UserId` and `TenantId` for multi-tenancy

## Authentication & Authorization
- ASP.NET Identity with JWT bearer tokens
- User entity inherits `IdentityUser<Guid>` — Identity tables renamed (no "AspNet" prefix)
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
- Domain layer must have ZERO NuGet dependencies
- NEVER execute code from analyzed repositories — static analysis only
- All vector operations go through `IVectorStore` interface (not direct pgvector calls)
- ConversationSummary must be generated before token count exceeds threshold
- UserProfile updates require explicit user confirmation via ProfileUpdateSuggestion

## Detailed Context
For full entity model and relationships: read `docs/ARCHITECTURE_DECISIONS.md`
For project vision, data flows, and risks: read `docs/PROJECT_CONTEXT.md`
For API endpoints, request/response models, and SignalR hubs: read `docs/API_CONTRACTS.md`
For implementation progress: read `docs/IMPLEMENTATION_LOG.md`