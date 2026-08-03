namespace Stackworx.EfCoreGraphQL.Validation;

using System.Reflection;
using HotChocolate;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Stackworx.EfCoreGraphQL.Abstractions;
using Stackworx.EfCoreGraphQL.Shared;

public class EvaluateSchema
{
    // TODO: create variation that relies on Xunit.assert to report multiple failures
    // Assert.Multiple();
    /// <param name="mode">The <c>GenerateOptions.Mode</c> generation ran with.</param>
    /// <param name="filter">
    /// The <c>GenerateOptions.Filter</c> generation ran with. Entities it excludes are out of scope, the
    /// same way they are for generation.
    /// </param>
    public static List<Error> Evaluate(
        ISchema schema,
        IModel model,
        Mode mode = Mode.OptOut,
        Func<IEntityType, bool>? filter = null)
    {
        var entities = model
            .GetEntityTypes()
            .Where(e => !e.IsOwned());

        var typesByRuntimeType = schema.Types
            .Where(x => x is IHasRuntimeType)
            .GroupBy(t => t.ToRuntimeType())
            .ToDictionary(x => x.Key, x => x.ToList());

        var errors = new List<Error>();

        // then
        foreach (var entity in entities)
        {
            if (entity.ShouldIgnore())
            {
                continue;
            }

            if (mode == Mode.OptIn && !entity.ShouldInclude())
            {
                continue;
            }

            if (filter is not null && filter(entity))
            {
                continue;
            }

            if (typesByRuntimeType.TryGetValue(entity.ClrType, out var types))
            {
                switch (types.Count)
                {
                    case 0:
                        break;
                    case 1:
                    {
                        var t = types[0];

                        if (t is ObjectType objectType)
                        {
                            errors.AddRange(Validate(objectType, entity, mode, filter));
                        }

                        break;
                    }

                    default:
                        throw new ApplicationException(
                            $"{entity.ClrType} maps to multiple GraphQL Types: {string.Join(",", types.Select(t => t.Name))}");
                }
            }
        }

        return errors;
    }

    private static IList<Error> Validate(
        ObjectType objectType,
        IEntityType entity,
        Mode mode,
        Func<IEntityType, bool>? filter)
    {
        var errors = new List<Error>();

        // The caller's mode and filter already scoped this entity in; what is left to decide is whether the
        // generator could emit for it at all — a keyless or composite-key entity gets nothing, so its
        // missing fields were never generated.
        var isGenerated = GenerationPolicy.GeneratesFor(entity, mode, filter);

        var navigations = entity
            .GetNavigations()
            .Where(n => !n.IsEagerLoaded && !n.TargetEntityType.IsOwned())
            .ToList();

        foreach (var nav in navigations)
        {
            var field = FindFieldForNavigation(objectType, nav);

            if (field is null)
            {
                if (isGenerated && GenerationPolicy.GeneratesFor(nav))
                {
                    errors.Add(SuppressedField(entity, objectType, nav));
                }

                continue;
            }

            var hasExplicitResolver = HasExplicitResolver(field);

            if (!hasExplicitResolver)
            {
                errors.Add(new Error
                {
                    Kind = ErrorKind.MissingResolver,
                    EntityType = entity,
                    ObjectType = objectType,
                    Field = field,
                    FieldName = field.Name,
                    Message = "Missing explicit resolver for navigation " + nav.Name + " on type " + objectType.Name + ".",
                });
            }
        }

        if (!isGenerated)
        {
            return errors;
        }

        // Many-to-many fields are only ever reached through a generated resolver, so an absent field is
        // the only failure worth reporting for them.
        foreach (var nav in entity.GetSkipNavigations())
        {
            if (GenerationPolicy.GeneratesFor(nav) && FindFieldForNavigation(objectType, nav) is null)
            {
                errors.Add(SuppressedField(entity, objectType, nav));
            }
        }

        return errors;
    }

