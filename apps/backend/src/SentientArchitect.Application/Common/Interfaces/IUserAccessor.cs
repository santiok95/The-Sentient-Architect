namespace SentientArchitect.Application.Common.Interfaces;

/// <summary>
/// Provides access to the current HTTP request's authenticated user identity.
/// Returns safe defaults (Guid.Empty / false) when no user is authenticated — never throws.
/// </summary>
public interface IUserAccessor
{
    /// <summary>Returns the current user's Id, or <see cref="Guid.Empty"/> if unauthenticated.</summary>
    Guid GetCurrentUserId();

    /// <summary>Returns the current user's TenantId, or <see cref="Guid.Empty"/> if unauthenticated.</summary>
    Guid GetCurrentTenantId();

    /// <summary>Returns true when an authenticated identity is present on the current request.</summary>
    bool IsAuthenticated();
}
