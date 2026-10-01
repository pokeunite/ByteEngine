using System.Text.Json.Nodes;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Runtime;
using ByteEngine.Editor.Panels;

namespace ByteEngine.Tests;

internal static class LastStandStarterTests
{
    public static void Prepare(string projectFile, string output)
    {
        string source = Path.GetDirectoryName(Path.GetFullPath(projectFile))!;
        string staging = Path.Combine(Path.GetTempPath(), "ByteEngineStarter-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            foreach (string folder in new[] { "Assets", "Scenes", ".byteengine/ModelAnimations", ".byteengine/ModelSockets" })
            {
                string directory = Path.Combine(source, folder);
                if (!Directory.Exists(directory)) continue;
                foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                {
                    if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked starter assets are unsupported.");
                    string name = Path.GetFileName(file);
                    if (name.StartsWith("698107__victor_natas__the-ambassador-of-filth.wav", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("cliff_rocks_01_height_2k.png", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("cliff_rocks_01_normal_gl_2k.png", StringComparison.OrdinalIgnoreCase) ||
                        file.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
                    string target = GamePackageExporter.ResolveInside(staging, Path.GetRelativePath(source, file));
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(file, target);
                }
            }
            File.Copy(projectFile, Path.Combine(staging, "Game.byteproject"));
            string eventsPath = Path.Combine(staging, "Assets", "LastStandFlow.byteevents");
            var events = JsonNode.Parse(File.ReadAllText(eventsPath))!.AsObject();
            var rules = events["rules"]!.AsArray();
            foreach (var rule in rules.Where(rule => rule?["id"]?.GetValue<string>() == "895b2315-adbe-49a4-bd54-ee5eb82ca61f").ToArray())
                rules.Remove(rule);
            File.WriteAllText(eventsPath, events.ToJsonString(new() { WriteIndented = true }));
            File.WriteAllText(Path.Combine(staging, "ASSET-NOTICE.txt"), """
                LAST STAND — EDUCATIONAL STARTER / THIRD-PARTY ASSET NOTICE

                This project demonstrates ByteEngine gameplay and Event Sheet systems.
                Imported third-party models, textures, sounds and animations belong to
                their original creators. They are not owned by the ByteEngine creator.
                Their inclusion does not grant a new license or transfer ownership.

                Before publishing a game or redistributing any asset, obtain and check
                that asset's original license and supply any required attribution.
                Educational use is the purpose of this starter, not a blanket exception
                to the original license terms. Assets with unverified terms should be
                replaced with your own assets or assets you have permission to use.

                The original music recording has been excluded from this starter.
                Fonts retain their included CC0 license in Assets/Fonts/ByteEngine.
                Engine-created gameplay logic remains subject to ByteEngine's license.
                """);
            File.WriteAllText(Path.Combine(staging, "START-HERE.txt"), """
                LAST STAND
                Open the project, then press Play and click Game View.
                WASD: move. Mouse: look. Left mouse: fire. Escape: release the mouse.
                Survive the waves; collect medkits. The HUD shows health and wave state.
                Review Assets/LastStandFlow.byteevents, Assets/player/fire.byteevents,
                and the player/zombie Blueprints to see how the game is assembled.
                Music is intentionally excluded. Read ASSET-NOTICE.txt before reuse.
                """);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            ByteAssetPackage.Create(staging, output);
            Console.WriteLine($"Last Stand starter: {new FileInfo(output).Length / 1048576.0:F2} MiB packed; music, logs and unused height/OpenGL-normal maps excluded.");
            Run(output);
        }
        finally { Directory.Delete(staging, true); }
    }

    public static void Run(string package)
    {
        string root = Path.Combine(Path.GetTempPath(), "ByteEngineStarterCheck-" + Guid.NewGuid().ToString("N"));
        try
        {
            string project = LastStandStarterFactory.Install(root, "Starter Test", package);
            using (var runtime = new GameProjectRuntime(project))
            {
                var scene = runtime.LoadStartupScene();
                if (runtime.Project.Name != "Starter Test" || scene.FindComponent<WaveSpawner3D>() == null ||
                    scene.FindGameObject("p-TPS") == null || scene.FindGameObject("Canvas") == null)
                    throw new Exception("Last Stand starter must preserve its player, waves and HUD.");
                if (runtime.Database.Assets.Any(asset => asset.ProjectPath.Contains("698107__", StringComparison.Ordinal)))
                    throw new Exception("Excluded music must not ship in the starter.");
                foreach (var renderer in scene.GameObjects.SelectMany(obj => obj.Components).OfType<MeshRenderer>())
                    if (renderer.MeshReference == null && !renderer.UsePrimitive)
                        throw new Exception("Starter has an unassigned non-primitive renderer.");
            }
            if (!File.Exists(Path.Combine(root, "ASSET-NOTICE.txt")) || Directory.Exists(Path.Combine(root, "Logs")))
                throw new Exception("Starter must include asset notice and exclude development logs.");
            foreach (var template in new[] { ProjectTemplate.Starter3D, ProjectTemplate.ByteArena })
            {
                var scene = ProjectTemplateFactory.Create(template);
                if (scene.GameObjects.SelectMany(obj => obj.Components).OfType<MeshRenderer>().Any(renderer => !renderer.UsePrimitive))
                    throw new Exception("Existing starter renderers must explicitly enable primitives.");
            }
            Console.WriteLine("Last Stand starter install, scene load, playable components, asset notice, excluded music and primitive checks passed.");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
