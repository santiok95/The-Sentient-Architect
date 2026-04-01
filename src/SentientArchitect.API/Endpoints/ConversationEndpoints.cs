using SentientArchitect.API.Extensions;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Features.Conversations.CreateConversation;
using SentientArchitect.Application.Features.Conversations.GetConversations;

namespace SentientArchitect.API.Endpoints;

public static class ConversationEndpoints
{
    public static IEndpointRouteBuilder MapConversationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/conversations")
            .WithTags("Conversations")
            .RequireAuthorization();

        group.MapPost("/", CreateAsync)
            .WithName("CreateConversation")
            .WithOpenApi();

        group.MapGet("/", GetAllAsync)
            .WithName("GetConversations")
            .WithOpenApi();

        group.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("DeleteConversation")
            .WithOpenApi();

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateConversationHttpRequest body,
        IUserAccessor userAccessor,
        CreateConversationUseCase useCase,
        CancellationToken ct)
    {
        var userId   = userAccessor.GetCurrentUserId();
        var tenantId = userAccessor.GetCurrentTenantId();

        var request = new CreateConversationRequest(userId, tenantId, body.Title ?? "New Conversation");
        var result  = await useCase.ExecuteAsync(request, ct);

        return result.ToCreatedResult($"/api/v1/conversations/{result.Data?.ConversationId}");
    }

    private static async Task<IResult> GetAllAsync(
        IUserAccessor userAccessor,
        GetConversationsUseCase useCase,
        CancellationToken ct)
    {
        var userId = userAccessor.GetCurrentUserId();
        var result = await useCase.ExecuteAsync(new GetConversationsRequest(userId), ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        IApplicationDbContext db,
        CancellationToken ct)
    {
        var conversation = await db.Conversations.FindAsync([id], ct);
        if (conversation is not null)
        {
            db.Conversations.Remove(conversation);
            await db.SaveChangesAsync(ct);
        }

        return Results.NoContent();
    }

    private record CreateConversationHttpRequest(string? Title = null);
}
