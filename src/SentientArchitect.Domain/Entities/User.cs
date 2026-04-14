using SentientArchitect.Domain.Abstractions;

namespace SentientArchitect.Domain.Entities;

public class User : BaseEntity
{
    public User(string email, string userName, string displayName)
    {
        Email = email;
        UserName = userName;
        DisplayName = displayName;
        IsActive = true;
    }

    private User() { }

    public string Email { get; private set; } = string.Empty;
    public string UserName { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public Guid TenantId { get; private set; }
    public bool IsActive { get; private set; } = true;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public void UpdateDisplayName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        DisplayName = name;
    }
}
