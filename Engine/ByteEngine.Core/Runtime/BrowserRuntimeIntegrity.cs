using System.Security.Cryptography;
using System.Text.Json;

namespace ByteEngine.Core.Runtime;

/// <summary>Rejects partial or mixed browser installations before a game export is created.</summary>
public static class BrowserRuntimeIntegrity
{
    public const string ManifestName = "browser-runtime-integrity.json";

    public static int Validate(string directory)
    {
        directory = Path.GetFullPath(directory);
        string manifest = Path.Combine(directory, ManifestName);
        if (!File.Exists(manifest))
            throw new InvalidDataException("Browser runtime has no integrity manifest. Refresh it with Tools/PublishBrowser.ps1 before exporting.");
        using var document = JsonDocument.Parse(File.ReadAllText(manifest));
        var root = document.RootElement;
        if (root.GetProperty("formatVersion").GetInt32() != 1)
            throw new InvalidDataException("Unsupported browser runtime integrity manifest. Refresh the runtime.");
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in root.GetProperty("files").EnumerateObject())
        {
            string name = entry.Name;
            if (string.IsNullOrWhiteSpace(name) || name.Contains('\\') || name.Contains(':') ||
                name.StartsWith('/') || name.Split('/').Any(p => p is ".." or "." or "") || !files.Add(name))
                throw new InvalidDataException("Invalid or duplicate runtime resource path: " + name);
            string file = GamePackageExporter.ResolveInside(directory, name);
            if (!File.Exists(file)) throw new InvalidDataException("Browser runtime resource is missing: " + name);
            using var stream = File.OpenRead(file);
            string hash = Convert.ToHexString(SHA256.HashData(stream));
            if (!string.Equals(hash, entry.Value.GetString(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Browser runtime resource is stale or corrupt: " + name + ". Refresh the browser runtime before exporting.");
        }
        foreach (string required in new[] { "index.html", "main.js", "content.js", "renderer.js", "frame-wire.js",
                     "graphics.js", "water.js", "audio.js", "video.js", "performance.js",
                     "browser-plugins.json", "browser-plugin-builds.json", "_framework/dotnet.js", "_framework/blazor.boot.json" })
            if (!files.Contains(required)) throw new InvalidDataException("Runtime integrity manifest omits: " + required);

        using var boot = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "_framework", "blazor.boot.json")));
        int checkedResources = 0;
        foreach (var category in boot.RootElement.GetProperty("resources").EnumerateObject())
        {
            if (category.Value.ValueKind != JsonValueKind.Object) continue;
            foreach (var resource in category.Value.EnumerateObject())
            {
                if (resource.Value.ValueKind != JsonValueKind.String || resource.Value.GetString() is not { } expected ||
                    !expected.StartsWith("sha256-", StringComparison.Ordinal)) continue;
                string name = "_framework/" + resource.Name;
                if (!files.Contains(name)) throw new InvalidDataException("Runtime manifest omits boot resource: " + name);
                using var stream = File.OpenRead(GamePackageExporter.ResolveInside(directory, name));
                if ("sha256-" + Convert.ToBase64String(SHA256.HashData(stream)) != expected)
                    throw new InvalidDataException("Browser boot resource hash does not match: " + name);
                checkedResources++;
            }
        }
        if (checkedResources < 3) throw new InvalidDataException("Browser boot manifest has insufficient resource integrity data.");
        // Old fingerprinted binaries must not hitchhike into the next game export.
        foreach (string file in Directory.EnumerateFiles(Path.Combine(directory, "_framework"), "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(directory, file).Replace('\\', '/');
            if (!files.Contains(relative) && !relative.EndsWith(".br") && !relative.EndsWith(".gz"))
                throw new InvalidDataException("Browser runtime contains an untracked framework resource: " + relative);
        }
        return checkedResources;
    }
}
