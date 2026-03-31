using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Data;

public class ApplicationContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public ApplicationContext(DbContextOptions<ApplicationContext> options)
        : base(options)
    {
    }

    // --- Semantic Brain ---
    public DbSet<KnowledgeItem> KnowledgeItems => Set<KnowledgeItem>();
    public DbSet<KnowledgeEmbedding> KnowledgeEmbeddings => Set<KnowledgeEmbedding>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<KnowledgeItemTag> KnowledgeItemTags => Set<KnowledgeItemTag>();

    // --- Shared / Cross-cutting ---
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<ProfileUpdateSuggestion> ProfileUpdateSuggestions => Set<ProfileUpdateSuggestion>();
    public DbSet<ContentPublishRequest> ContentPublishRequests => Set<ContentPublishRequest>();
    public DbSet<TokenUsageTracker> TokenUsageTrackers => Set<TokenUsageTracker>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationContext).Assembly);
    }
}
