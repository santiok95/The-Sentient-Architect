using SentientArchitect.Domain.Abstractions;

namespace SentientArchitect.Domain.Entities;

/// <summary>
/// Opaque refresh token persisted in the database.
/// Each login creates a new entry. On refresh, the old token is revoked and a new one issued (rotation).
/// On logout, all tokens for the user are revoked.
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; private init; }

    /// <summary>Cryptographically random opaque string (Base64, 64 bytes entropy).</summary>
    public string Token { get; private init; } = default!;

    public DateTimeOffset ExpiresAt { get; private init; }

    public bool IsRevoked { get; private set; }

    /// <summary>Required by EF Core.</summary>
    private RefreshToken() { }

    public RefreshToken(Guid userId, string token, DateTimeOffset expiresAt)
    {
        UserId = userId;
        Token = token;
        ExpiresAt = expiresAt;
        IsRevoked = false;
    }

    public bool IsValid => !IsRevoked && ExpiresAt > DateTimeOffset.UtcNow;

    public void Revoke() => IsRevoked = true;
}
