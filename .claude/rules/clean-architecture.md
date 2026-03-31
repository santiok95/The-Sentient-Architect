# Clean Architecture Rules

## Layer Dependencies
- Domain → nothing (zero external packages)
- Application → Domain only
- Infrastructure → Application + Domain + external packages
- Data → Application + Domain + EF Core packages
- Data.Postgres → Data + Npgsql/pgvector packages
- Presentation (API) → Application + Data.Postgres + Infrastructure (for DI registration)

## Domain Layer
- Entities: implement `IEntity`, inherit `BaseEntity` for shared fields (Id, audit, multi-tenancy)
- Entities: private/init setters, modification through behavior methods, factory methods for creation
- Value Objects: immutable, equality by value
- Abstractions: `IEntity`, `BaseEntity` (DDD primitives only — NO infrastructure interfaces here)
- Enums: all domain enums live here (stored as string in DB)
- No DTOs in Domain → those belong in Application

## Application Layer
- Interfaces: `IKnowledgeRepository`, `IVectorStore`, `IEmbeddingService`, `ICodeAnalyzer`, `ITrendScanner`
- One Use Case class per operation (Single Responsibility), organized as Vertical Slices per feature
- Use Cases receive interfaces via constructor injection
- DTOs define what goes in/out of Use Cases
- Mappers convert between Domain entities and DTOs
- Result pattern: all services return `Result` or `Result<T>` (no exceptions for business logic)
- No direct EF Core usage → only interfaces

## Data Layer (`Data` + `Data.Postgres`)
- `Data/`: ApplicationContext, Entity Configurations (Fluent API), ApplicationUser (Identity)
- `Data.Postgres/`: PostgreSQL-specific configs (pgvector, HNSW), Migrations, DI registration
- Concrete implementations of Application interfaces (repositories)

## Infrastructure Layer
- Identity services (TokenService, UserAccessor, IdentitySeeder)
- Background job definitions (IHostedService)
- External API clients (GitHub, LLM providers)

## Presentation Layer
- Controllers are thin → delegate to Use Cases
- No business logic in controllers
- Middleware for cross-cutting concerns (auth, logging, error handling)
