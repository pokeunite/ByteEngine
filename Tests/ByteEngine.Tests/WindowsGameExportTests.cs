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
            var serializer = new SceneSerializer(new ComponentSerializer(projectRoot, database, assets, null), project.Classification);
            var scene = new Scene("Standalone Test");
            scene.CreateGameObject("Camera").AddComponent(new Camera3D { ActiveGameCamera = true });
            serializer.Save(scene, Path.Combine(projectRoot, project.StartupScene));
            database.Scan();
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
        if (!File.Exists(Path.Combine(content, ".byteengine", "ModelSockets", "test.sockets.json")))
            throw new Exception("Runtime socket metadata must survive content packaging.");
        var before = Directory.GetFiles(content, "*", SearchOption.AllDirectories)
            .ToDictionary(file => file, File.GetLastWriteTimeUtc);
        using (var game = new GameProjectRuntime(Path.Combine(content, "Game.byteproject")))
        {
            game.Database.TryGetAsset("Assets/data.txt", out var data);
            if (data!.Guid != dataGuid) throw new Exception("Export must preserve stable asset GUIDs.");
            var scene = game.LoadStartupScene();
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
        if (Directory.Exists(content)) throw new Exception("Private runtime content must be removed after shutdown.");
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
