# Coding Standards — Modern .NET 9 Practices

These rules apply to ALL code in this project. Any AI agent or developer must follow them.

## Entity Rules

1. **BaseEntity**: ALL entities MUST inherit `BaseEntity` from `Domain/Abstractions/`. Exception: join tables with composite keys (e.g., `KnowledgeItemTag`).
2. **Rich Domain Model**: Entities have private setters and behavior methods for state changes. NO public setters for mutable state.
3. **Private parameterless constructor**: Required for EF Core materialization. Keep it private.
4. **No exceptions in Domain**: Domain entities do NOT throw exceptions. NO `throw` statements. Validation lives in Application layer via the Result pattern.
5. **No factory methods**: Don't use `static Create()` methods. Use constructors directly.
6. **Collections**: Initialize as `new HashSet<T>()` in BOTH constructors. Type as `ICollection<T>`.
7. **Navigation properties**: Nullable (`Entity? NavigationProp { get; set; }`).

## Data Access — No Repository Pattern

- **DO NOT** use the Repository pattern. `DbContext` already IS a Repository + Unit of Work.
- Inject `IApplicationDbContext` directly into use cases (Jason Taylor / modern .NET consensus).
- Use EF Core LINQ directly: `db.KnowledgeItems.Add(...)`, `db.KnowledgeItems.FirstOrDefaultAsync(...)`.
- `AsNoTracking()` on all read-only queries.
- `Include()` explicitly when navigation properties are needed.
- `IApplicationDbContext` lives in `Application/Common/Interfaces/`.
- `ApplicationContext` implements `IApplicationDbContext`.

## Use Case Pattern

- One class per operation: `IngestKnowledgeUseCase`, `SearchKnowledgeUseCase`.
- Method: `Task<Result<TResponse>> ExecuteAsync(TRequest request, CancellationToken ct = default)`.
- Primary constructors: `public class MyUseCase(IApplicationDbContext db, IEmbeddingService emb)`.
- NO MediatR. NO AutoMapper. Direct classes, manual mapping.
- DTOs (Request/Response) co-located in the same feature folder.

## Result Pattern

- `Result` and `Result<TData>` live in `Application/Common/Results/`.
- ALL use cases return `Result` or `Result<TData>`.
- Exceptions are ONLY for truly exceptional errors (DB down, out of memory).
- API layer converts `Result` to HTTP responses via `ToHttpResult()` extensions.

## Enum Rules

- Always declare with explicit numeric values: `MyEnum { Value1 = 0, Value2 = 1 }`.
- NEVER reorder existing values. Only append new values at the end.
- Stored as **string** in the database via `.HasConversion<string>().HasMaxLength(50)`.

## Architecture Rules

- **Domain**: Entities, Enums, BaseEntity, IEntity. ZERO NuGet packages. ZERO exceptions.
- **Application**: `IApplicationDbContext` + service interfaces, Result pattern, Use Cases (Vertical Slices). References Domain + EF Core (for DbSet<T> in interface).
- **Data**: ApplicationContext (implements IApplicationDbContext), Entity Configurations (DB-agnostic), ApplicationUser. References Domain + Application.
- **Data.Postgres**: PostgreSQL-specific config (pgvector, HNSW), Migrations, DesignTimeFactory, DI. References Data.
- **Infrastructure**: Identity services (TokenService, UserAccessor, Seeder), AI services (NullEmbeddingService → real provider). References Application + Domain + Data.
- **API**: Minimal API endpoints, ResultExtensions, middleware. References App + Infra + Data.Postgres.

## Interface Placement

- `IApplicationDbContext`, `IEmbeddingService`, `IVectorStore`, `ITokenService`, `IUserAccessor` live in `Application/Common/Interfaces/`.
- Domain only has `IEntity` as an interface.
- NO `IXxxRepository` interfaces — use `IApplicationDbContext` directly.

## User Entity Pattern

- `User` POCO lives in **Domain** (no IdentityUser dependency).
- `ApplicationUser : IdentityUser<Guid>` lives in **Data** (near the DbContext).

## EF Core / Data Rules

- **Fluent API only** — no data annotations on entities.
- **One configuration file per entity** (or per logical group).
- **PostgreSQL-specific configs** (vector types, HNSW indexes) go in **Data.Postgres**.
- **JSONB for collections**: Use `List<string>` with `.HasColumnType("jsonb")`.
- **Enums as string**: `.HasConversion<string>().HasMaxLength(50)` — NOT default int.

## String Collections

- NEVER use `string[]`. Use `List<string>` mapped to JSONB columns.
- In entity: `public List<string> MyList { get; private set; } = [];`
- In config: `.HasColumnType("jsonb")`
