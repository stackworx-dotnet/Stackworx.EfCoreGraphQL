namespace Stackworx.EfCoreGraphQL.DesignTime;

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Stackworx.EfCoreGraphQL;

/// <summary>
/// Regenerates sidecar files without scaffolding a migration.
/// </summary>
/// <remarks>
/// Generation depends on things the EF model snapshot does not record — <c>[EFCoreGraphQLInclude]</c>,
/// <c>[GraphQLIgnore]</c> and the rest are read off the CLR types — so output can go stale while the
/// snapshot is still current. Scaffolding a migration to pick those up leaves an empty
/// <c>Up</c>/<c>Down</c> pair behind in the migration history; this is the way to regenerate without one.
/// The snapshot hook remains what keeps output fresh when the model itself changes.
/// </remarks>
public static class SidecarGenerator
{
    /// <summary>
    /// Regenerates the sidecar for every model snapshot in <paramref name="assembly"/>.
    /// </summary>
    /// <param name="assembly">
    /// The assembly holding the model snapshots, the <see cref="IDesignTimeDbContextFactory{TContext}"/>
    /// used to build the model, and the <see cref="IDesignTimeServices"/> holding the options — the
    /// assembly <c>dotnet ef</c> already loads for design-time services.
    /// </param>
    /// <param name="outputDirectory">
    /// Where to write. Falls back to <c>STACKWORX_EFCOREGRAPHQL_SIDECAR_OUTPUT_DIR</c>, the same variable
    /// scaffolding uses.
    /// </param>
    /// <param name="options">
    /// Generation options. Left null, they are read from the assembly's
    /// <see cref="IDesignTimeServices"/> implementations, so options stay declared in one place.
    /// </param>
    /// <param name="contextFactoryArgs">Passed to <see cref="IDesignTimeDbContextFactory{TContext}.CreateDbContext"/>.</param>
    public static IReadOnlyList<SidecarGenerationResult> Generate(
        Assembly assembly,
        string? outputDirectory = null,
        GenerateOptions? options = null,
        string[]? contextFactoryArgs = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var outputDir = SidecarOutput.ResolveRequiredOutputDir(outputDirectory);
        var resolvedOptions = options ?? DiscoverOptions(assembly);

        var snapshots = DiscoverSnapshots(assembly);
        if (snapshots.Count == 0)
        {
            throw new InvalidOperationException(
                $"No EF Core model snapshot was found in '{assembly.GetName().Name}'. Sidecar generation is named "
                + "after the snapshot, so the assembly holding your Migrations folder is the one to pass. Scaffold a "
                + "migration first if the project has none.");
        }

        var results = new List<SidecarGenerationResult>(snapshots.Count);

        foreach (var (snapshotType, contextType) in snapshots)
        {
            using var context = CreateContext(assembly, contextType, contextFactoryArgs ?? []);

            var content = DataLoaderGenerator.GenerateString(
                context.GetService<IDesignTimeModel>().Model,
                contextType,
                new GenerateOptions(resolvedOptions)
                {
                    Namespace = resolvedOptions.Namespace ?? SidecarOutput.DeriveNamespace(snapshotType.Namespace),
                });

            var path = Path.Combine(outputDir, SidecarOutput.FileName(snapshotType.Name));

            results.Add(new SidecarGenerationResult
            {
                Path = path,
                ContextType = contextType,
                Changed = SidecarOutput.AtomicWrite(path, content),
            });
        }

        return results;
    }

    private static List<(Type SnapshotType, Type ContextType)> DiscoverSnapshots(Assembly assembly)
    {
        var snapshots = new List<(Type, Type)>();

        // Snapshots are emitted without an access modifier, so they are internal.
        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(ModelSnapshot).IsAssignableFrom(type))
            {
                continue;
            }

            var contextType = type.GetCustomAttribute<DbContextAttribute>()?.ContextType
                ?? throw new InvalidOperationException(
                    $"Model snapshot '{type.FullName}' has no [DbContext] attribute, so the DbContext it belongs to "
                    + "cannot be determined.");

            snapshots.Add((type, contextType));
        }

        return snapshots;
    }

    private static DbContext CreateContext(Assembly assembly, Type contextType, string[] args)
    {
        var factoryInterface = typeof(IDesignTimeDbContextFactory<>).MakeGenericType(contextType);

        var factoryType = assembly.GetTypes()
            .FirstOrDefault(t => !t.IsAbstract && !t.IsInterface && factoryInterface.IsAssignableFrom(t));

        if (factoryType is null)
        {
            throw new InvalidOperationException(
                $"No IDesignTimeDbContextFactory<{contextType.Name}> was found in '{assembly.GetName().Name}'. "
                + "Generation needs the EF model built from the CLR types, which the model snapshot does not carry, "
                + "so a design-time factory has to be able to construct the context. Add one, or build the context "
                + "yourself and call DataLoaderGenerator.Generate.");
        }

        var factory = Activator.CreateInstance(factoryType, nonPublic: true)
            ?? throw new InvalidOperationException($"'{factoryType.FullName}' could not be constructed.");

        return (DbContext)factoryInterface
            .GetMethod(nameof(IDesignTimeDbContextFactory<DbContext>.CreateDbContext))!
            .Invoke(factory, [args])!;
    }

    /// <summary>
    /// Runs the assembly's <see cref="IDesignTimeServices"/> implementations to pick up the
    /// <see cref="GenerateOptions"/> they register, which is what <c>AddEfCoreGraphQL</c> does.
    /// </summary>
    private static GenerateOptions DiscoverOptions(Assembly assembly)
    {
        var services = new ServiceCollection();

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface || !typeof(IDesignTimeServices).IsAssignableFrom(type))
            {
                continue;
            }

            if (Activator.CreateInstance(type, nonPublic: true) is IDesignTimeServices designTimeServices)
            {
                designTimeServices.ConfigureDesignTimeServices(services);
            }
        }

        // Registered by AddEfCoreGraphQL(options)/AddEfCoreGraphQL(configure); absent for the bare
        // registration, which means defaults.
        return services
            .FirstOrDefault(d => d.ServiceType == typeof(GenerateOptions))?
            .ImplementationInstance as GenerateOptions
            ?? new GenerateOptions();
    }
}

public sealed record SidecarGenerationResult
{
    public required string Path { get; init; }

    public required Type ContextType { get; init; }

    /// <summary>
    /// True when generation changed the file. Assert this is false to fail a build on stale output.
    /// </summary>
    public required bool Changed { get; init; }
}
