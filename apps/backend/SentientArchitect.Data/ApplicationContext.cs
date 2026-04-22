using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Data;

public class ApplicationContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IApplicationDbContext
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

    // --- Code Guardian ---
    public DbSet<RepositoryInfo> Repositories => Set<RepositoryInfo>();
    public DbSet<AnalysisReport> AnalysisReports => Set<AnalysisReport>();
    public DbSet<AnalysisFinding> AnalysisFindings => Set<AnalysisFinding>();

    // --- Consultant Agent ---
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();

    // --- Shared / Cross-cutting ---
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<ProfileUpdateSuggestion> ProfileUpdateSuggestions => Set<ProfileUpdateSuggestion>();
    public DbSet<ContentPublishRequest> ContentPublishRequests => Set<ContentPublishRequest>();
    public DbSet<TokenUsageTracker> TokenUsageTrackers => Set<TokenUsageTracker>();

    // --- Trends Radar ---
    public DbSet<TechnologyTrend> TechnologyTrends => Set<TechnologyTrend>();
    public DbSet<TrendSnapshot> TrendSnapshots => Set<TrendSnapshot>();

    // --- Auth ---
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Aseguramos que EF agregue el CREATE EXTENSION vector
        builder.HasAnnotation("Npgsql:PostgresExtension:vector", ",,");

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationContext).Assembly);
    }
}
