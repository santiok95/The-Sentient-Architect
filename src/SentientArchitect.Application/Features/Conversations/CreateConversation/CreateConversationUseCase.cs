using Microsoft.EntityFrameworkCore;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Domain.Entities;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.Application.Features.Conversations.CreateConversation;

public class CreateConversationUseCase(IApplicationDbContext db)
{
    public async Task<Result<CreateConversationResponse>> ExecuteAsync(
        CreateConversationRequest request,
        CancellationToken ct = default)
    {
        string? repoBranch = null;

        if (request.ActiveRepositoryId.HasValue)
        {
            // Must be a Consultant conversation to use repo-bound mode
            if (request.AgentType != AgentType.Consultant)
                return Result<CreateConversationResponse>.Failure(
                    ["Only Consultant conversations can be bound to a repository."],
                    ErrorType.Validation);

            // Repo must belong to the same user AND have at least one Completed report
            var repo = await db.Repositories
                .AsNoTracking()
                .Include(r => r.Reports)
                .Where(r => r.Id == request.ActiveRepositoryId.Value && r.UserId == request.UserId)
                .FirstOrDefaultAsync(ct);

            if (repo is null)
                return Result<CreateConversationResponse>.Failure(
                    ["Repository not found or access denied."],
                    ErrorType.Forbidden);

            var hasCompleted = repo.Reports.Any(r => r.Status == AnalysisStatus.Completed);
            if (!hasCompleted)
                return Result<CreateConversationResponse>.Failure(
                    ["Repository analysis is not complete yet. Wait for the analysis to finish before starting a consultation."],
                    ErrorType.Validation);

            repoBranch = repo.DefaultBranch ?? "main";
        }

        var conversation = new Conversation(request.UserId, request.TenantId, request.Title, request.AgentType);

        if (request.ActiveRepositoryId.HasValue)
            conversation.UpdateConsultantContext(
                request.ActiveRepositoryId,
                preferredStack: null,
                contextMode: ConsultantContextMode.RepoBound,
                activeRepositoryBranch: repoBranch);

        db.Conversations.Add(conversation);
        await db.SaveChangesAsync(ct);

        return Result<CreateConversationResponse>.SuccessWith(
            new CreateConversationResponse(conversation.Id, conversation.Title));
    }
}
