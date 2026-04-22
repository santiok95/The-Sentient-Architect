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

namespace SentientArchitect.API.Endpoints;

public class AuthEndpoints : IEndpointModule
{
    public void Map(IEndpointRouteBuilder app)
    {
        var rateLimitOpts = app.ServiceProvider
            .GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;

        var group = app.MapGroup("/api/v1/auth")
            .WithTags("Auth");

        // Registro deshabilitado temporalmente.
        // Razones: sin email verification ni CAPTCHA, un atacante con proxies
        // rotativos puede crear cuentas fantasma. Hasta tener esos controles,
        // el endpoint responde 403 y no ejecuta el use case.
        group.MapPost("/register", () => Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "El registro está deshabilitado.",
            detail: "El registro público no está habilitado en este momento."))
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

            return result.Succeeded
                ? Results.NoContent()
                : Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "No hay una sesión activa.",
                    detail: result.Errors.Count > 0
                        ? string.Join("; ", result.Errors)
                        : "No estás autenticado o la sesión ya fue cerrada.");
        })
        .WithName("Logout")
        .WithOpenApi()
        .RequireAuthorization();
    }

    private static IResult ToLoginResult(Result<LoginResponse> result)
    {
        if (!result.Succeeded || result.Data is null)
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "No pudimos iniciar sesión.",
                detail: result.Errors.Count > 0
                    ? string.Join("; ", result.Errors)
                    : "El correo electrónico o la contraseña son incorrectos.");

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
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Sesión inválida.",
                detail: result.Errors.Count > 0
                    ? string.Join("; ", result.Errors)
                    : "La sesión expiró o no es válida. Iniciá sesión nuevamente.");

        return Results.Ok(new
        {
            token = result.Data.Token,
            refreshToken = result.Data.RefreshToken,
        });
    }
}
