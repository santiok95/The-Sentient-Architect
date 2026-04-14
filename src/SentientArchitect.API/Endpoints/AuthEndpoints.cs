using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SentientArchitect.API.Common.Endpoints;
using SentientArchitect.API.Extensions;
using SentientArchitect.Application.Common.Interfaces;
using SentientArchitect.Data;
using System.Text;

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
            [FromServices] ITokenService tokenService) =>
        {
            var user = await userManager.FindByEmailAsync(body.Email);

            if (user is null || !await userManager.CheckPasswordAsync(user, body.Password))
                return Results.Unauthorized();

            var roles       = await userManager.GetRolesAsync(user);
            var expiresDays = 7;

            var token = tokenService.CreateToken(
                user.Id,
                user.Email!,
                user.DisplayName,
                user.TenantId,
                roles);

            return Results.Ok(new
            {
                token,
                refreshToken = token, // JWT re-used as refresh credential (stateless)
                expiresIn    = expiresDays * 86400,
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
            var key = configuration["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(key))
                return Results.Problem("JWT not configured");

            // Validate the submitted token (expired or not) to extract the userId claim
            var handler = new JwtSecurityTokenHandler();
            SecurityToken? validated = null;
            try
            {
                handler.ValidateToken(body.RefreshToken, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                    ValidateIssuer           = false,
                    ValidateAudience         = false,
                    ValidateLifetime         = false, // allow expired tokens
                    ClockSkew                = TimeSpan.Zero,
                }, out validated);
            }
            catch
            {
                return Results.Unauthorized();
            }

            var jwt = (JwtSecurityToken)validated;
            var subClaim = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
            if (!Guid.TryParse(subClaim, out var userId))
                return Results.Unauthorized();

            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null || !user.IsActive)
                return Results.Unauthorized();

            var roles    = await userManager.GetRolesAsync(user);
            var newToken = tokenService.CreateToken(user.Id, user.Email!, user.DisplayName, user.TenantId, roles);

            return Results.Ok(new
            {
                token        = newToken,
                refreshToken = newToken,
            });
        })
        .WithName("RefreshToken")
        .WithOpenApi()
        .AllowAnonymous();
    }

    private record RegisterRequest(string Email, string Password, string DisplayName);
    private record LoginRequest(string Email, string Password);
    private record RefreshRequest(string RefreshToken);
}
