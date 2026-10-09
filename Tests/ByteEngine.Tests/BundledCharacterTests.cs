using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Editor;
using ByteEngine.Editor.Panels;

namespace ByteEngine.Tests;

internal static class BundledCharacterTests
{
    public static void Run()
    {
        string documents = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents");
        string root = Path.Combine(documents, "ByteEngine-Mannequin-Check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var project = EditorProjectContext.Create(Path.Combine(root, "Starter", "Starter.byteproject"), _ => { });
            var ids = new List<Guid>();
            foreach (string name in BundledCharacterInstaller.ModelFiles)
            {
                string path = "Assets/Characters/ByteEngine/" + name;
                Check(project.AssetDatabase.TryGetAsset(path, out AssetRecord? record) && record != null,
                    "New project must register " + name);
                ids.Add(record!.Guid);
                ModelAsset model = project.Assets.LoadModel(new AssetReference(record.Guid, record.ProjectPath));
                Check(model.Meshes.Count == 2 && model.Materials.Count == 2, "Models retain two material sections.");
                Check(model.Skeleton != null && model.Skeleton.Bones.Count >= 36, "Model skeleton must survive GLB import.");
                Check(!model.Skeleton!.Bones.Any(b => b.Name.StartsWith("CTRL_", StringComparison.Ordinal)), "Blender controls must not ship as deform joints.");
                foreach (ImportedMesh mesh in model.Meshes)
                {
                    Check(mesh.Indices.Length > 0 && mesh.JointWeights.Length > 0, "Mesh must retain geometry and skin weights.");
                    foreach (Vector4 w in mesh.JointWeights)
                        Check(Math.Abs(w.X + w.Y + w.Z + w.W - 1) < .001f, "Imported weights must remain normalized.");
                }
                if (name.Contains("FPS", StringComparison.Ordinal))
                {
                    Check(model.Animations.Any(a => a.Name == "FPS_Ready_Pose"), "FPS ready pose must remain available.");
                    Check(!model.Skeleton.Bones.Any(b => b.Name.StartsWith("thigh.", StringComparison.Ordinal)), "FPS rig must contain arms only.");
                }
                else
                    Check(model.Skeleton.Bones.Any(b => b.Name == "head") && model.Skeleton.Bones.Any(b => b.Name == "thigh.L"), "Full body retains its head and legs.");
                Console.WriteLine($"{name}: {model.Meshes.Sum(m => m.Indices.Length / 3)} triangles, {model.Skeleton.Bones.Count} joints, {model.Animations.Count} animations.");
            }

            string notes = Path.Combine(project.ProjectRoot, "Assets", "Characters", "ByteEngine", "README.txt");
            File.WriteAllText(notes, "Project-specific character notes");
            Check(BundledCharacterInstaller.Install(project.ProjectRoot) == 0, "Reinstall must not replace project copies.");
            Check(File.ReadAllText(notes) == "Project-specific character notes", "Project edits must survive reinstall.");
            project.AssetDatabase.Scan();
            Check(ids.All(id => project.AssetDatabase.Assets.Any(a => a.Guid == id)), "Asset references must survive reinstall.");

            string staging = Path.Combine(root, "PackagedSource");
            Directory.CreateDirectory(staging);
            new ProjectSerializer().Save(new ProjectData { Name = "Game", AssetDirectory = "Content" }, Path.Combine(staging, "Game.byteproject"));
            string package = Path.Combine(root, "TestStarter.bytepak");
            ByteAssetPackage.Create(staging, package);
            string lastStand = Path.Combine(root, "PackagedStarter");
            LastStandStarterFactory.Install(lastStand, "PackagedStarter", package);
            foreach (string name in BundledCharacterInstaller.ModelFiles)
                Check(File.Exists(Path.Combine(lastStand, "Content", "Characters", "ByteEngine", name)), "Packaged starters and custom asset roots receive the mannequins.");

            string missing = Path.Combine(root, "MissingBundle");
            string destination = Path.Combine(root, "MissingDestination");
            try
            {
                BundledCharacterInstaller.Install(destination, sourceDirectory: missing);
                throw new Exception("Incomplete bundles must fail explicitly.");
            }
            catch (FileNotFoundException) { }
            Check(!Directory.Exists(destination), "Missing bundle must not partially install.");
            Console.WriteLine("Bundled character project creation, engine import, normalized skin weights, FPS pose, packaged starter and preservation checks passed.");
        }
        finally
        {
            if (Path.GetDirectoryName(Path.GetFullPath(root)) == Path.GetFullPath(documents) && Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
