namespace Stackworx.EfCoreGraphQL.DesignTime;

using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.Extensions.DependencyInjection;
using Stackworx.EfCoreGraphQL;

public class EfCoreMigrationsCodeGenerator : CSharpMigrationsGenerator
{
    private readonly GenerateOptions options;

    public EfCoreMigrationsCodeGenerator(
        MigrationsCodeGeneratorDependencies dependencies,
        CSharpMigrationsGeneratorDependencies csharpDependencies,
        IServiceProvider serviceProvider)
        : base(dependencies, csharpDependencies)
    {
        // Registered by AddEfCoreGraphQL. Absent when the generator is registered by itself, which keeps
        // the documented bare AddSingleton<IMigrationsCodeGenerator, ...> registration working.
        this.options = serviceProvider.GetService<GenerateOptions>() ?? new GenerateOptions();
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

        var outputDir = SidecarOutput.ResolveRequiredOutputDir();
        var sidecarPath = Path.Combine(outputDir, SidecarOutput.FileName(modelSnapshotName));

        var content = DataLoaderGenerator.GenerateString(
            model,
            contextType,
            new GenerateOptions(this.options)
            {
                Namespace = this.options.Namespace ?? SidecarOutput.DeriveNamespace(modelSnapshotNamespace),
            });

        SidecarOutput.AtomicWrite(sidecarPath, content);
    }
}
