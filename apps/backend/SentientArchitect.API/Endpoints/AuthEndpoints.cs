using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SentientArchitect.API.Common.Endpoints;
using SentientArchitect.API.Filters;
using SentientArchitect.API.Options;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Application.Common.Results;
using SentientArchitect.Application.Features.Auth.Login;
using SentientArchitect.Application.Features.Auth.LogoutSession;
using SentientArchitect.Application.Features.Auth.RefreshSession;
using SentientArchitect.Application.Features.Auth.RegisterUser;

namespace SentientArchitect.API.Endpoints;

public class AuthEndpoints : IEndpointModule
{
    public void Map(IEndpointRouteBuilder app)
    {
        var rateLimitOpts = app.ServiceProvider
            .GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;

        var group = app.MapGroup("/api/v1/auth")
            .WithTags("Auth");

        var register = group.MapPost("/register", async (
            [FromBody] RegisterUserRequest body,
            [FromServices] RegisterUserUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(body, ct);
            return ToRegisterResult(result);
        })
        .WithName("Register")
        .WithOpenApi()
        .AllowAnonymous();

        var login = group.MapPost("/login", async (
            [FromBody] LoginRequest body,
            [FromServices] LoginUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(body, ct);
            return ToLoginResult(result);
        })
        .WithName("Login")
        .WithOpenApi()
        .AllowAnonymous();

        if (rateLimitOpts.Enabled)
            login.AddEndpointFilter<LoginFailedByEmailFilter>();

        var refresh = group.MapPost("/refresh", async (
            [FromBody] RefreshSessionRequest body,
            [FromServices] RefreshSessionUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(body, ct);
            return ToRefreshResult(result);
        })
        .WithName("RefreshToken")
        .WithOpenApi()
        .AllowAnonymous();

        group.MapPost("/logout", async (
            [FromServices] IUserAccessor userAccessor,
            [FromServices] LogoutSessionUseCase useCase,
            CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(
                new LogoutSessionRequest(userAccessor.GetCurrentUserId()),
                ct);

            return result.Succeeded ? Results.NoContent() : Results.Unauthorized();
        })
        .WithName("Logout")
        .WithOpenApi()
        .RequireAuthorization();
    }

    private static IResult ToRegisterResult(Result<RegisterUserResponse> result)
    {
        if (!result.Succeeded || result.Data is null)
            return Results.BadRequest(new { errors = result.Errors });

        return Results.Created(
            $"/api/v1/auth/{result.Data.UserId}",
            new
            {
                userId = result.Data.UserId,
                email = result.Data.Email,
                displayName = result.Data.DisplayName,
            });
    }

    private static IResult ToLoginResult(Result<LoginResponse> result)
    {
        if (!result.Succeeded || result.Data is null)
            return Results.Unauthorized();

        return Results.Ok(new
        {
            token = result.Data.Token,
            refreshToken = result.Data.RefreshToken,
            expiresIn = result.Data.ExpiresIn,
            user = new
            {
                id = result.Data.User.Id,
                email = result.Data.User.Email,
                displayName = result.Data.User.DisplayName,
                role = result.Data.User.Role,
                tenantId = result.Data.User.TenantId,
            },
        });
    }

    private static IResult ToRefreshResult(Result<RefreshSessionResponse> result)
    {
        if (!result.Succeeded || result.Data is null)
            return Results.Unauthorized();

        return Results.Ok(new
        {
            token = result.Data.Token,
            refreshToken = result.Data.RefreshToken,
        });
    }
}
