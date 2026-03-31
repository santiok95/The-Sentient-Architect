using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Data.Configurations;

public class KnowledgeEmbeddingConfiguration : IEntityTypeConfiguration<KnowledgeEmbedding>
{
    public void Configure(EntityTypeBuilder<KnowledgeEmbedding> builder)
    {
        builder.ToTable("KnowledgeEmbeddings");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.KnowledgeItemId).IsRequired();
        builder.Property(e => e.ChunkIndex).IsRequired();
        builder.Property(e => e.ChunkText).IsRequired();
        builder.Property(e => e.Embedding).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasIndex(e => e.KnowledgeItemId);

        // NOTE: vector(1536) column type and HNSW index configured in Data.Postgres
    }
}
