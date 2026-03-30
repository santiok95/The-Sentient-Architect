using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Infrastructure.Identity;

namespace SentientArchitect.Infrastructure.Persistence;

/// <summary>
/// Main database context. Inherits from IdentityDbContext to integrate ASP.NET Identity tables.
/// All entity configurations are applied via IEntityTypeConfiguration classes (Fluent API).
/// </summary>
public class SentientArchitectDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public SentientArchitectDbContext(DbContextOptions<SentientArchitectDbContext> options)
        : base(options)
    {
    }

    // --- Semantic Brain (Pillar 1) ---
    public DbSet<KnowledgeItem> KnowledgeItems => Set<KnowledgeItem>();
    public DbSet<KnowledgeEmbedding> KnowledgeEmbeddings => Set<KnowledgeEmbedding>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<KnowledgeItemTag> KnowledgeItemTags => Set<KnowledgeItemTag>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Apply Identity model first (creates all AspNet* tables)
        base.OnModelCreating(builder);

        // Enable pgvector extension
        builder.HasPostgresExtension("vector");

        // Apply all IEntityTypeConfiguration<T> classes from this assembly
        builder.ApplyConfigurationsFromAssembly(typeof(SentientArchitectDbContext).Assembly);
    }
}
