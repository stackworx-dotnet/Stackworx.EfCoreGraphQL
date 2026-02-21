namespace Stackworx.EfCoreGraphQL.DesignTime;

using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Stackworx.EfCoreGraphQL;

public class MyMigrationsCodeGenerator(MigrationsCodeGeneratorDependencies dependencies,
    CSharpMigrationsGeneratorDependencies csharpDependencies)
    : CSharpMigrationsGenerator(dependencies, csharpDependencies)
{
    public override string GenerateSnapshot(string? modelSnapshotNamespace, Type contextType, string modelSnapshotName, IModel model)
    {
        var snapshotCode = base.GenerateSnapshot(modelSnapshotNamespace, contextType, modelSnapshotName, model);
        TryGenerateSidecar(snapshotCode, modelSnapshotNamespace, modelSnapshotName, model);
        return snapshotCode;
    }

    private static void TryGenerateSidecar(
        string snapshotCode,
        string? modelSnapshotNamespace,
        string modelSnapshotName,
        IModel model)
    {
        try
        {
            // We intentionally fingerprint the snapshot code (not the model) because it already captures
            // provider-specific annotations and is stable across EF versions for a given model.
            var snapshotHash = Hash(snapshotCode);

            // Sidecar + its hash live next to each other.
            // Since EF doesn’t tell us the snapshot file path, we write relative to CWD.
            // In migrations scaffolding, that’s the project directory containing the Migrations folder.
            // If your tooling runs with a different working directory, set it accordingly.
            var baseName = modelSnapshotName; // typically "{DbContext}ModelSnapshot"
            var sidecarFileName = baseName + ".DataLoaders.g.cs";
            var hashFileName = baseName + ".DataLoaders.g.hash";

            var outputDir = Environment.CurrentDirectory;
            var sidecarPath = Path.Combine(outputDir, sidecarFileName);
            var hashPath = Path.Combine(outputDir, hashFileName);

            var existingHash = File.Exists(hashPath) ? File.ReadAllText(hashPath).Trim() : null;
            if (string.Equals(existingHash, snapshotHash, StringComparison.OrdinalIgnoreCase))
            {
                return; // no schema/model change
            }

            // Generate file content
            var content = DataLoaderGenerator.GenerateString(
                model,
                new GenerateOptions
                {
                    Namespace = modelSnapshotNamespace + ".Generated.DataLoaders",
                    // Keep defaults for Mode/Filter; consumers can later add configurability.
                });

            AtomicWrite(sidecarPath, content);
            AtomicWrite(hashPath, snapshotHash + Environment.NewLine);
        }
        catch
        {
            // Don’t break migrations scaffolding if sidecar generation fails.
            // (If you want strict mode, we can add an environment variable toggle.)
        }
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
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        File.Move(tmp, path);
    }
}