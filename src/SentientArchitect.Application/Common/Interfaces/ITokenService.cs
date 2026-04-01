namespace SentientArchitect.Application.Common.Interfaces;

/// <summary>
/// Generates JWT bearer tokens for authenticated users.
/// Lives in Application so use cases can depend on it without referencing Data/Infrastructure.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Creates a signed JWT for the given user identity.
    /// </summary>
    /// <param name="userId">The user's unique identifier (sub claim).</param>
    /// <param name="email">The user's email address.</param>
    /// <param name="displayName">Human-readable name (name claim).</param>
    /// <param name="tenantId">The user's tenant scope for multi-tenancy.</param>
    /// <param name="roles">One or more role names assigned to the user.</param>
    string CreateToken(Guid userId, string email, string displayName, Guid tenantId, IList<string> roles);
}
