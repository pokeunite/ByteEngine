using System.Security.Cryptography;
using System.Text.Json;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Plugins;
using ByteEngine.Core.Serialization;

namespace ByteEngine.Core.Runtime;

public sealed record GamePackageResult(string Directory, string Executable, int ContentFiles, long ContentBytes);

/// <summary>Packages a prepublished Windows player. Never invokes a compiler on the user's machine.</summary>
public static class GamePackageExporter
{
    public static readonly string[] RequiredRuntimeFiles =
    {
        "ByteEngine.Player.exe", "ByteEngine.Player.dll", "ByteEngine.Player.deps.json",
        "ByteEngine.Player.runtimeconfig.json", "ByteEngine.Core.dll", "coreclr.dll",
        "hostfxr.dll", "hostpolicy.dll", "openal32.dll", "glfw3.dll", "assimp.dll"
    };

    public static GamePackageResult Export(string projectFile, string runtimeDirectory,
        string outputParent, string startupScene, IProgress<string>? progress = null)
    {
        var project = new ProjectSerializer().Load(projectFile);
        string root = Path.GetDirectoryName(Path.GetFullPath(projectFile))!;
        project.StartupScene = startupScene.Replace('\\', '/');
        string scene = ResolveInside(root, project.StartupScene);
        if (!File.Exists(scene)) throw new FileNotFoundException("Choose a saved startup scene.", scene);
        using (var json = JsonDocument.Parse(File.ReadAllText(scene)))
            if (json.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Startup scene must contain scene data.");
        runtimeDirectory = Path.GetFullPath(runtimeDirectory);
        foreach (string required in RequiredRuntimeFiles)
            if (!File.Exists(Path.Combine(runtimeDirectory, required)))
                throw new FileNotFoundException($"Windows player runtime is incomplete: {required}. Refresh the engine distribution.");

        // AssemblyVersion stays stable; compare actual build identities before packaging plugins.
        using (var file = File.OpenRead(Path.Combine(runtimeDirectory, "ByteEngine.Core.dll")))
        using (var pe = new PEReader(file))
        {
            var metadata = pe.GetMetadataReader();
            var runtimeId = metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
            if (runtimeId != typeof(GamePackageExporter).Assembly.ManifestModule.ModuleVersionId)
                throw new InvalidOperationException("The Windows player runtime differs from this editor. Refresh PlayerRuntime before exporting; mismatched runtimes can lose terrain and plugin components.");
        }

        string[] roots = new[] { project.AssetDirectory, project.SceneDirectory }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (string directory in roots)
        {
            string source = ResolveInside(root, directory);
            if (!System.IO.Directory.Exists(source))
                throw new DirectoryNotFoundException($"Project content directory is missing: {directory}");
        }
        string parent = Path.GetFullPath(outputParent);
        foreach (string directory in roots)
            if (IsInside(ResolveInside(root, directory), parent))
                throw new InvalidOperationException("Choose an export location outside the Assets and Scenes directories.");
        if (IsInside(ResolveInside(root, ".byteengine"), parent))
            throw new InvalidOperationException("Choose an export location outside the project metadata directory.");
        if (IsInside(runtimeDirectory, parent))
            throw new InvalidOperationException("The export location cannot be inside the player runtime.");
        // Fail before creating a build when GUID metadata is incomplete or duplicated.
        using (var validation = new AssetDatabase(root, roots, readOnly: true)) { }
        string safeName = SafeName(project.Name);
        string destination = Path.Combine(parent, safeName + "-Windows-" +
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
        System.IO.Directory.CreateDirectory(destination);
        string marker = Path.Combine(destination, "BUILD-INCOMPLETE.txt");
        File.WriteAllText(marker, "This build is not complete. Do not distribute it. See build-error.txt if present.");
        string content = Path.Combine(destination, "Content");
        try
        {
            progress?.Report("Copying standalone Windows runtime...");
            CopyTree(runtimeDirectory, destination, false);
            System.IO.Directory.CreateDirectory(content);
            foreach (string directory in roots)
            {
                progress?.Report("Packing " + directory + "...");
                CopyTree(ResolveInside(root, directory), ResolveInside(content, directory), true);
            }
            // Model-owned animation libraries, event metadata and sockets are runtime content.
            foreach (string directory in new[] { ".byteengine/ModelAnimations", ".byteengine/ModelSockets" })
            {
                string source = ResolveInside(root, directory);
                if (System.IO.Directory.Exists(source))
                    CopyTree(source, ResolveInside(content, directory), true);
            }

            foreach (var plugin in ByteEnginePluginPackageManager.ListInstalled(root).Where(p => p.Enabled))
            {
                var manifest = ByteEnginePluginPackageManager.ReadManifest(File.ReadAllText(plugin.ManifestPath));
                if (!manifest.Runtime) continue;
                progress?.Report("Packing plugin " + manifest.Name + "...");
                string destinationPlugins = ResolveInside(content, "Plugins");
                System.IO.Directory.CreateDirectory(destinationPlugins);
                if (plugin.PackagePath != null)
                    File.Copy(plugin.PackagePath, Path.Combine(destinationPlugins, Path.GetFileName(plugin.PackagePath)), true);
                else CopyTree(plugin.Directory, Path.Combine(destinationPlugins, manifest.Id), true);
            }

            // StartupScene need not be under the normal scene directory.
            string exportedScene = ResolveInside(content, project.StartupScene);
            if (!File.Exists(exportedScene))
                throw new InvalidOperationException("Startup scene must be located within the configured Assets or Scenes directory.");
            new ProjectSerializer().Save(project, Path.Combine(content, "Game.byteproject"));
            progress?.Report("Verifying packaged assets...");
            using (var validation = new AssetDatabase(content, roots, readOnly: true)) { }
            string[] contentFiles = WalkFiles(content).ToArray();
            long contentBytes = contentFiles.Sum(file => new FileInfo(file).Length);
            progress?.Report("Compressing game assets into Game.bytepak...");
            string package = Path.Combine(destination, ByteAssetPackage.FileName);
            ByteAssetPackage.Create(content, package);
            var entries = new[] { new PackageFile(ByteAssetPackage.FileName,
                new FileInfo(package).Length, HashFile(package)) };
            System.IO.Directory.Delete(content, true);
            CopyAssetNotice(root, destination);
            File.WriteAllText(Path.Combine(destination, "game-package.json"),
                JsonSerializer.Serialize(new { formatVersion = 2, target = "win-x64",
                    engineVersion = ByteEngineInfo.Version, project.Name, startupScene = project.StartupScene,
                    files = entries }, new JsonSerializerOptions { WriteIndented = true }));
            string executable = Path.Combine(destination, safeName + ".exe");
            File.Move(Path.Combine(destination, "ByteEngine.Player.exe"), executable);
            File.WriteAllText(Path.Combine(destination, "README.txt"),
                $"Launch {safeName}.exe. Keep this ENTIRE folder together; zip the folder to share it.\r\n" +
                "Windows x64 with a compatible OpenGL graphics driver is required. .NET installation is not required.\r\n" +
                "Native libraries require the Microsoft Visual C++ x64 v14 Redistributable. Use the included official install link if the game reports a missing native dependency.\r\n" +
                "Escape releases the mouse; click to recapture. Use the window close button to quit.\r\n" +
                "Game logs: %LOCALAPPDATA%\\ByteEngine\\Games\\Logs\r\n" +
                "Game assets/scenes and project-local runtime plugins are stored in Game.bytepak. Keep that file beside the executable.\r\n");
            File.WriteAllText(Path.Combine(destination, "Install Visual C++ Runtime.url"),
                "[InternetShortcut]\r\nURL=https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist\r\n");
            File.Delete(marker);
            return new GamePackageResult(destination, executable, contentFiles.Length, contentBytes);
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(destination, "build-error.txt"), error.ToString());
            throw new IOException($"Export failed. The incomplete build was retained at '{destination}'. {error.Message}", error);
        }
        finally
        {
            // Only this export's staging directory, never source project content.
            if (System.IO.Directory.Exists(content)) System.IO.Directory.Delete(content, true);
        }
    }

    internal static void CopyAssetNotice(string projectRoot, string destination)
    {
        string notice = Path.Combine(projectRoot, "ASSET-NOTICE.txt");
        if (File.Exists(notice)) File.Copy(notice, Path.Combine(destination, "ASSET-NOTICE.txt"), true);
    }

    public static void ValidatePackage(string directory)
    {
        string manifest = Path.Combine(directory, "game-package.json");
        using var json = JsonDocument.Parse(File.ReadAllText(manifest));
        foreach (var entry in json.RootElement.GetProperty("files").EnumerateArray())
        {
            string path = ResolveInside(directory, entry.GetProperty("Path").GetString()!);
            string expected = entry.GetProperty("Sha256").GetString()!;
            if (!File.Exists(path) || new FileInfo(path).Length != entry.GetProperty("Length").GetInt64() ||
                !string.Equals(HashFile(path), expected, StringComparison.Ordinal))
                throw new InvalidDataException($"Game content is missing or corrupted: {path}");
        }
    }

    public static string ResolveInside(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
            throw new InvalidDataException("Content paths must be project-relative.");
        string full = Path.GetFullPath(Path.Combine(root, relative));
        if (!IsInside(root, full) || string.Equals(full, Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Content path escapes its directory: {relative}");
        return full;
    }

    internal static bool IsInside(string root, string path) =>
        string.Equals(Path.GetFullPath(root), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase) ||
        Path.GetFullPath(path).StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) +
            Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    internal static string SafeName(string name)
    {
        string result = new(name.Select(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray());
        result = result.Trim('_');
        if (result.Length == 0) result = "Game";
        if (result.Length > 64) result = result[..64];
        if (new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6",
            "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }
            .Contains(result, StringComparer.OrdinalIgnoreCase)) result = "Game_" + result;
        return result;
    }

    internal static IEnumerable<string> WalkFiles(string directory)
    {
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            throw new IOException($"Linked directories are not supported in game exports: {directory}");
        foreach (string entry in System.IO.Directory.EnumerateFileSystemEntries(directory))
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Linked content is not supported in game exports: {entry}");
            if ((attributes & FileAttributes.Directory) != 0)
            {
                foreach (string file in WalkFiles(entry)) yield return file;
            }
            else yield return entry;
        }
    }

    internal static void CopyTree(string source, string destination, bool content)
    {
        System.IO.Directory.CreateDirectory(destination);
        foreach (string file in WalkFiles(source))
        {
            if (content && (file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
                file.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))) continue;
            string target = ResolveInside(destination, Path.GetRelativePath(source, file));
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private sealed record PackageFile(string Path, long Length, string Sha256);
}
