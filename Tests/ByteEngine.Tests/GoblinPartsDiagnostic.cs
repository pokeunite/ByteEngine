using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Tests;

internal static class GoblinPartsDiagnostic
{
    public static void Run(string directory)
    {
        string[] files = Directory.GetFiles(directory, "*.glb");
        if (files.Length < 12) throw new InvalidOperationException("Goblin parts kit is incomplete.");
        foreach (string file in files)
        {
            var record = new AssetRecord(Guid.NewGuid(), AssetType.Model3D,
                "Assets/" + Path.GetFileName(file), file, file + ".meta",
                new AssetMetadata { Type = AssetType.Model3D });
            var imported = new GltfModelImporter().Import(record, new ModelImporterSettings());
            if (imported.Meshes.Count == 0 || imported.Meshes.Any(mesh => mesh.Vertices.Length == 0))
                throw new InvalidOperationException($"Invalid ByteEngine mesh: {file}");
            Console.WriteLine($"{Path.GetFileName(file)}: {imported.Meshes.Count} mesh(es)");
        }
        Console.WriteLine($"Goblin parts: {files.Length} GLBs imported by ByteEngine.");
    }
}
