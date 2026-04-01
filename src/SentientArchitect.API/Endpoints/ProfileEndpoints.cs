using Microsoft.AspNetCore.Mvc;
using SentientArchitect.API.Common.Endpoints;
using SentientArchitect.API.Extensions;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Features.Profile.AcceptSuggestion;
using SentientArchitect.Application.Features.Profile.GetProfile;
using SentientArchitect.Application.Features.Profile.GetProfileSuggestions;
using SentientArchitect.Application.Features.Profile.RejectSuggestion;
using SentientArchitect.Application.Features.Profile.UpdateProfile;

namespace SentientArchitect.API.Endpoints;

public class ProfileEndpoints : IEndpointModule
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/profile")
            .WithTags("Profile")
            .RequireAuthorization();

        group.MapGet("/", async (
            [FromServices] IUserAccessor userAccessor,
            [FromServices] GetProfileUseCase useCase,
            CancellationToken ct) =>
        {
            var userId = userAccessor.GetCurrentUserId();
            var result = await useCase.ExecuteAsync(new GetProfileRequest(userId), ct);
            return result.ToHttpResult();
        })
        .WithName("GetProfile")
        .WithOpenApi();

        group.MapPut("/", async (
            [FromBody] UpdateProfileHttpRequest body,
            [FromServices] IUserAccessor userAccessor,
            [FromServices] UpdateProfileUseCase useCase,
            CancellationToken ct) =>
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
        })
        .WithName("UpdateProfile")
        .WithOpenApi();

        group.MapGet("/suggestions", async (
            [FromServices] IUserAccessor userAccessor,
            [FromServices] GetProfileSuggestionsUseCase useCase,
            CancellationToken ct) =>
        {
            var userId = userAccessor.GetCurrentUserId();
            var result = await useCase.ExecuteAsync(new GetProfileSuggestionsRequest(userId), ct);
            return result.ToHttpResult();
        })
        .WithName("GetProfileSuggestions")
        .WithOpenApi();

        group.MapPost("/suggestions/{id:guid}/accept", async (
            [FromRoute] Guid id,
            [FromServices] IUserAccessor userAccessor,
            [FromServices] AcceptSuggestionUseCase useCase,
            CancellationToken ct) =>
        {
            var userId = userAccessor.GetCurrentUserId();
            var result = await useCase.ExecuteAsync(new AcceptSuggestionRequest(id, userId), ct);
            return result.ToHttpResult();
        })
        .WithName("AcceptSuggestion")
        .WithOpenApi();

        group.MapPost("/suggestions/{id:guid}/reject", async (
            [FromRoute] Guid id,
            [FromServices] IUserAccessor userAccessor,
            [FromServices] RejectSuggestionUseCase useCase,
            CancellationToken ct) =>
        {
            var userId = userAccessor.GetCurrentUserId();
            var result = await useCase.ExecuteAsync(new RejectSuggestionRequest(id, userId), ct);
            return result.ToHttpResult();
        })
        .WithName("RejectSuggestion")
        .WithOpenApi();
    }

    private record UpdateProfileHttpRequest(
        List<string>? PreferredStack = null,
        List<string>? KnownPatterns = null,
        string? InfrastructureContext = null,
        string? TeamSize = null,
        string? ExperienceLevel = null,
        string? CustomNotes = null);
}
