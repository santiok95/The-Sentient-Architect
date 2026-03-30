# Entity Framework Core Conventions

## Configuration
- Use Fluent API exclusively (no [Attributes] on entities)
- One configuration class per entity: `KnowledgeItemConfiguration : IEntityTypeConfiguration<KnowledgeItem>`
- All configurations in `Infrastructure/Persistence/Configurations/`

## Data Types
- Guid PKs: `HasDefaultValueSql("gen_random_uuid()")` or client-generated
- String arrays (PreferredStack, KnownPatterns): PostgreSQL native `text[]` via `.HasColumnType("text[]")`
- Vector type: `Pgvector.EntityFrameworkCore` package, `vector` column type
- DateTime: always UTC, use value converter if needed
- Enums: store as string via `.HasConversion<string>()`

## Relationships
- KnowledgeItem → RepositoryInfo: 1:0..1 TPT (Table-Per-Type)
- KnowledgeItem → Tag: M:N via KnowledgeItemTag join table
- KnowledgeItem → KnowledgeEmbedding: 1:N with cascade delete
- User → UserProfile: 1:1
- Conversation → ConversationMessage: 1:N with cascade delete
- RepositoryInfo → AnalysisReport: 1:N (multiple reports over time)

## Indexes
- HNSW index on `KnowledgeEmbedding.Embedding` for similarity search
- Unique index on `User.Email`
- Index on `KnowledgeItem.UserId` + `KnowledgeItem.TenantId`
- Index on `KnowledgeItem.Type` for filtered queries
- Index on `Conversation.UserId` + `Conversation.Status`

## Migrations
- Always use named migrations: `dotnet ef migrations add AddKnowledgeEmbeddingTable`
- Review generated SQL before applying
- pgvector extension must be created: `CREATE EXTENSION IF NOT EXISTS vector;`
