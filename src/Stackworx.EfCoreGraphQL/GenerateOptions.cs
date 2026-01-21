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
}