namespace Stackworx.EfCoreGraphQL;

using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Stackworx.EfCoreGraphQL.Abstractions;

public class GenerateOptions
{
    public Mode? Mode { get; set; } = Abstractions.Mode.OptOut;

    /// <summary>
    /// Some types cannot be annotated and need to be manually excluded (e.g. Identity types).
    /// Return true to exclude an entity from generation.
    /// </summary>
    public Func<IEntityType, bool>? Filter { get; set; }

    public string Namespace { get; set; } = "Generated.DataLoaders";

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