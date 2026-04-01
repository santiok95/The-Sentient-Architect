using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SentientArchitect.API.Common.Endpoints;
using SentientArchitect.API.Extensions;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Data;

namespace SentientArchitect.API.Endpoints;

public class AuthEndpoints : IEndpointModule
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth")
            .WithTags("Auth");

        group.MapPost("/register", async (
            [FromBody] RegisterRequest body,
            [FromServices] UserManager<ApplicationUser> userManager) =>
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
        })
        .WithName("Register")
        .WithOpenApi()
        .AllowAnonymous();

        group.MapPost("/login", async (
            [FromBody] LoginRequest body,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromServices] ITokenService tokenService) =>
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
        })
        .WithName("Login")
        .WithOpenApi()
        .AllowAnonymous();
    }

    private record RegisterRequest(string Email, string Password, string DisplayName);
    private record LoginRequest(string Email, string Password);
}
