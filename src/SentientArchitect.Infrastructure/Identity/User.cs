using Microsoft.AspNetCore.Identity;

namespace SentientArchitect.Infrastructure.Identity;

/// <summary>
/// Application user entity. Inherits from IdentityUser&lt;Guid&gt; to leverage ASP.NET Identity
/// for authentication (password hashing, lockout, email confirmation, etc.).
/// 
/// Lives in Infrastructure (not Domain) because IdentityUser is an external NuGet dependency.
/// Domain entities reference users via primitive Guid UserId properties only (see ADR-002).
/// </summary>
public class User : IdentityUser<Guid>
{
    /// <summary>
    /// User-facing display name. Required.
    /// </summary>
    public string DisplayName { get; private set; } = default!;

    /// <summary>
    /// Multi-tenancy key. Initially equals UserId for solo users.
    /// Changes when the user joins an organization/team.
    /// </summary>
    public Guid TenantId { get; private set; }

    /// <summary>
    /// When this user account was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>EF Core / Identity requires a parameterless constructor.</summary>
    private User() { }

    /// <summary>
    /// Creates a new User with TenantId defaulting to their own UserId (solo user).
    /// </summary>
    public static User Create(string email, string displayName, string userName)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty.", nameof(email));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("DisplayName cannot be empty.", nameof(displayName));

        if (string.IsNullOrWhiteSpace(userName))
            throw new ArgumentException("UserName cannot be empty.", nameof(userName));

        var id = Guid.NewGuid();

        return new User
        {
            Id = id,
            Email = email.Trim().ToLowerInvariant(),
            NormalizedEmail = email.Trim().ToUpperInvariant(),
            UserName = userName.Trim(),
            NormalizedUserName = userName.Trim().ToUpperInvariant(),
            DisplayName = displayName.Trim(),
            TenantId = id, // Solo user: TenantId == UserId
            CreatedAt = DateTime.UtcNow,
            SecurityStamp = Guid.NewGuid().ToString()
        };
    }

    /// <summary>
    /// Assigns this user to a team/organization tenant.
    /// </summary>
    public void AssignToTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));

        TenantId = tenantId;
    }

    /// <summary>
    /// Updates the display name.
    /// </summary>
    public void UpdateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("DisplayName cannot be empty.", nameof(displayName));

        DisplayName = displayName.Trim();
    }
}
