using System.Text;
using ByteEngine.Core.Assets;

namespace ByteEngine.Tests;

internal static class ModelLodCacheTests
{
    internal static void Run()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "LodCacheFixtures", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Assets"));
        string path = Path.Combine(root, "Assets", "grid.obj");
        var obj = new StringBuilder("o Grid\n");
        const int side = 12;
        for (int y = 0; y < side; y++) for (int x = 0; x < side; x++) obj.AppendLine($"v {x} {y} 0");
        for (int y = 0; y < side - 1; y++) for (int x = 0; x < side - 1; x++)
        {
            int a = y * side + x + 1;
            obj.AppendLine($"f {a} {a + 1} {a + side}");
            obj.AppendLine($"f {a + 1} {a + side + 1} {a + side}");
        }
        File.WriteAllText(path, obj.ToString());
        using var database = new AssetDatabase(root, ["Assets"]);
        var record = database.Assets.Single(a => a.Type == AssetType.Model3D);
        record.Metadata.ModelImporter.GenerateLods = false;
        database.SetModelImporterSettings(record.Guid, 1, true, true);
        database.RefreshPaths([path]);
        using var assets = new AssetManager(database);
        var reference = new AssetReference(record.Guid, record.ProjectPath);
        var model = assets.LoadModel(reference);
        var meshReference = new ModelMeshReference(reference, model.Meshes[0].Key);
        var original = assets.GetModelMesh(reference, meshReference.SubAssetKey);
        Check(ReferenceEquals(assets.GetModelLodMesh(meshReference, 1), original), "Missing LOD fallback");
        record = database.Resolve(reference)!;
        record.Metadata.ModelImporter.GenerateLods = true;
        database.SetModelImporterSettings(record.Guid, 1, true, true);
        database.RefreshPaths([path]);
        var deadline = System.Diagnostics.Stopwatch.StartNew();
        while (assets.PendingImports > 0 && deadline.Elapsed.TotalSeconds < 5)
        {
            database.Update();
            Thread.Sleep(1);
        }
        database.Update();
        Check(assets.PendingImports == 0, "Asynchronous reimport did not finish");
        var lod = assets.GetModelLodMesh(meshReference, 1);
        Check(!ReferenceEquals(lod, original) && lod.IndexCount < original.IndexCount, "Reimport leaves stale missing-LOD fallback");
        for (int i = 0; i < 100; i++) assets.GetModelLodMesh(meshReference, 1);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++)
            if (!ReferenceEquals(assets.GetModelLodMesh(meshReference, 1), lod)) throw new Exception("LOD cache identity changed");
        Check(GC.GetAllocatedBytesForCurrentThread() - before < 1024, "Repeated LOD lookup allocates per frame");
        Console.WriteLine("PASS LOD cache: missing variant fallback, real generated LOD after reimport, stable identity and allocation-free repeated lookup");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
