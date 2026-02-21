namespace Stackworx.EfCoreGraphQL.DesignTime;

using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Stackworx.EfCoreGraphQL;

public class EfCoreMigrationsCodeGenerator(
    MigrationsCodeGeneratorDependencies dependencies,
    CSharpMigrationsGeneratorDependencies csharpDependencies)
    : CSharpMigrationsGenerator(dependencies, csharpDependencies)
{
    // The generator runs during design-time migration scaffolding.
    // When startup project != target project, CWD is often not the migrations project.
    // To avoid writing files into the wrong repo folder, we require an explicit output directory.
    private const string SidecarOutputDirEnvVar = "STACKWORX_EFCOREGRAPHQL_SIDECAR_OUTPUT_DIR";

    public override string GenerateSnapshot(string? modelSnapshotNamespace, Type contextType, string modelSnapshotName,
        IModel model)
    {
        var snapshotCode = base.GenerateSnapshot(modelSnapshotNamespace, contextType, modelSnapshotName, model);
        TryGenerateSidecar(snapshotCode, modelSnapshotNamespace, modelSnapshotName, model, contextType);
        return snapshotCode;
    }

    private static void TryGenerateSidecar(string snapshotCode,
        string? modelSnapshotNamespace,
        string modelSnapshotName,
        IModel model,
        Type contextType)
    {
        // We intentionally fingerprint the snapshot code (not the model) because it already captures
        // provider-specific annotations and is stable across EF versions for a given model.
        var snapshotHash = Hash(snapshotCode);

        // Sidecar + its hash live next to each other.
        var baseName = modelSnapshotName; // typically "{DbContext}ModelSnapshot"
        var sidecarFileName = baseName + ".DataLoaders.g.cs";
        var hashFileName = baseName + ".DataLoaders.g.hash";

        var outputDir = ResolveRequiredOutputDir();
        var sidecarPath = Path.Combine(outputDir, sidecarFileName);
        var hashPath = Path.Combine(outputDir, hashFileName);

        var existingHash = File.Exists(hashPath)
            ? File.ReadAllText(hashPath).TrimStart('\uFEFF').Trim()
            : null;
        if (string.Equals(existingHash, snapshotHash, StringComparison.OrdinalIgnoreCase))
        {
            return; // no schema/model change
        }

        // Generate file content
        var @namespace = string.IsNullOrWhiteSpace(modelSnapshotNamespace)
            ? "Generated.DataLoaders"
            : modelSnapshotNamespace + ".Generated.DataLoaders";

        var content = DataLoaderGenerator.GenerateString(
            model,
            contextType,
            new GenerateOptions
            {
                Namespace = @namespace,
                // Keep defaults for Mode/Filter; consumers can later add configurability.
            });

        AtomicWrite(sidecarPath, content);
        AtomicWrite(hashPath, snapshotHash + Environment.NewLine);
    }

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

    private static string Hash(string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
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