using Microsoft.EntityFrameworkCore;
using SentientArchitect.API.Extensions;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Features.Profile.AcceptSuggestion;
using SentientArchitect.Application.Features.Profile.GetProfile;
using SentientArchitect.Application.Features.Profile.RejectSuggestion;
using SentientArchitect.Application.Features.Profile.UpdateProfile;
using SentientArchitect.Domain.Enums;

namespace SentientArchitect.API.Endpoints;

public static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/profile")
            .WithTags("Profile")
            .RequireAuthorization();

        group.MapGet("/", GetAsync)
            .WithName("GetProfile")
            .WithOpenApi();

        group.MapPut("/", UpdateAsync)
            .WithName("UpdateProfile")
            .WithOpenApi();

        group.MapGet("/suggestions", GetSuggestionsAsync)
            .WithName("GetProfileSuggestions")
            .WithOpenApi();

        group.MapPost("/suggestions/{id:guid}/accept", AcceptAsync)
            .WithName("AcceptSuggestion")
            .WithOpenApi();

        group.MapPost("/suggestions/{id:guid}/reject", RejectAsync)
            .WithName("RejectSuggestion")
            .WithOpenApi();

        return app;
    }

    private static async Task<IResult> GetAsync(
        IUserAccessor userAccessor,
        GetProfileUseCase useCase,
        CancellationToken ct)
    {
        var userId = userAccessor.GetCurrentUserId();
        var result = await useCase.ExecuteAsync(new GetProfileRequest(userId), ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> UpdateAsync(
        UpdateProfileHttpRequest body,
        IUserAccessor userAccessor,
        UpdateProfileUseCase useCase,
        CancellationToken ct)
    {
        var userId = userAccessor.GetCurrentUserId();

        var request = new UpdateProfileRequest(
            userId,
            body.PreferredStack,
            body.KnownPatterns,
            body.InfrastructureContext,
            body.TeamSize,
            body.ExperienceLevel,
            body.CustomNotes);

        var result = await useCase.ExecuteAsync(request, ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> GetSuggestionsAsync(
        IUserAccessor userAccessor,
        IApplicationDbContext db,
        CancellationToken ct)
    {
        var userId      = userAccessor.GetCurrentUserId();
        var suggestions = await db.ProfileUpdateSuggestions
            .Where(s => s.UserId == userId && s.Status == SuggestionStatus.Pending)
            .AsNoTracking()
            .ToListAsync(ct);
        return Results.Ok(suggestions);
    }

    private static async Task<IResult> AcceptAsync(
        Guid id,
        IUserAccessor userAccessor,
        AcceptSuggestionUseCase useCase,
        CancellationToken ct)
    {
        var userId = userAccessor.GetCurrentUserId();
        var result = await useCase.ExecuteAsync(new AcceptSuggestionRequest(id, userId), ct);
        return result.ToHttpResult();
    }

    private static async Task<IResult> RejectAsync(
        Guid id,
        IUserAccessor userAccessor,
        RejectSuggestionUseCase useCase,
        CancellationToken ct)
    {
        var userId = userAccessor.GetCurrentUserId();
        var result = await useCase.ExecuteAsync(new RejectSuggestionRequest(id, userId), ct);
        return result.ToHttpResult();
    }

    private record UpdateProfileHttpRequest(
        List<string>? PreferredStack = null,
        List<string>? KnownPatterns = null,
        string? InfrastructureContext = null,
        string? TeamSize = null,
        string? ExperienceLevel = null,
        string? CustomNotes = null);
}
