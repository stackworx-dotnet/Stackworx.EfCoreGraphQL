namespace Stackworx.EfCoreGraphQL;

using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
/// Ready-made values for <see cref="GenerateOptions.Filter"/>.
/// </summary>
public static class EntityTypeFilters
{
    private const string IdentityNamespace = "Microsoft.AspNetCore.Identity";

    /// <summary>
    /// Excludes the ASP.NET Core Identity entity types (<c>IdentityUser</c>, <c>IdentityRole</c>,
    /// <c>IdentityUserToken&lt;TKey&gt;</c> and friends), which cannot be excluded with
    /// <c>[EFCoreGraphQLIgnore]</c> because they are declared by the framework.
    /// </summary>
    /// <remarks>
    /// Only types declared in the Identity namespace are matched. Your own types are kept even when they
    /// derive from an Identity type (e.g. <c>ApplicationUser : IdentityUser&lt;Guid&gt;</c>) — annotate
    /// those instead, so an entity you own stays under your control.
    /// </remarks>
    public static bool AspNetIdentity(IEntityType entityType)
        => entityType.ClrType.Namespace == IdentityNamespace;
}
