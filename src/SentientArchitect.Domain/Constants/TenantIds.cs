namespace SentientArchitect.Domain.Constants;

/// <summary>
/// Well-known tenant ID values. Using named constants avoids silent Guid.Empty magic spread across the codebase.
/// </summary>
public static class TenantIds
{
    /// <summary>
    /// The synthetic tenant assigned to content that has been approved and published to the shared knowledge base.
    /// Any user may read items with this TenantId through the SearchPlugin.
    /// </summary>
    public static readonly Guid Shared = Guid.Empty;
}
