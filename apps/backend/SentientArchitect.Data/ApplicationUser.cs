using Microsoft.AspNetCore.Identity;

namespace SentientArchitect.Data;

/// <summary>
/// ASP.NET Identity user entity. Lives in Data because it defines the DB schema.
/// Maps to the Domain User entity via services in the Application/Infrastructure layers.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
