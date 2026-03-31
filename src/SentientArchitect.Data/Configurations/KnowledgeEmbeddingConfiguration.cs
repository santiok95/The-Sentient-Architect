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
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasIndex(e => e.KnowledgeItemId);

        // --- SOLUCIÓN RED FLAG 1: pgvector explícito con ValueConverter ---
        // Npgsql 9 a nivel de EF Core no tolera la inferencia cruda de `float[]` a un tipo físico `vector(1536)` 
        // a menos que le interpongamos un Conversor (ValueConverter) explícito haciendo puente con Pgvector.Vector.
        builder.Property(e => e.Embedding)
            .IsRequired()
            .HasConversion(
                v => new Pgvector.Vector(v),     // .NET float[] -> Postgres pgevector (Database insert)
                v => v.ToArray()                 // Postgres pgvector -> .NET float[] (Database read)
            )
            .HasColumnType("vector(1536)");

        // Configuramos el HNSW Index usando las anotaciones abstraidas de Npgsql.
        // EF Core 9 traduce "Npgsql:IndexMethod" a .HasMethod() cuando genera el SQL de Postgres.
        builder.HasIndex(e => e.Embedding)
            .HasDatabaseName("IX_KnowledgeEmbeddings_HNSW")
            .HasAnnotation("Npgsql:IndexMethod", "hnsw")
            .HasAnnotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });
    }
}
