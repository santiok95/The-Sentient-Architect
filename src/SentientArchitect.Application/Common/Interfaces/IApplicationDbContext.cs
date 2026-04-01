using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SentientArchitect.Domain.Entities;

namespace SentientArchitect.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DatabaseFacade Database { get; }
    DbSet<KnowledgeItem> KnowledgeItems { get; }
    DbSet<KnowledgeEmbedding> KnowledgeEmbeddings { get; }
    DbSet<Tag> Tags { get; }
    DbSet<KnowledgeItemTag> KnowledgeItemTags { get; }
    DbSet<UserProfile> UserProfiles { get; }
    DbSet<ProfileUpdateSuggestion> ProfileUpdateSuggestions { get; }
    DbSet<ContentPublishRequest> ContentPublishRequests { get; }
    DbSet<TokenUsageTracker> TokenUsageTrackers { get; }
    DbSet<Conversation> Conversations { get; }
    DbSet<ConversationMessage> ConversationMessages { get; }
    DbSet<RepositoryInfo> Repositories { get; }
    DbSet<AnalysisReport> AnalysisReports { get; }
    DbSet<AnalysisFinding> AnalysisFindings { get; }
    DbSet<TechnologyTrend> TechnologyTrends { get; }
    DbSet<TrendSnapshot> TrendSnapshots { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
