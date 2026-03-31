# Coding Standards — Modern .NET 9 Practices

These rules apply to ALL code in this project. Any AI agent or developer must follow them.

## Entity Rules

1. **IEntity**: ALL entities MUST implement `IEntity` from `Domain/Abstractions/`. Exception: join tables with composite keys (e.g., `KnowledgeItemTag`).
2. **POCO style**: Entities are simple POCOs with constructor (required params) and public setters (`{ get; set; }`).
3. **No parameterless constructors**: EF Core 7+ does not require them. Constructors should accept required fields.
4. **No exceptions in Domain**: Domain entities do NOT throw exceptions. NO `throw` statements. Validation lives in Application layer via the Result pattern.
5. **No factory methods**: Don't use `static Create()` methods. Use constructors directly.
6. **Collections**: Initialize as `HashSet<T>` in the constructor. Type as `ICollection<T>`.
7. **Navigation properties**: Nullable (`Entity? NavigationProp { get; set; }`).

## Result Pattern

- `Result` and `Result<TData>` live in `Application/Common/Results/`.
- ALL Application services/use cases return `Result` or `Result<TData>`.
- Exceptions are ONLY for truly exceptional errors (DB down, out of memory).
- API layer converts `Result` to HTTP responses via `ToHttpResult()` extensions.

## Enum Rules

- Always declare with explicit numeric values: `MyEnum { Value1 = 0, Value2 = 1 }`.
- NEVER reorder existing values. Only append new values at the end.
- Stored as **integer** in the database (EF Core default). No `.HasConversion<string>()`.

## Architecture Rules

- **Domain**: Entities, Enums, IEntity. ZERO NuGet packages. ZERO exceptions.
- **Application**: Interfaces, Result pattern, Use Cases (future: Vertical Slice). References Domain.
- **Data**: ApplicationContext, Entity Configurations (DB-agnostic), ApplicationUser. References Domain + Application.
- **Data.Postgres**: PostgreSQL-specific config (pgvector, HNSW), Migrations, DesignTimeFactory, DI. References Data.
- **Infrastructure**: Identity services (TokenService, UserAccessor, Seeder), DI. References Application + Domain + Data.
- **API**: Endpoints, Middleware, ResultExtensions. References App + Infra + Data.Postgres.

## Interface Placement

- Persistence/service interfaces live in **Application** (`Application/Common/Interfaces/`), NOT in Domain.
- Domain only has `IEntity` as an interface.

## User Entity Pattern

- `User` POCO lives in **Domain** (no IdentityUser dependency).
- `ApplicationUser : IdentityUser<Guid>` lives in **Data** (near the DbContext).
- Mapping between them is handled by Application/Infrastructure services.

## EF Core / Data Rules

- **Fluent API only** — no data annotations on entities.
- **One configuration file per entity** (or per logical group).
- **PostgreSQL-specific configs** (vector types, HNSW indexes) go in **Data.Postgres**.
- **JSONB for collections**: Use `List<string>` with `.HasColumnType("jsonb")` instead of `string[]` or `text[]`.
- **No `HasConversion<string>`** for enums — use default int storage.

## String Collections

- NEVER use `string[]`. Use `List<string>` mapped to JSONB columns.
- In entity: `public List<string> MyList { get; set; } = [];`
- In config: `.HasColumnType("jsonb")`
