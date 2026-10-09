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
                    Check(model.Animations.Count == 103, $"FPS rig must import every bundled animation clip (imported {model.Animations.Count}, expected 103).");
                    Check(model.Animations.Any(a => a.Name == "PSX_pistol_reload") &&
                        model.Animations.Any(a => a.Name == "UAL_Pistol_Shoot"), "FPS pistol clips must survive engine import.");
                    Check(!model.Skeleton.Bones.Any(b => b.Name.StartsWith("thigh.", StringComparison.Ordinal)), "FPS rig must contain arms only.");
                }
                else
                {
                    Check(model.Animations.Count == 146, $"Full body must import every bundled animation clip and root-motion variant (imported {model.Animations.Count}, expected 146).");
                    Check(model.Skeleton.Bones.Any(b => b.Name == "head") && model.Skeleton.Bones.Any(b => b.Name == "thigh.L"), "Full body retains its head and legs.");
                }
                foreach (ImportedAnimation clip in model.Animations)
                {
                    Check(float.IsFinite(clip.Duration) && clip.Duration > 0, "Every clip must have a finite positive duration.");
                    Check(clip.Channels.Count > 0, "Every clip must contain animation channels.");
                    foreach (ImportedAnimationChannel channel in clip.Channels)
                    {
                        if (channel.Rotation is { } rotation)
                        {
                            float previousTime = -1;
                            foreach (ImportedQuaternionKey key in rotation.Keys)
                            {
                                Check(float.IsFinite(key.Time) && key.Time >= previousTime, "Animation keys must be ordered.");
                                Check(float.IsFinite(key.Value.Length()) && Math.Abs(key.Value.Length() - 1) < .002f,
                                    "Animation rotations must remain finite and normalized.");
                                previousTime = key.Time;
                            }
                        }
                    }
                }
                Check(model.Animations.Count(a => a.Name.StartsWith("Rokoko_", StringComparison.Ordinal)) == 15,
                    "Every starter mannequin must retain all fifteen downloaded Rokoko takes.");
                Console.WriteLine($"{name}: {model.Meshes.Sum(m => m.Indices.Length / 3)} triangles, {model.Skeleton.Bones.Count} joints, {model.Animations.Count} animations.");
            }

            string notes = Path.Combine(project.ProjectRoot, "Assets", "Characters", "ByteEngine", "README.txt");
            foreach (string name in BundledCharacterInstaller.SupportFiles)
                Check(File.Exists(Path.Combine(Path.GetDirectoryName(notes)!, name)), "Starter projects must receive animation credits and licenses.");
            if (Environment.GetEnvironmentVariable("BYTEENGINE_MANNEQUIN_EXPORT_DIR") is { Length: > 0 } exportDirectory)
            {
                foreach ((string name, int expected) in new[]
                {
                    ("Mannequin-FullBody-All-Available.glb", 146),
                    ("Mannequin-FPS-Arms-All-Available.glb", 103)
                })
                {
                    string file = Path.Combine(exportDirectory, name);
                    Guid guid = Guid.NewGuid();
                    var source = new AssetRecord(guid, AssetType.Model3D, name, file, file + ".meta",
                        new AssetMetadata { Guid = guid, Type = AssetType.Model3D });
                    ImportedModel imported = ModelImporter.ForPath(file).Import(source, new ModelImporterSettings());
                    Check(imported.Animations.Count == expected, "Combined export must preserve every downloaded animation.");
                    Check(imported.Animations.Count(a => a.Name.StartsWith("Rokoko_", StringComparison.Ordinal)) == 15,
                        "Combined export must include all fifteen Rokoko takes.");
                    Console.WriteLine($"{name}: engine imported {imported.Animations.Count} combined clips.");
                }
            }
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
            Console.WriteLine("Bundled character creation, all animation clips, normalized skin/rotation keys, credits, packaged starter and preservation checks passed.");
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
