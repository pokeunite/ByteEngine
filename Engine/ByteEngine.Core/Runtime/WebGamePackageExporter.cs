using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Serialization;

namespace ByteEngine.Core.Runtime;

public static class WebGamePackageExporter
{
    public static GamePackageResult Export(string projectFile, string runtimeDirectory,
        string outputParent, string startupScene, IProgress<string>? progress = null)
    {
        var project = new ProjectSerializer().Load(projectFile);
        string root = Path.GetDirectoryName(Path.GetFullPath(projectFile))!;
        project.StartupScene = startupScene.Replace('\\', '/');
        if (!File.Exists(GamePackageExporter.ResolveInside(root, project.StartupScene)))
            throw new FileNotFoundException("Choose a saved startup scene.");
        runtimeDirectory = Path.GetFullPath(runtimeDirectory);
        foreach (string file in new[] { "index.html", "main.js", "content.js", "renderer.js", "audio.js", "_framework/dotnet.js" })
            if (!File.Exists(GamePackageExporter.ResolveInside(runtimeDirectory, file)))
                throw new FileNotFoundException("Browser player runtime is missing: " + file);
        string parent = Path.GetFullPath(outputParent);
        var roots = new[] { project.AssetDirectory, project.SceneDirectory }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (string directory in roots.Append(".byteengine"))
            if (GamePackageExporter.IsInside(GamePackageExporter.ResolveInside(root, directory), parent))
                throw new InvalidOperationException("Export outside project content directories.");
        if (GamePackageExporter.IsInside(runtimeDirectory, parent))
            throw new InvalidOperationException("Export outside the browser runtime.");
        using var database = new AssetDatabase(root, roots, readOnly: true);
        // Reject known desktop-only render components instead of silently crashing at runtime.
        foreach (var asset in database.Assets.Where(a => a.Type == AssetType.Scene || a.Type == AssetType.Blueprint))
            ValidateComponents(asset.FullPath);
        string destination = Path.Combine(parent, GamePackageExporter.SafeName(project.Name) + "-Web-" +
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(destination);
        string marker = Path.Combine(destination, "BUILD-INCOMPLETE.txt");
        File.WriteAllText(marker, "Incomplete web export. Do not distribute.");
        string content = Path.Combine(destination, "site", "Content");
        try
        {
            string site = Path.Combine(destination, "site");
            GamePackageExporter.CopyTree(runtimeDirectory, site, false);
            // Ship uncompressed runtime files: works on ordinary static hosts without custom encoding headers.
            foreach (string file in GamePackageExporter.WalkFiles(site).Where(f => f.EndsWith(".br") || f.EndsWith(".gz")))
                File.Delete(file);
            foreach (string directory in roots)
                GamePackageExporter.CopyTree(GamePackageExporter.ResolveInside(root, directory),
                    GamePackageExporter.ResolveInside(content, directory), true);
            foreach (string directory in new[] { ".byteengine/ModelAnimations", ".byteengine/ModelSockets" })
            {
                string source = GamePackageExporter.ResolveInside(root, directory);
                if (Directory.Exists(source)) GamePackageExporter.CopyTree(source,
                    GamePackageExporter.ResolveInside(content, directory), true);
            }
            if (!File.Exists(GamePackageExporter.ResolveInside(content, project.StartupScene)))
                throw new InvalidOperationException("Startup scene must be in the Assets or Scenes directory.");
            foreach (var model in database.Assets.Where(a => a.Type == AssetType.Model3D))
            {
                progress?.Report("Preparing model: " + model.ProjectPath);
                CookedModelStore.Save(content, ModelImporter.ForPath(model.FullPath).Import(model, model.Metadata.ModelImporter));
            }
            new ProjectSerializer().Save(project, Path.Combine(content, "Game.byteproject"));
            int contentCount = GamePackageExporter.WalkFiles(content).Count();
            string packageRoot = Path.Combine(destination, "PackageStaging");
            Directory.CreateDirectory(packageRoot);
            Directory.Move(content, Path.Combine(packageRoot, "Content"));
            string resources = Path.Combine(site, "Resources");
            if (Directory.Exists(resources)) Directory.Move(resources, Path.Combine(packageRoot, "Resources"));
            string package = Path.Combine(site, ByteAssetPackage.FileName);
            progress?.Report("Compressing browser assets...");
            ByteAssetPackage.Create(packageRoot, package);
            Directory.Delete(packageRoot, true);
            GamePackageExporter.CopyAssetNotice(root, site);
            var paths = new[] { package };
            if (paths.GroupBy(p => Path.GetRelativePath(site, p).Replace('\\', '/'), StringComparer.OrdinalIgnoreCase)
                .Any(g => g.Count() > 1)) throw new InvalidDataException("Content has case-colliding file names.");
            var entries = paths.Select(file => new {
                path = Path.GetRelativePath(site, file).Replace('\\', '/'),
                size = new FileInfo(file).Length,
                sha256 = Hash(file)
            }).ToArray();
            File.WriteAllText(Path.Combine(site, "web-game.json"), JsonSerializer.Serialize(new {
                formatVersion = 2, name = project.Name, files = entries
            }));
            var siteFiles = GamePackageExporter.WalkFiles(site).ToArray();
            if (siteFiles.Length > 1000 || siteFiles.Sum(f => new FileInfo(f).Length) > 500L * 1024 * 1024 ||
                siteFiles.Any(f => new FileInfo(f).Length > 200L * 1024 * 1024 || Path.GetRelativePath(site, f).Length > 240))
                throw new InvalidOperationException("Export exceeds itch.io's default HTML file count, size or path limits. Reduce project content.");
            progress?.Report("Creating itch.io ZIP...");
            string zip = Path.Combine(destination, GamePackageExporter.SafeName(project.Name) + "-Web.zip");
            ZipFile.CreateFromDirectory(site, zip, CompressionLevel.Optimal, false);
            File.WriteAllText(Path.Combine(destination, "README.txt"),
                "Upload the ZIP as an HTML game on itch.io. index.html is at the ZIP root.\n" +
                "Desktop browser with WebGL 2 required. Click the game to activate audio/input. Escape releases mouse.\n" +
                "Browser renderer uses reduced graphics: no desktop shadow/IBL/postprocessing parity.\n" +
                "Test privately before release. To test locally, serve the site directory over HTTP, not file://.\n");
            File.Delete(marker);
            return new GamePackageResult(destination, zip, contentCount, entries.Sum(e => e.size));
        }
        catch (Exception error) { File.WriteAllText(Path.Combine(destination, "build-error.txt"), error.ToString()); throw; }
        finally
        {
            if (Directory.Exists(content)) Directory.Delete(content, true);
            string staging = Path.Combine(destination, "PackageStaging");
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
        }
    }

    private static string Hash(string file)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
    private static void ValidateComponents(string file)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(file));
        Visit(json.RootElement);
        static void Visit(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name.Equals("type", StringComparison.OrdinalIgnoreCase) &&
                        property.Value.ValueKind == JsonValueKind.String &&
                        property.Value.GetString() is "SpriteRenderer" or "ArenaGameManager")
                        throw new NotSupportedException("This web build does not support " + property.Value.GetString() + ".");
                    Visit(property.Value);
                }
            else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) Visit(item);
        }
    }
}