    /// <summary>
    /// A navigation the generator resolved that no longer reaches the schema. The generated resolver can
    /// never run, and nothing in the build says so.
    /// </summary>
    /// <remarks>
    /// HotChocolate applies <c>ExtendObjectType(IgnoreFields = ...)</c> while merging that extension into
    /// the type, against the fields present at that moment. A second extension of the same type re-adds
    /// what it resolves, so whichever of the two merges last wins, and merge order follows registration
    /// order. Adopting the generator into a schema that already hides fields by hand is where the two
    /// meet.
    /// </remarks>
    private static Error SuppressedField(IEntityType entity, ObjectType objectType, INavigationBase nav)
        => new()
        {
            Kind = ErrorKind.SuppressedField,
            EntityType = entity,
            ObjectType = objectType,
            Field = null,
            FieldName = ToCamel(nav.Name),
            Message =
                $"Generated resolver for navigation {nav.Name} on type {objectType.Name} is dead: the schema has no "
                + $"'{ToCamel(nav.Name)}' field. Another ExtendObjectType extension of {objectType.Name} that lists it "
                + "in IgnoreFields removes it when that extension merges last. Ignore the navigation with "
                + $"[GraphQLIgnore] so nothing is generated for it, or stop ignoring '{ToCamel(nav.Name)}'.",
        };

    private static IObjectField? FindFieldForNavigation(ObjectType objectType, INavigationBase nav)
    {
        // 1) Try by bound member
        if (nav.PropertyInfo is MemberInfo member)
        {
            var byMember = objectType.Fields.FirstOrDefault(f =>
                f is IObjectField { Member: not null } of && SymbolEquals(of.Member, member));
            if (byMember is not null)
            {
                return byMember;
            }
        }

        // 2) Try exact name
        var byName = objectType.Fields.FirstOrDefault(f => string.Equals(f.Name, nav.Name, StringComparison.Ordinal));
        if (byName is not null)
        {
            return byName;
        }

        // 3) Try camelCase name
        var camel = ToCamel(nav.Name);
        return objectType.Fields.FirstOrDefault(f => string.Equals(f.Name, camel, StringComparison.Ordinal));
    }

    private static bool SymbolEquals(MemberInfo a, MemberInfo b)
        => a.MetadataToken == b.MetadataToken && a.Module == b.Module;

    private static string ToCamel(string name)
        => string.IsNullOrEmpty(name) || char.IsLower(name[0])
            ? name
            : char.ToLowerInvariant(name[0]) + name.Substring(1);

    private static bool HasExplicitResolver(IObjectField field)
    {
        // https://chillicream.com/docs/hotchocolate/v15/defining-a-schema/dynamic-schemas#resolver-types
        if (field.PureResolver is not null)
        {
            return false;
        }

        // Code first special case
        if (field.Member is null && field.ResolverMember is null)
        {
            return true;
        }

        if (field.ResolverMember is MethodInfo)
        {
            return true;
        }

        if (field.Member is MethodInfo)
        {
            return true;
        }

        return false;
    }

    public enum ErrorKind
    {
        /// <summary>
        /// The navigation is exposed as a field, but resolves by property access, so selecting it queries
        /// per parent.
        /// </summary>
        MissingResolver,

        /// <summary>
        /// The generator emitted a resolver for the navigation, but the schema has no field for it.
        /// </summary>
        SuppressedField,
    }

    public record Error
    {
        public required ErrorKind Kind { get; init; }

        public required IEntityType EntityType { get; init; }

        public required IObjectType ObjectType { get; init; }

        /// <summary>
        /// Null for <see cref="ErrorKind.SuppressedField"/>, where the schema has no field to point at.
        /// </summary>
        public required IObjectField? Field { get; init; }

        /// <summary>
        /// The GraphQL field name the error is about, present whether or not the field reached the schema.
        /// </summary>
        public required string FieldName { get; init; }

        public required string Message { get; init; }
    }
}
