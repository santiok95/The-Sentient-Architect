using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Infrastructure.Persistence.Configurations;

/// <summary>
/// Fluent API configuration for KnowledgeEmbedding.
/// 
/// Embedding column uses hardcoded vector(1536) for text-embedding-3-small model (ADR-001).
/// HNSW index configured for fast approximate nearest neighbor search.
/// </summary>
public class KnowledgeEmbeddingConfiguration : IEntityTypeConfiguration<KnowledgeEmbedding>
{
    public void Configure(EntityTypeBuilder<KnowledgeEmbedding> builder)
    {
        builder.ToTable("KnowledgeEmbeddings");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.KnowledgeItemId)
            .IsRequired();

        builder.Property(e => e.ChunkIndex)
            .IsRequired();

        builder.Property(e => e.ChunkText)
            .IsRequired();

        // ADR-001: Hardcoded vector(1536) for text-embedding-3-small
        // Changing embedding models requires a migration + full re-embedding
        builder.Property(e => e.Embedding)
            .IsRequired()
            .HasColumnType("vector(1536)");

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        // HNSW index for fast similarity search (per vector-db.md)
        // Using cosine distance operator class (vector_cosine_ops)
        builder.HasIndex(e => e.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");

        // Index on KnowledgeItemId for efficient lookups by parent
        builder.HasIndex(e => e.KnowledgeItemId);
    }
}
