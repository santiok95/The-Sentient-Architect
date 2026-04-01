using System.ComponentModel;
using Microsoft.SemanticKernel;
using SentientArchitect.Application.Features.Knowledge.IngestKnowledge;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Infrastructure.Agents.Knowledge;

public sealed class IngestPlugin(IngestKnowledgeUseCase ingestUseCase)
{
    [KernelFunction, Description("Store new knowledge in the knowledge base. Use this to save articles, notes, code snippets, or any technical content the user wants to remember.")]
    public async Task<string> IngestContentAsync(
        [Description("Title of the content to store")] string title,
        [Description("Full text content to store and index")] string content,
        [Description("Type of content: Article, Note, Documentation, RepositoryReference, or TrendReport")] string contentType,
        [Description("User ID of the owner")] string userId,
        [Description("Tenant ID for scoping")] string tenantId,
        [Description("Optional source URL")] string? sourceUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<KnowledgeItemType>(contentType, true, out var type))
            type = KnowledgeItemType.Note;

        var request = new IngestKnowledgeRequest(
            Guid.Parse(userId),
            Guid.Parse(tenantId),
            title,
            content,
            type,
            sourceUrl);

        var result = await ingestUseCase.ExecuteAsync(request, cancellationToken);

        return result.Succeeded
            ? $"Content '{title}' stored successfully with ID {result.Data!.KnowledgeItemId}."
            : $"Failed to store content: {string.Join(", ", result.Errors)}";
    }
}
