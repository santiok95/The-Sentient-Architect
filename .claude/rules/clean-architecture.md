# Clean Architecture Rules

## Layer Dependencies
- Domain → nothing (zero external packages)
- Application → Domain only
- Infrastructure → Application + Domain + external packages
- Presentation (API) → Application + Infrastructure (for DI registration)

## Domain Layer
- Entities: private setters, modification through behavior methods
- Value Objects: immutable, equality by value
- Interfaces: `IKnowledgeRepository`, `IVectorStore`, `IEmbeddingService`, `ICodeAnalyzer`, `ITrendScanner`
- Enums: all domain enums live here
- No DTOs in Domain → those belong in Application

## Application Layer
- One Use Case class per operation (Single Responsibility)
- Use Cases receive interfaces via constructor injection
- DTOs define what goes in/out of Use Cases
- Mappers convert between Domain entities and DTOs
- No direct EF Core usage → only repository interfaces

## Infrastructure Layer
- EF Core DbContext and entity configurations (Fluent API)
- Concrete implementations of all Domain interfaces
- Background job definitions (IHostedService)
- External API clients (GitHub, LLM providers)

## Presentation Layer
- Controllers are thin → delegate to Use Cases
- No business logic in controllers
- Middleware for cross-cutting concerns (auth, logging, error handling)
