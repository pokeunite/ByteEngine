using ByteEngine.Core.Runtime;

namespace ByteEngine.Tests;

/// <summary>
/// Prepares a minimal runtime folder whose loose Core and bundled-player identity
/// match the running test assembly, so the single-executable export path can be
/// exercised against a real published single-file player.
///
/// The published player keeps its own embedded Core (it only loads the payload),
/// while GamePackageExporter validates the editor/runtime identity before
/// packaging. Production publishes keep that identity aligned by copying the
/// player Core into the editor; tests build a third assembly, so they align it
/// explicitly instead of weakening the exporter guard.
/// </summary>
internal static class SingleExecutableExportTests
{
    public static string PrepareRuntime(string root, string publishedRuntime)
    {
        string source = Path.GetFullPath(publishedRuntime);
        string bundledPlayer = Path.Combine(source, "Bundled", "ByteEngine.Player.exe");
        if (!File.Exists(bundledPlayer))
            throw new InvalidOperationException("Published runtime has no Bundled\\ByteEngine.Player.exe.");
        string destination = Path.Combine(root, "Bundled Test Runtime");
        if (Directory.Exists(destination))
            Directory.Delete(destination, true);
        Directory.CreateDirectory(Path.Combine(destination, "Bundled"));
        foreach (string required in GamePackageExporter.RequiredRuntimeFiles)
        {
            if (required.Equals("ByteEngine.Core.dll", StringComparison.OrdinalIgnoreCase))
                continue;
            File.WriteAllText(Path.Combine(destination, required), "bundled test runtime placeholder");
        }
        File.Copy(typeof(GamePackageExporter).Assembly.Location,
            Path.Combine(destination, "ByteEngine.Core.dll"), true);
        File.Copy(bundledPlayer, Path.Combine(destination, "Bundled", "ByteEngine.Player.exe"), true);
        File.WriteAllText(Path.Combine(destination, "Bundled", "core-mvid.txt"),
            typeof(GamePackageExporter).Assembly.ManifestModule.ModuleVersionId.ToString());
        return destination;
    }
}
