namespace Stackworx.EfCoreGraphQL.Shared;

using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Stackworx.EfCoreGraphQL.Abstractions;

/// <summary>
/// Decides what the generator emits. Shared with the validation package so that a field the generator
/// resolved can be told apart from one nobody asked for: the two have to agree, or validation either
/// misses dead generated code or reports fields that were never generated.
/// </summary>
/// <remarks>
/// Internal because this file is compiled into both packages, and a public type would then exist twice for
/// anyone referencing both.
/// </remarks>
internal static class GenerationPolicy
{
    /// <summary>
    /// True when an extension class is emitted for <paramref name="entity"/>.
    /// </summary>
    public static bool GeneratesFor(IEntityType entity, Mode? mode, Func<IEntityType, bool>? filter)
    {
        if (entity.IsOwned() || entity.ShouldIgnore())
        {
            return false;
        }

        if (mode == Mode.OptIn && !entity.ShouldInclude())
        {
            return false;
        }

        if (filter is not null && filter(entity))
        {
            return false;
        }

        var pk = entity.FindPrimaryKey();

        // Nothing to load by without a key, and a composite key marks a join entity.
        return pk is not null && pk.Properties.Count == 1;
    }

    /// <summary>
    /// True when a field override is emitted for <paramref name="navigation"/>, assuming its declaring
    /// entity passes <see cref="GeneratesFor(IEntityType, Mode?, Func{IEntityType, bool})"/>.
    /// </summary>
    public static bool GeneratesFor(INavigation navigation)
    {
        if (navigation.IsEagerLoaded || navigation.TargetEntityType.IsOwned())
        {
            return false;
        }

        var fk = navigation.ForeignKey;
        if (fk.Properties.Count > 1)
        {
            return false;
        }

        if (navigation.HasGraphQLIgnore())
        {
            return false;
        }

        // A shadow key has no CLR property, so neither the loader nor the field override can read it.
        // Leaving HotChocolate's default resolution in place beats overriding with a resolver that throws,
        // which would turn any query for the field into an error. The loader is only emitted from the
        // principal side, which is why the principal key only has to be readable there.
        if (fk.Properties.Single().IsShadowProperty())
        {
            return false;
        }

        return navigation.IsOnDependent || !fk.PrincipalKey.Properties.Single().IsShadowProperty();
    }

    /// <summary>
    /// True when a field override is emitted for the many-to-many <paramref name="navigation"/>, assuming
    /// its declaring entity passes <see cref="GeneratesFor(IEntityType, Mode?, Func{IEntityType, bool})"/>.
    /// </summary>
    public static bool GeneratesFor(ISkipNavigation navigation)
    {
        if (navigation.IsEagerLoaded || navigation.IsOnDependent)
        {
            return false;
        }

        if (navigation.HasGraphQLIgnore())
        {
            return false;
        }

        // Emission projects the parent key out of the join with a SelectMany() built from the inverse
        // member's name, so EF modelling the relationship is not enough — the inverse needs a CLR member.
        return navigation.Inverse.FieldInfo is not null || navigation.Inverse.PropertyInfo is not null;
    }
}
