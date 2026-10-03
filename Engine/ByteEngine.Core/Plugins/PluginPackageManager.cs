using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
namespace ByteEngine.Core.Plugins;

public sealed record InstalledByteEnginePlugin(string Id, string Name, string Version,
    string Directory, string ManifestPath, bool Enabled)
{
    public string? PackagePath { get; init; }
    public ByteEnginePluginManifest? Manifest { get; init; }
    public string? Error { get; init; }
}
public sealed record PluginImportResult(InstalledByteEnginePlugin Plugin, string Message);

/// <summary>Canonical project packages with disposable, content-addressed extraction.</summary>
public static class ByteEnginePluginPackageManager
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    private const long MaxFile = 256L * 1024 * 1024, MaxTotal = 512L * 1024 * 1024;
    private static string Root(string project) => Path.Combine(Path.GetFullPath(project), "Plugins");
    public static IReadOnlyList<InstalledByteEnginePlugin> ListInstalled(string projectRoot)
    {
        string root = Root(projectRoot);
        var result = new List<InstalledByteEnginePlugin>();
        if (!System.IO.Directory.Exists(root)) return result;
        foreach (string path in System.IO.Directory.EnumerateFiles(root, "*.byteplugin").Order(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var manifest = ReadPackage(path);
                string cache = Materialize(projectRoot, path, manifest);
                bool enabled = ReadEnabled(path + ".state.json");
                result.Add(new(manifest.Id, manifest.Name, manifest.Version, cache,
                    Path.Combine(cache, "plugin.json"), enabled) { PackagePath = path, Manifest = manifest });
            }
            catch (Exception error)
            {
                result.Add(new(Path.GetFileNameWithoutExtension(path), "Invalid Plugin", "?", root, path, false) { PackagePath = path, Error = error.Message });
            }
        }
        foreach (string path in System.IO.Directory.EnumerateFiles(root, "plugin.json", SearchOption.AllDirectories)
            .Concat(System.IO.Directory.EnumerateFiles(root, "plugin.json.disabled", SearchOption.AllDirectories)).Order(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var manifest = ReadManifest(File.ReadAllText(path));
                result.Add(new(manifest.Id, manifest.Name, manifest.Version, Path.GetDirectoryName(path)!, path,
                    Path.GetFileName(path) == "plugin.json" && ReadEnabled(Path.Combine(Path.GetDirectoryName(path)!, "plugin.state.json"))) { Manifest = manifest });
            }
            catch (Exception error) { result.Add(new(Path.GetFileName(Path.GetDirectoryName(path))!, "Invalid Plugin", "?", Path.GetDirectoryName(path)!, path, false) { Error = error.Message }); }
        }
        return result;
    }
    public static PluginImportResult Import(string projectRoot, string packagePath)
    {
        if (!Path.GetExtension(packagePath).Equals(".byteplugin", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose a .byteplugin package.");
        var manifest = ReadPackage(packagePath);
        if (ListInstalled(projectRoot).Any(p => p.Id.Equals(manifest.Id, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Plugin '{manifest.Id}' is already installed.");
        string cache = Materialize(projectRoot, packagePath, manifest);
        string root = Root(projectRoot); System.IO.Directory.CreateDirectory(root);
        string destination = Inside(root, manifest.Id + ".byteplugin");
        File.Copy(packagePath, destination, false);
        var installed = new InstalledByteEnginePlugin(manifest.Id, manifest.Name, manifest.Version, cache,
            Path.Combine(cache, "plugin.json"), true) { PackagePath = destination, Manifest = manifest };
        return new(installed, $"Installed {manifest.Name} v{manifest.Version}. Reopen the project to load it.");
    }
    public static string SetEnabled(InstalledByteEnginePlugin plugin, bool enabled)
    {
        string path = plugin.PackagePath == null ? Path.Combine(plugin.Directory, "plugin.state.json") : plugin.PackagePath + ".state.json";
        File.WriteAllText(path, JsonSerializer.Serialize(new { enabled }));
        // Migrate the original disabled-manifest convention without losing the manifest.
        string disabled = Path.Combine(plugin.Directory, "plugin.json.disabled"), active = Path.Combine(plugin.Directory, "plugin.json");
        if (plugin.PackagePath == null && File.Exists(disabled) && !File.Exists(active)) File.Move(disabled, active);
        return $"{(enabled ? "Enabled" : "Disabled")} {plugin.Name}. Reopen the project.";
    }
    public static string Remove(InstalledByteEnginePlugin plugin)
    {
        SetEnabled(plugin, false);
        try
        {
            if (plugin.PackagePath != null)
            {
                File.Delete(plugin.PackagePath); File.Delete(plugin.PackagePath + ".state.json");
                // A cache may remain mapped on Windows; packages are already removed.
                try { if (!Path.GetFullPath(plugin.Directory).Equals(Path.GetDirectoryName(Path.GetFullPath(plugin.PackagePath)), StringComparison.OrdinalIgnoreCase)) System.IO.Directory.Delete(plugin.Directory, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            else System.IO.Directory.Delete(plugin.Directory, true);
            return $"Removed {plugin.Name}. Reopen the project to unload its registrations.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { return $"{plugin.Name} is disabled. Restart ByteEngine to finish removing locked files."; }
    }
    public static void OpenPluginsFolder(string projectRoot)
    {
        string root = Root(projectRoot); System.IO.Directory.CreateDirectory(root);
        Process.Start(new ProcessStartInfo { FileName = root, UseShellExecute = true });
    }
    public static ByteEnginePluginManifest ReadManifest(string text)
    {
        var m = JsonSerializer.Deserialize<ByteEnginePluginManifest>(text, Json) ?? throw new InvalidDataException("Empty manifest.");
        if (string.IsNullOrWhiteSpace(m.Id) || !char.IsAsciiLetterOrDigit(m.Id[0]) ||
            m.Id.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-')))
            throw new InvalidDataException("Invalid plugin id.");
        if (string.IsNullOrWhiteSpace(m.Name) || !Version.TryParse(m.Version, out _)) throw new InvalidDataException("Invalid name or version.");
        ValidatePath(m.Assembly);
        if (!m.Assembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Assembly must be a DLL.");
        if (m.SchemaVersion != 1) throw new InvalidDataException($"Unsupported plugin schema {m.SchemaVersion}.");
        return m;
    }
    private static ByteEnginePluginManifest ReadPackage(string path)
    {
        using var zip = ZipFile.OpenRead(path); ValidateArchive(zip);
        var entry = zip.GetEntry("plugin.json") ?? throw new InvalidDataException("Missing root plugin.json.");
        if (entry.Length > 1024 * 1024) throw new InvalidDataException("Manifest exceeds 1 MB.");
        using var reader = new StreamReader(entry.Open()); var m = ReadManifest(reader.ReadToEnd());
        if (zip.GetEntry(m.Assembly.Replace('\\','/')) == null) throw new InvalidDataException("Missing plugin assembly.");
        return m;
    }
    private static string Materialize(string project, string package, ByteEnginePluginManifest manifest)
    {
        using var stream = File.OpenRead(package); string hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        string root = Path.Combine(Path.GetFullPath(project), ".byteengine", "PluginCache");
        System.IO.Directory.CreateDirectory(root); string destination = Inside(root, manifest.Id + "-" + hash);
        if (File.Exists(Path.Combine(destination, ".complete"))) return destination;
        string stage = Inside(root, "stage-" + Guid.NewGuid().ToString("N")); System.IO.Directory.CreateDirectory(stage);
        try
        {
            using var zip = ZipFile.OpenRead(package); ValidateArchive(zip);
            foreach (var entry in zip.Entries)
            {
                string relative = entry.FullName.Replace('\\','/'); string output = Inside(stage, relative);
                if (relative.EndsWith('/')) { System.IO.Directory.CreateDirectory(output); continue; }
                System.IO.Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                using var input = entry.Open(); using var target = new FileStream(output, FileMode.CreateNew);
                input.CopyTo(target);
            }
            File.WriteAllText(Path.Combine(stage, ".complete"), hash);
            if (System.IO.Directory.Exists(destination)) System.IO.Directory.Delete(destination, true);
            System.IO.Directory.Move(stage, destination); return destination;
        }
        finally { if (System.IO.Directory.Exists(stage)) System.IO.Directory.Delete(stage, true); }
    }
    private static void ValidateArchive(ZipArchive zip)
    {
        if (zip.Entries.Count is 0 or > 2048) throw new InvalidDataException("Invalid package entry count.");
        long total = 0; var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            string path = entry.FullName.Replace('\\','/'); ValidatePath(path);
            if (!paths.Add(path.TrimEnd('/'))) throw new InvalidDataException("Duplicate package path.");
            if (entry.Length > MaxFile || (total = checked(total + entry.Length)) > MaxTotal) throw new InvalidDataException("Package exceeds size limits.");
            if ((entry.ExternalAttributes >> 16 & 0xF000) == 0xA000) throw new InvalidDataException("Symbolic links are not supported.");
        }
    }
    private static void ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains(':') ||
            path.Replace('\\','/').Split('/').Any(p => p is "." or "..")) throw new InvalidDataException($"Unsafe package path: {path}");
    }
    private static string Inside(string root, string relative)
    {
        ValidatePath(relative); string full = Path.GetFullPath(Path.Combine(root, relative.Replace('/',Path.DirectorySeparatorChar)));
        if (!full.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Package path escapes its directory.");
        return full;
    }
    private static bool ReadEnabled(string path)
    {
        if (!File.Exists(path)) return true;
        try { return JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("enabled").GetBoolean(); }
        catch { return false; }
    }
}