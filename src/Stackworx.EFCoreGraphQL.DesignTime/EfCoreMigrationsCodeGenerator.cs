namespace Stackworx.EfCoreGraphQL.DesignTime;

using System.Text;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.Extensions.DependencyInjection;
using Stackworx.EfCoreGraphQL;

public class EfCoreMigrationsCodeGenerator : CSharpMigrationsGenerator
{
    // The generator runs during design-time migration scaffolding.
    // When startup project != target project, CWD is often not the migrations project.
    // To avoid writing files into the wrong repo folder, we require an explicit output directory.
    private const string SidecarOutputDirEnvVar = "STACKWORX_EFCOREGRAPHQL_SIDECAR_OUTPUT_DIR";

    private readonly GenerateOptions options;

    private readonly Action<string>? writeWarning;

    public EfCoreMigrationsCodeGenerator(
        MigrationsCodeGeneratorDependencies dependencies,
        CSharpMigrationsGeneratorDependencies csharpDependencies,
        IServiceProvider serviceProvider)
        : base(dependencies, csharpDependencies)
    {
        // Registered by AddEfCoreGraphQL. Absent when the generator is registered by itself, which keeps
        // the documented bare AddSingleton<IMigrationsCodeGenerator, ...> registration working.
        this.options = serviceProvider.GetService<GenerateOptions>() ?? new GenerateOptions();

        // IOperationReporter is how a message reaches `dotnet ef` output, and EF only exposes it from an
        // Internal namespace. Keep it to this line so nothing else in the type depends on it.
#pragma warning disable EF1001
        this.writeWarning = serviceProvider.GetService<IOperationReporter>() is { } reporter
            ? reporter.WriteWarning
            : null;
#pragma warning restore EF1001
    }

    public override string GenerateSnapshot(string? modelSnapshotNamespace, Type contextType, string modelSnapshotName,
        IModel model)
    {
        var snapshotCode = base.GenerateSnapshot(modelSnapshotNamespace, contextType, modelSnapshotName, model);
        this.TryGenerateSidecar(modelSnapshotNamespace, modelSnapshotName, model, contextType);
        return snapshotCode;
    }

    private void TryGenerateSidecar(
        string? modelSnapshotNamespace,
        string modelSnapshotName,
        IModel model,
        Type contextType)
    {
        // Sidecar file is generated on every snapshot generation.
        // This avoids stale output when generation-affecting changes (e.g. GraphQLIgnore attributes)
        // don't influence the EF model snapshot text.

        var baseName = modelSnapshotName; // typically "{DbContext}ModelSnapshot"
        var sidecarFileName = baseName + ".DataLoaders.g.cs";

        if (HasNoClrTypes(model))
        {
            this.writeWarning?.Invoke(
                $"Stackworx.EfCoreGraphQL left '{sidecarFileName}' unchanged: this snapshot was rebuilt from a migration, whose model declares entity types by name and carries no CLR types, so DataLoaders cannot be generated from it. " +
                "The sidecar still describes the model as it was before this operation; the next 'dotnet ef migrations add' or 'dotnet ef migrations scaffold' will bring it back in sync.");
            return;
        }

        var outputDir = ResolveRequiredOutputDir();
        var sidecarPath = Path.Combine(outputDir, sidecarFileName);

        var content = DataLoaderGenerator.GenerateString(
            model,
            contextType,
            new GenerateOptions(this.options)
            {
                Namespace = this.options.Namespace ?? DeriveNamespace(modelSnapshotNamespace),
            });

        AtomicWrite(sidecarPath, content);
    }

    /// <summary>
    /// Whether the model carries no CLR types, as one rebuilt from a migration's
    /// <c>BuildTargetModel</c> does — <c>migrations remove</c> reverts the snapshot that way.
    /// </summary>
    /// <remarks>
    /// Such a model declares its entity types by name, so every one of them is a property bag. An
    /// ordinary model can hold property bags too — an implicit many-to-many join entity is one — but
    /// never for every entity type, because the ends it joins are CLR-backed.
    /// </remarks>
    private static bool HasNoClrTypes(IModel model)
    {
        var entityTypes = model.GetEntityTypes();

        return entityTypes.Any()
               && entityTypes.All(e => e.ClrType == typeof(Dictionary<string, object>));
    }

    private static string DeriveNamespace(string? modelSnapshotNamespace)
        => string.IsNullOrWhiteSpace(modelSnapshotNamespace)
            ? GenerateOptions.DefaultNamespace
            : modelSnapshotNamespace + "." + GenerateOptions.DefaultNamespace;

    private static string ResolveRequiredOutputDir()
    {
        var configured = Environment.GetEnvironmentVariable(SidecarOutputDirEnvVar);
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                $"{nameof(Stackworx)} EFCoreGraphQL sidecar generation requires the environment variable '{SidecarOutputDirEnvVar}' to be set to the directory where sidecar files should be written (typically the migrations folder or the project directory containing the snapshot). " +
                "This is required to avoid writing files to an unexpected working directory when the EF Core Startup Project differs from the Target Project.");
        }

        var fullPath = Path.GetFullPath(configured);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException(
                $"Environment variable '{SidecarOutputDirEnvVar}' points to '{configured}', but that directory does not exist (resolved to '{fullPath}').");
        }

        return fullPath;
    }


    private static void AtomicWrite(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Environment.CurrentDirectory);

        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content, Encoding.UTF8);

        // Replace is atomic on Windows; on Unix it's effectively atomic within a filesystem.
        File.Move(tmp, path, overwrite: true);
    }
}
