namespace Stackworx.EfCoreGraphQL;

using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.Metadata;

internal static class ForeignKeyIgnoreFields
{
    /// <summary>
    /// Returns GraphQL field names (camelCase) for EF Core foreign-key scalar properties that are
    /// redundant when navigations are exposed.
    /// </summary>
    public static IReadOnlyList<string> Get(IEntityType entity)
    {
        // We skip join entities elsewhere, but keep this helper safe for any entity type.
        var pk = entity.FindPrimaryKey();
        var pkProps = pk?.Properties?.ToHashSet() ?? [];

        var names = new HashSet<string>();

        foreach (var fk in entity.GetForeignKeys())
        {
            foreach (var prop in fk.Properties)
            {
                // Don't hide key fields (important for shared-PK 1:1 patterns).
                if (pkProps.Contains(prop))
                {
                    continue;
                }

                // If there is no CLR property (shadow property), HotChocolate won't expose it by default
                // when using the CLR type as the object type. Still, we keep it here since some schemas
                // may bind fields differently.
                var clrName = prop.Name;
                names.Add(TypeUtils.ToGraphQlFieldName(clrName));
            }
        }

        return names.OrderBy(n => n).ToArray();
    }
}
