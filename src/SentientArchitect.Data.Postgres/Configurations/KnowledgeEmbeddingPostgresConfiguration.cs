using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Data.Postgres.Configurations;

/// <summary>
/// PostgreSQL-specific configurations for KnowledgeEmbedding.
/// ADR-001: Hardcoded vector(1536) for text-embedding-3-small HNSW performance.
/// </summary>
public class KnowledgeEmbeddingPostgresConfiguration : IEntityTypeConfiguration<KnowledgeEmbedding>
{
    public void Configure(EntityTypeBuilder<KnowledgeEmbedding> builder)
    {
        // ADR-001: Hardcoded vector(1536) for text-embedding-3-small
        builder.Property(e => e.Embedding)
            .HasColumnType("vector(1536)");

        // HNSW index for fast cosine similarity search
        builder.HasIndex(e => e.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");
    }
}
