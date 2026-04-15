using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using SentientArchitect.Application.Common.Interfaces;

namespace SentientArchitect.Infrastructure.Agents.Consultant;

public sealed class SummaryPlugin(IApplicationDbContext db)
{
    [KernelFunction, Description("Get a summary of the current conversation to maintain context across long discussions. Call this when resuming a conversation that may have been compacted.")]
    public async Task<string> GetConversationSummaryAsync(
        [Description("Conversation ID to get summary for")] string conversationId,
        CancellationToken cancellationToken = default)
    {
        var convGuid     = Guid.Parse(conversationId);
        var conversation = await db.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == convGuid, cancellationToken);

        if (conversation is null)
            return "Conversation not found.";

        return conversation.Summary ?? "No summary yet. This is a new conversation.";
    }

    [KernelFunction, Description("Save a rolling summary of the conversation to manage token usage. Call this when the conversation token count approaches the limit.")]
    public async Task<string> SaveConversationSummaryAsync(
        [Description("Conversation ID to update")] string conversationId,
        [Description("Concise summary of the conversation so far, capturing key decisions and context")] string summary,
        [Description("Approximate token count after compaction")] int remainingTokens,
        CancellationToken cancellationToken = default)
    {
        var convGuid     = Guid.Parse(conversationId);
        var conversation = await db.Conversations
            .FirstOrDefaultAsync(c => c.Id == convGuid, cancellationToken);

        if (conversation is null)
            return "Conversation not found.";

        conversation.UpdateSummary(summary, remainingTokens);
        await db.SaveChangesAsync(cancellationToken);

        return "Summary saved successfully.";
    }
}
