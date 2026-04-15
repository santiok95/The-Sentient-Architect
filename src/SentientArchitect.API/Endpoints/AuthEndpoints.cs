using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
            .WithTags("Auth")
            .RequireRateLimiting("auth");  // 10 req/min per IP — brute-force protection

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
            [FromServices] ITokenService tokenService,
            [FromServices] IConfiguration configuration) =>
        {
            var user = await userManager.FindByEmailAsync(body.Email);

            if (user is null || !await userManager.CheckPasswordAsync(user, body.Password))
                return Results.Unauthorized();

            var roles = await userManager.GetRolesAsync(user);
            var refreshExpiresDays = int.TryParse(configuration["Jwt:RefreshTokenExpiresInDays"], out var rd) ? rd : 7;

            var accessToken  = tokenService.CreateToken(user.Id, user.Email!, user.DisplayName, user.TenantId, roles);
            var refreshToken = GenerateRefreshToken();

            user.RefreshToken          = refreshToken;
            user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(refreshExpiresDays);
            await userManager.UpdateAsync(user);

            return Results.Ok(new
            {
                token        = accessToken,
                refreshToken,
                expiresIn    = 86400, // access token: 1 day
                user = new
                {
                    id          = user.Id,
                    email       = user.Email,
                    displayName = user.DisplayName,
                    role        = roles.FirstOrDefault() ?? "User",
                    tenantId    = user.TenantId,
                },
            });
        })
        .WithName("Login")
        .WithOpenApi()
        .AllowAnonymous();

        group.MapPost("/refresh", async (
            [FromBody] RefreshRequest body,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromServices] ITokenService tokenService,
            [FromServices] IConfiguration configuration) =>
        {
            // Look up the user by the opaque refresh token value.
            // Using a timing-safe comparison is not required here because we query the DB by exact match —
            // the DB lookup itself is the gate; an invalid token returns no user.
            var user = await userManager.Users
                .FirstOrDefaultAsync(u => u.RefreshToken == body.RefreshToken);

            if (user is null || !user.IsActive)
                return Results.Unauthorized();

            // Reject expired refresh tokens
            if (user.RefreshTokenExpiresAt is null || user.RefreshTokenExpiresAt < DateTime.UtcNow)
                return Results.Unauthorized();

            // Rotation: invalidate the current token and issue a new pair
            var roles              = await userManager.GetRolesAsync(user);
            var refreshExpiresDays = int.TryParse(configuration["Jwt:RefreshTokenExpiresInDays"], out var rd) ? rd : 7;
            var newAccessToken     = tokenService.CreateToken(user.Id, user.Email!, user.DisplayName, user.TenantId, roles);
            var newRefreshToken    = GenerateRefreshToken();

            user.RefreshToken          = newRefreshToken;
            user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(refreshExpiresDays);
            await userManager.UpdateAsync(user);

            return Results.Ok(new
            {
                token        = newAccessToken,
                refreshToken = newRefreshToken,
            });
        })
        .WithName("RefreshToken")
        .WithOpenApi()
        .AllowAnonymous();
    }

    private static string GenerateRefreshToken()
    {
        Span<byte> bytes = stackalloc byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private record RegisterRequest(string Email, string Password, string DisplayName);
    private record LoginRequest(string Email, string Password);
    private record RefreshRequest(string RefreshToken);
}
