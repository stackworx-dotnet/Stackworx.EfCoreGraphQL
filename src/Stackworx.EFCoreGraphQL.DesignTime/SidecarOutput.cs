namespace Stackworx.EfCoreGraphQL.DesignTime;

using System.Text;
using Stackworx.EfCoreGraphQL;

/// <summary>
/// Where sidecar files go and what they are called. Shared by the scaffolding hook and the generate-only
/// entry point so the two write the same bytes to the same path.
/// </summary>
internal static class SidecarOutput
{
    // The generator runs during design-time operations. When startup project != target project, CWD is
    // often not the migrations project, so writing relative to it would land files in the wrong repo
    // folder. An explicit output directory is required instead.
    internal const string OutputDirEnvVar = "STACKWORX_EFCOREGRAPHQL_SIDECAR_OUTPUT_DIR";

    internal static string FileName(string modelSnapshotName)
        => modelSnapshotName + ".DataLoaders.g.cs";

    internal static string DeriveNamespace(string? modelSnapshotNamespace)
        => string.IsNullOrWhiteSpace(modelSnapshotNamespace)
            ? GenerateOptions.DefaultNamespace
            : modelSnapshotNamespace + "." + GenerateOptions.DefaultNamespace;

    internal static string ResolveRequiredOutputDir(string? configured = null)
    {
        configured ??= Environment.GetEnvironmentVariable(OutputDirEnvVar);

        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                $"{nameof(Stackworx)} EFCoreGraphQL sidecar generation requires the environment variable '{OutputDirEnvVar}' to be set to the directory where sidecar files should be written (typically the migrations folder or the project directory containing the snapshot). " +
                "This is required to avoid writing files to an unexpected working directory when the EF Core Startup Project differs from the Target Project.");
        }

        var fullPath = Path.GetFullPath(configured);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException(
                $"Environment variable '{OutputDirEnvVar}' points to '{configured}', but that directory does not exist (resolved to '{fullPath}').");
        }

        return fullPath;
    }

    /// <returns>True when <paramref name="content"/> differs from what was already on disk.</returns>
    internal static bool AtomicWrite(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Environment.CurrentDirectory);

        var changed = !File.Exists(path) || !string.Equals(File.ReadAllText(path), content, StringComparison.Ordinal);

        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content, Encoding.UTF8);

        // Replace is atomic on Windows; on Unix it's effectively atomic within a filesystem.
        File.Move(tmp, path, overwrite: true);

        return changed;
    }
}
