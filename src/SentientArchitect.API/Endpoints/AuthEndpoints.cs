using Microsoft.AspNetCore.Identity;
using SentientArchitect.API.Extensions;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Data;

namespace SentientArchitect.API.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth")
            .WithTags("Auth");

        group.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .WithOpenApi()
            .AllowAnonymous();

        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .WithOpenApi()
            .AllowAnonymous();

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest body,
        UserManager<ApplicationUser> userManager)
    {
        var user = new ApplicationUser
        {
            Id          = Guid.NewGuid(),
            UserName    = body.Email,
            Email       = body.Email,
            DisplayName = body.DisplayName,
            TenantId    = Guid.NewGuid(),
            CreatedAt   = DateTime.UtcNow,
            IsActive    = true,
        };

        var createResult = await userManager.CreateAsync(user, body.Password);

        if (!createResult.Succeeded)
        {
            var errors = createResult.Errors.Select(e => e.Description).ToList();
            return Results.BadRequest(new { errors });
        }

        await userManager.AddToRoleAsync(user, "User");

        return Results.Created(
            $"/api/v1/auth/{user.Id}",
            new { userId = user.Id, email = user.Email, displayName = user.DisplayName });
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest body,
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService)
    {
        var user = await userManager.FindByEmailAsync(body.Email);

        if (user is null || !await userManager.CheckPasswordAsync(user, body.Password))
            return Results.Unauthorized();

        var roles    = await userManager.GetRolesAsync(user);
        var expiresDays = 7;
        var expiresAt   = DateTime.UtcNow.AddDays(expiresDays);

        var token = tokenService.CreateToken(
            user.Id,
            user.Email!,
            user.DisplayName,
            user.TenantId,
            roles);

        return Results.Ok(new
        {
            token,
            userId      = user.Id,
            email       = user.Email,
            displayName = user.DisplayName,
            expiresAt,
        });
    }

    private record RegisterRequest(string Email, string Password, string DisplayName);
    private record LoginRequest(string Email, string Password);
}
