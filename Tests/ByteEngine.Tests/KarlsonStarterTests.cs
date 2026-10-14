using ByteEngine.Core.Runtime;
using ByteEngine.Core.Serialization;
using ByteEngine.Editor.Panels;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.Common;

namespace ByteEngine.Tests;

internal static class KarlsonStarterTests
{
    public static void Prepare(string projectFile, string output)
    {
        string source = Path.GetDirectoryName(Path.GetFullPath(projectFile))!;
        string staging = Path.Combine(Path.GetTempPath(), "ByteEngineKarlsonStarter-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            string authored = string.Join("\n", new[] { "Assets", "Scenes" }.SelectMany(folder =>
                GamePackageExporter.WalkFiles(Path.Combine(source, folder)))
                .Where(file => Path.GetExtension(file) is ".bytescene" or ".byteblueprint" or ".byteanim" or ".byteevents")
                .Select(File.ReadAllText));
            var unusedVariants = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "MilkDepot_Assembled.glb", "Floor_4m.glb", "Stair_1m.glb", "Wall_4m.glb" };
            foreach (string folder in new[] { "Assets", "Scenes", "Plugins", "PluginLibrary",
                ".byteengine/ModelAnimations", ".byteengine/ModelSockets" })
            {
                string directory = Path.Combine(source, folder);
                if (!Directory.Exists(directory)) continue;
                foreach (string file in GamePackageExporter.WalkFiles(directory))
                {
                    string relative = Path.GetRelativePath(source, file).Replace('\\', '/');
                    if (relative.StartsWith("Assets/Concepts/", StringComparison.OrdinalIgnoreCase)) continue;
                    string model = file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ? file[..^5] : file;
                    if (unusedVariants.Contains(Path.GetFileName(model)))
                    {
                        string guid = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(model + ".meta"))!["guid"]!.GetValue<string>();
                        // If a later scene/blueprint starts using a spare variant, retain it automatically.
                        if (!authored.Contains(guid, StringComparison.OrdinalIgnoreCase) &&
                            !authored.Contains(Path.GetFileName(model), StringComparison.OrdinalIgnoreCase)) continue;
                    }
                    if (file.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) ||
                        file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
                    string target = GamePackageExporter.ResolveInside(staging, Path.GetRelativePath(source, file));
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(file, target);
                }
            }
            File.Copy(projectFile, Path.Combine(staging, "Game.byteproject"));
            foreach (string name in new[] { "WEAPON SETTINGS.txt", "MILK DEPOT CONTROLS.txt" })
                if (File.Exists(Path.Combine(source, name))) File.Copy(Path.Combine(source, name), Path.Combine(staging, name));
            // Keep the editable reference source current with the canonical engine plugin.
            string canonical = Path.GetFullPath("Plugins/KarlsonDemo");
            string pluginSource = Path.Combine(staging, "PluginSource", "KarlsonDemo");
            Directory.CreateDirectory(pluginSource);
            foreach (string file in Directory.EnumerateFiles(canonical))
                if (Path.GetExtension(file) is ".cs" or ".json") File.Copy(file, Path.Combine(pluginSource, Path.GetFileName(file)));
            File.WriteAllText(Path.Combine(staging, "START-HERE.txt"), """
                KARLSON DEMO - BYTEENGINE TEST TEMPLATE

                Open your .byteproject in ByteEngine, press Play and click Game View.
                WASD: move. Mouse: look. Space: jump. Left mouse: fire. R: reload.
                1/2/3/4: select weapons. Escape: release mouse.
                Shoot all eight practice targets, then collect the milk at the exit.
                Yellow pickups supply ammo. Cyan beacons set your recovery checkpoint.
                Targets are stationary practice dummies. Falling returns you to a checkpoint.

                Milk Depot is the startup scene. Scenes/Main.bytescene is the gun lab.
                Weapon settings are editable on the view models under FPS Character.
                Blueprints, animation profiles, events and VFX are in Assets.
                The required Modular Weapon Gameplay plugin is included in Plugins.
                PluginSource/KarlsonDemo contains a reference copy of its C# source.
                Build plugin changes in the engine repository's Plugins/KarlsonDemo project.

                TESTING
                Use the same window size and graphics settings when comparing machines.
                Open Window > Performance and report FPS/frame time while moving and turning.
                Include CPU, GPU, RAM, resolution, graphics settings and engine version.
                Test jumping, weapon switching, fire/reload, ammo pickups, checkpoints,
                all eight targets and collecting the milk. Include a screenshot or diagnostics
                with any bug report, plus steps to reproduce it.

                This is a ByteEngine demo inspired by Karlson, not the original game.
                Each new template project has its own project identity and save storage.
                """);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            ByteAssetPackage.Create(staging, output);
            Console.WriteLine($"Karlson Demo template: {new FileInfo(output).Length / 1048576.0:F2} MiB.");
            Verify(output);
        }
        finally
        {
            if (!Path.GetFullPath(staging).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
                throw new IOException("Unsafe template staging cleanup path.");
            Directory.Delete(staging, true);
        }
    }

    private static void Verify(string package)
    {
        string name = "Karlson Demo Template Check " + Guid.NewGuid().ToString("N")[..8];
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), name);
        string projectFile = KarlsonStarterFactory.Install(root, name, package);
        var project = new ProjectSerializer().Load(projectFile);
        if (project.Name != name || project.ProjectId == Guid.Empty || project.RuntimeSaveId != null)
            throw new Exception("Template project identity was not reset.");
        // Embedded model materials need a native GL context during scene deserialization.
        using var window = new GameWindow(GameWindowSettings.Default, new NativeWindowSettings
        {
            StartVisible = false, ClientSize = new(64,64), APIVersion = new(3,3), Profile = ContextProfile.Core
        });
        window.Context.MakeCurrent();
        using (var runtime = new GameProjectRuntime(projectFile))
        {
            var scene = runtime.LoadStartupScene();
            var components = scene.GameObjects.SelectMany(obj => obj.Components).ToArray();
            if (!scene.Name.StartsWith("Milk Depot") || scene.FindGameObject("FPS Character") == null ||
                components.Count(c => c.GetType().Name == "WeaponAmmoPickup3D") != 6 ||
                !components.Any(c => c.GetType().Name == "MilkDepotCourse3D") ||
                components.Count(c => c.GetType().Name == "WeaponInstance3D") < 3)
                throw new Exception("Template lost its FPS player, weapon plugin, pickups or course.");
            if (runtime.Database.Assets.Count == 0) throw new Exception("Template assets were not indexed.");
        }
        if (Directory.Exists(Path.Combine(root, "Logs")) || Directory.Exists(Path.Combine(root, ".byteengine", "Backups")) ||
            !File.Exists(Path.Combine(root, "START-HERE.txt")) || !File.Exists(Path.Combine(root, "Scenes", "Main.bytescene")))
            throw new Exception("Template includes development data or omits instructions/gun lab.");
        try { KarlsonStarterFactory.Install(root, "Overwrite", package); throw new Exception("Nonempty folder was overwritten."); }
        catch (IOException) { }
        Console.WriteLine("PASS Karlson template: portable install, fresh identity, packaged plugin, startup scene, FPS weapons/pickups, clean contents and overwrite protection.");
        Console.WriteLine("TEMPLATE CHECK PROJECT: " + projectFile);
    }
}
