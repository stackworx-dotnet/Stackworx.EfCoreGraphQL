namespace Stackworx.EfCoreGraphQL;

using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Stackworx.EfCoreGraphQL.Abstractions;

public class GenerateOptions
{
    public const string DefaultNamespace = "Generated.DataLoaders";

    public GenerateOptions()
    {
    }

    /// <summary>
    /// Copies <paramref name="other"/> so a caller can override individual options without mutating an
    /// instance it does not own.
    /// </summary>
    public GenerateOptions(GenerateOptions other)
    {
        ArgumentNullException.ThrowIfNull(other);

        this.Mode = other.Mode;
        this.Filter = other.Filter;
        this.Namespace = other.Namespace;
        this.CI = other.CI;
        this.IgnoreForeignKeyFields = other.IgnoreForeignKeyFields;
    }

    public Mode? Mode { get; set; } = Abstractions.Mode.OptOut;

    /// <summary>
    /// Some types cannot be annotated and need to be manually excluded (e.g. Identity types).
    /// Return true to exclude an entity from generation.
    /// </summary>
    /// <seealso cref="EntityTypeFilters"/>
    public Func<IEntityType, bool>? Filter { get; set; }

    /// <summary>
    /// Namespace of the generated code. Leave null for <see cref="DefaultNamespace"/>; the design-time
    /// integration instead derives it from the model snapshot's namespace.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// When enabled, generation will fail the process if generated output differs from git HEAD.
    /// </summary>
    public bool CI { get; set; }

    /// <summary>
    /// When enabled, foreign-key scalar fields are hidden from the schema via
    /// <c>ExtendObjectType(IgnoreFields = ...)</c>, on the basis that the navigation replaces them.
    /// </summary>
    /// <remarks>
    /// Disable this for an existing schema: hiding the scalars removes fields that clients may already
    /// select (e.g. a foreign key that doubles as a business identifier), which breaks their queries.
    /// </remarks>
    public bool IgnoreForeignKeyFields { get; set; } = true;
}
