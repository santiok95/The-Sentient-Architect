using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SentientArchitect.Infrastructure.Identity;

namespace SentientArchitect.Infrastructure.Persistence.Configurations;

/// <summary>
/// Fluent API configuration for the User entity (IdentityUser&lt;Guid&gt;).
/// Configures custom properties and renames the table from "AspNetUsers" to "Users".
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // Rename from AspNetUsers → Users
        builder.ToTable("Users");

        builder.Property(u => u.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(u => u.TenantId)
            .IsRequired();

        builder.Property(u => u.CreatedAt)
            .IsRequired();

        // Index on TenantId for multi-tenant queries
        builder.HasIndex(u => u.TenantId);
    }
}
