using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SentientArchitect.Application.Common.Interfaces;

namespace SentientArchitect.Infrastructure.Identity;

/// <summary>
/// Reads the current user's identity from the active HTTP request context.
/// Always returns safe defaults — never throws when unauthenticated.
/// </summary>
public sealed class UserAccessor(IHttpContextAccessor httpContextAccessor) : IUserAccessor
{
    private const string TenantIdClaimType = "tenantId";

    private ClaimsPrincipal? Principal =>
        httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated() =>
        Principal?.Identity?.IsAuthenticated is true;

    public Guid GetCurrentUserId()
    {
        var value = Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? Principal?.FindFirstValue(JwtClaims.Sub);

        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }

    public Guid GetCurrentTenantId()
    {
        var value = Principal?.FindFirstValue(TenantIdClaimType);
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }

    /// <summary>Short aliases for JWT registered claim names — avoids a package reference in this file.</summary>
    private static class JwtClaims
    {
        internal const string Sub = "sub";
    }
}
