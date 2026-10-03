using ByteEngine.Core.Plugins;
using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.VisualLogic;

namespace ByteEngine.Tests;

internal static class WindowsGameExportTests
{
    public static GamePackageResult Run(string root, string? realRuntime = null)
    {
        string projectRoot = Path.Combine(root, "Export Source");
        Directory.CreateDirectory(Path.Combine(projectRoot, "Assets"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "Scenes"));
        var project = new ProjectData { Name = "Export Test" };
        string projectFile = Path.Combine(projectRoot, "Test.byteproject");
        new ProjectSerializer().Save(project, projectFile);
        string? pluginPackage = Environment.GetEnvironmentVariable("BYTEENGINE_TEST_PLUGIN");
        ByteEnginePluginSession? authoringPlugins = null;
        if (pluginPackage != null)
        {
            ByteEnginePluginPackageManager.Import(projectRoot, pluginPackage);
            string Variant(string id, bool runtime)
            {
                string path = Path.Combine(root, id + ".byteplugin"); File.Copy(pluginPackage, path);
                using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
                var old = zip.GetEntry("plugin.json")!; JsonObject manifest;
                using (var reader = new StreamReader(old.Open())) manifest = JsonNode.Parse(reader.ReadToEnd())!.AsObject();
                old.Delete(); manifest["id"] = id; manifest["runtime"] = runtime;
                using var writer = new StreamWriter(zip.CreateEntry("plugin.json").Open()); writer.Write(manifest.ToJsonString());
                return path;
            }
            var disabled = ByteEnginePluginPackageManager.Import(projectRoot, Variant("tests.disabled", true)).Plugin;
            ByteEnginePluginPackageManager.SetEnabled(disabled, false);
            ByteEnginePluginPackageManager.Import(projectRoot, Variant("tests.editoronly", false));
            // Editor-only fixture is deliberately not registered in the authoring session.
            var editorOnly = ByteEnginePluginPackageManager.ListInstalled(projectRoot).Single(p => p.Id == "tests.editoronly");
            ByteEnginePluginPackageManager.SetEnabled(editorOnly, false);
            authoringPlugins = ByteEnginePluginManager.LoadProjectPlugins(projectRoot, ByteEnginePluginLoadMode.Editor);
            ByteEnginePluginPackageManager.SetEnabled(editorOnly, true);
        }
        string dataFile = Path.Combine(projectRoot, "Assets", "data.txt");
        File.WriteAllText(dataFile, "This is runtime content.");
        var blueprint = new BlueprintDefinition { Name = "Spawned Test", Root = new GameObjectData
            { Id = Guid.NewGuid(), Name = "Spawned Test" } };
        new BlueprintSerializer().Save(blueprint, Path.Combine(projectRoot, "Assets", "Spawn.byteblueprint"));
        new EventModuleSerializer().Save(new EventModuleDefinition { Name = "Spawn events" },
            Path.Combine(projectRoot, "Assets", "Spawn.byteevents"));
        Guid dataGuid;
        using (var database = new AssetDatabase(projectRoot, new[] { "Assets", "Scenes" }))
        using (var assets = new AssetManager(database))
        {
            database.TryGetAsset("Assets/data.txt", out var data);
            dataGuid = data!.Guid;
            database.TryGetAsset("Assets/Spawn.byteevents", out var events);
            blueprint.EventModules.Add(events!.Guid);
            new BlueprintSerializer().Save(blueprint, Path.Combine(projectRoot, "Assets", "Spawn.byteblueprint"));
            var components = new ComponentSerializer(projectRoot, database, assets, null);
            ByteEnginePluginRegistry.ApplyComponentCodecs(components);
            var serializer = new SceneSerializer(components, project.Classification);
            var scene = new Scene("Standalone Test");
            scene.CreateGameObject("Camera").AddComponent(new Camera3D { ActiveGameCamera = true });
            if (pluginPackage != null)
            {
                var registration = ByteEnginePluginRegistry.Components.Single(p => p.PluginId == "bytebard.spacescraper");
                var component = (Component)Activator.CreateInstance(registration.ComponentType)!;
                registration.ComponentType.GetProperty("BuildRadius")!.SetValue(component, 37f);
                scene.CreateGameObject("Space Builder").AddComponent(component);
            }
            serializer.Save(scene, Path.Combine(projectRoot, project.StartupScene));
            database.Scan();
        }
        authoringPlugins?.Dispose();
        if (pluginPackage != null)
        {
            try { WebGamePackageExporter.Export(projectFile, "unused", root, project.StartupScene); throw new Exception("Web plugin export was not blocked."); }
            catch (InvalidOperationException error) when (error.Message.Contains("managed runtime plugins")) { Console.WriteLine("PASS: web export explicitly blocks runtime plugins."); }
        }
        string metadata = Path.Combine(projectRoot, ".byteengine", "ModelSockets", "test.sockets.json");
        Directory.CreateDirectory(Path.GetDirectoryName(metadata)!);
        File.WriteAllText(metadata, "{}");
        string runtime = realRuntime ?? Path.Combine(root, "Fake Player");
        if (realRuntime == null)
        {
            Directory.CreateDirectory(runtime);
            foreach (string required in GamePackageExporter.RequiredRuntimeFiles)
                File.WriteAllText(Path.Combine(runtime, required), "test runtime placeholder");
        }
        string output = Path.Combine(root, "Exported Games");
        var result = GamePackageExporter.Export(projectFile, runtime, output, project.StartupScene);
        if (!File.Exists(result.Executable) || File.Exists(Path.Combine(result.Directory, "ByteEngine.Editor.dll")) ||
            File.Exists(Path.Combine(result.Directory, "BUILD-INCOMPLETE.txt")) ||
            !File.Exists(Path.Combine(result.Directory, ByteAssetPackage.FileName)) ||
            Directory.Exists(Path.Combine(result.Directory, "Content")))
            throw new Exception("Standalone package must contain player and runtime socket metadata, not editor.");
        GamePackageExporter.ValidatePackage(result.Directory);
        using var contentSession = new GameContentSession(result.Directory);
        string content = Path.GetDirectoryName(contentSession.ProjectFile)!;
        if (pluginPackage != null)
        {
            string[] packaged = Directory.GetFiles(Path.Combine(content, "Plugins"), "*.byteplugin");
            if (packaged.Length != 1 || !packaged[0].EndsWith("bytebard.spacescraper.byteplugin"))
                throw new Exception("Native export must include only enabled runtime plugins.");
        }
        if (!File.Exists(Path.Combine(content, ".byteengine", "ModelSockets", "test.sockets.json")))
            throw new Exception("Runtime socket metadata must survive content packaging.");
        var before = Directory.GetFiles(content, "*", SearchOption.AllDirectories)
            .ToDictionary(file => file, File.GetLastWriteTimeUtc);
        using (var game = new GameProjectRuntime(Path.Combine(content, "Game.byteproject")))
        {
            game.Database.TryGetAsset("Assets/data.txt", out var data);
            if (data!.Guid != dataGuid) throw new Exception("Export must preserve stable asset GUIDs.");
            var scene = game.LoadStartupScene();
            if (pluginPackage != null)
            {
                var builder = scene.FindGameObject("Space Builder")!.Components.Single();
                if (builder is MissingComponent || (float)builder.GetType().GetProperty("BuildRadius")!.GetValue(builder)! != 37f)
                    throw new Exception("Native exported plugin component failed to restore.");
                Console.WriteLine("PASS: Windows runtime plugin, component state, disabled/editor-only exclusions.");
            }
            if (scene.FindComponent<Camera3D>() == null) throw new Exception("Standalone startup scene must load.");
            game.Database.TryGetAsset("Assets/Spawn.byteblueprint", out var bp);
            var spawned = RuntimeSpawnService.SpawnBlueprint(scene,
                new AssetReference(bp!.Guid, bp.ProjectPath), new Vector3(2, 0, 3));
            if (spawned == null || spawned.GetComponent<EventModuleComponent>() == null ||
                Vector3.Distance(spawned.Transform.WorldPosition, new Vector3(2,0,3)) > .0001f)
                throw new Exception("Exported game must support Blueprint spawning with its Event Sheets.");
        }
        if (before.Any(pair => File.GetLastWriteTimeUtc(pair.Key) != pair.Value))
            throw new Exception("Standalone startup must not rewrite packaged assets or metadata.");
        ExpectFailure(() => GamePackageExporter.ResolveInside(content, "../escape"));
        ExpectFailure(() => GamePackageExporter.Export(projectFile, runtime,
            Path.Combine(projectRoot, "Assets", "Builds"), project.StartupScene));
        ExpectFailure(() => GamePackageExporter.Export(projectFile, runtime, output, "Scenes/Missing.bytescene"));
        string packageFile = Path.Combine(result.Directory, ByteAssetPackage.FileName);
        byte[] originalPackage = File.ReadAllBytes(packageFile);
        byte[] corruptPackage = originalPackage.ToArray();
        corruptPackage[^1] ^= 0xff;
        File.WriteAllBytes(packageFile, corruptPackage);
        ExpectFailure(() => GamePackageExporter.ValidatePackage(result.Directory));
        // Restore only this test-created package so the real-runtime --validate smoke can use it.
        File.WriteAllBytes(packageFile, originalPackage);
        contentSession.Dispose();
        if (Directory.Exists(content))
        {
            string cache = Path.Combine(content, ".byteengine", "PluginCache") + Path.DirectorySeparatorChar;
            var remaining = Directory.GetFiles(content, "*", SearchOption.AllDirectories);
            if (pluginPackage == null || remaining.Any(p => !p.StartsWith(cache, StringComparison.OrdinalIgnoreCase) || !p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
                throw new Exception("Shutdown must remove private assets; only Windows-mapped plugin DLLs may remain in the disposable cache.");
            Console.WriteLine("PASS: temporary assets removed; Windows-mapped plugin DLL cleanup deferred until process exit.");
        }
        string missingMeta = dataFile + ".meta";
        File.Move(missingMeta, missingMeta + ".test");
        ExpectFailure(() => { using var db = new AssetDatabase(projectRoot, new[] { "Assets", "Scenes" }, readOnly: true); });
        if (File.Exists(missingMeta)) throw new Exception("Read-only runtime must not regenerate missing metadata.");
        File.Move(missingMeta + ".test", missingMeta);
        Console.WriteLine("Windows game export, relocation, GUIDs, read-only content, startup scene, Blueprint events and integrity regressions passed.");
        return result;
    }

    private static void ExpectFailure(Action action)
    {
        try { action(); }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or IOException) { return; }
        throw new Exception("Unsafe or invalid game package operation should fail.");
    }
}
