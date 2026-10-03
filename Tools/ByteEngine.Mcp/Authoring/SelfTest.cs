using System.Text.Json;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;

namespace ByteEngine.Mcp.Authoring;

internal static class SelfTest
{
    public static Task RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "byteengine-mcp-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string file = Path.Combine(root, "Test.byteproject");
            new ProjectSerializer().Save(new ProjectData { Name = "MCP Test" }, file);
            Directory.CreateDirectory(Path.Combine(root, "Assets"));
            File.WriteAllText(Path.Combine(root, "Assets", "Test.obj"),
                "o Test\nv 0 0 0\nv 1 0 0\nv 0 1 0\nvt 0 0\nvt 1 0\nvt 0 1\nvn 0 0 1\nf 1/1/1 2/2/1 3/3/1\n");
            using var session = new AuthoringSession();
            JsonElement Call(string domain, string op, object? input = null)
            {
                string result = session.Dispatch(domain, op, input == null ? null : JsonSerializer.SerializeToElement(input));
                var json = JsonDocument.Parse(result).RootElement.Clone();
                if (json.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.False)
                    throw new InvalidOperationException($"{domain}.{op}: {result}");
                return json;
            }
            Call("project", "open", new { path = file });
            string? pluginPackage = Environment.GetEnvironmentVariable("BYTEENGINE_TEST_PLUGIN");
            if (!string.IsNullOrWhiteSpace(pluginPackage))
            {
                Call("plugin", "import", new { path = pluginPackage });
                Call("project", "open", new { path = file });
                var cap = Call("catalog", "component", new { name = "SpaceBuilder3D" });
                if (cap.GetProperty("codec").GetString() != "bytebard.spacescraper.SpaceBuilder3D") throw new Exception("Plugin codec missing.");
                if (Call("catalog", "actions", new { search = "bytebard.spacescraper" }).GetProperty("items").GetArrayLength() == 0) throw new Exception("Plugin action missing.");
                var testScene = Call("scene", "create", new { name = "PluginScene", expected_rev = "new" });
                string testPath = testScene.GetProperty("path").GetString()!;
                Call("scene", "apply", new { scene = testPath, expected_rev = testScene.GetProperty("rev").GetString(), edits = new object[] {
                    new { op = "create_object", name = "Builder", @as = "$builder" },
                    new { op = "set_component", target = "$builder", type = "SpaceBuilder3D", properties = new { buildRadius = 37f, maxParts = 321, startInBuildMode = false } }
                } });
                var inspection = Call("scene", "inspect", new { scene = testPath });
                string builderId = inspection.GetProperty("objects").EnumerateArray().Single(o => o.GetProperty("name").GetString() == "Builder").GetProperty("id").GetString()!;
                Call("scene", "apply", new { scene = testPath, expected_rev = inspection.GetProperty("rev").GetString(), edits = new object[] {
                    new { op = "set_component", target = builderId, type = "bytebard.spacescraper.SpaceBuilder3D", properties = new { buildRadius = 37f } },
                    new { op = "remove_component", target = builderId, type = "SpaceBuilder3D" },
                    new { op = "set_component", target = builderId, type = "SpaceBuilder3D", properties = new { buildRadius = 37f, maxParts = 321, startInBuildMode = false } }
                } });
                Call("project", "open", new { path = file });
                void CheckPluginData(bool missing)
                {
                    var loaded = session.Scenes.Load(Path.Combine(root, testPath));
                    var obj = loaded.FindGameObject("Builder")!;
                    var comp = obj.Components.Single();
                    if ((comp is MissingComponent) != missing) throw new Exception("Plugin availability mismatch.");
                    var saved = session.Components.Serialize(comp)!;
                    if (saved.Type != "bytebard.spacescraper.SpaceBuilder3D" || saved.Properties["buildRadius"]!.GetValue<float>() != 37f || saved.Properties["maxParts"]!.GetValue<int>() != 321)
                        throw new Exception("Plugin state lost during save/reload.");
                    session.Scenes.Save(loaded, Path.Combine(root, testPath));
                }
                CheckPluginData(false);
                Call("plugin", "disable", new { id = "bytebard.spacescraper" });
                Call("project", "open", new { path = file }); CheckPluginData(true);
                Call("plugin", "enable", new { id = "bytebard.spacescraper" });
                Call("project", "open", new { path = file }); CheckPluginData(false);
                Console.WriteLine("Plugin import -> placement -> save/reload -> disabled preservation -> restored: PASS");
            }
            string? goblinPackage=Environment.GetEnvironmentVariable("BYTEENGINE_TEST_GOBLIN_PLUGIN");
            string? goblinCatalog=Environment.GetEnvironmentVariable("BYTEENGINE_TEST_GOBLIN_CATALOG");
            if(goblinPackage!=null && goblinCatalog!=null)
            {
                Call("plugin","import",new{path=goblinPackage}); Call("project","open",new{path=file});
                var assembly=ByteEngine.Core.Plugins.ByteEnginePluginRegistry.Components.First(p=>p.PluginId=="bytebard.goblinscrapper").ComponentType.Assembly;
                dynamic catalog=assembly.GetType("GoblinScrapper.Construction.VehiclePartCatalog")!.GetMethod("Load")!.Invoke(null,[goblinCatalog])!;
                dynamic machine=Activator.CreateInstance(assembly.GetType("GoblinScrapper.Construction.VehicleAssembly")!,catalog,"goblin_master_block")!;
                int wheel=machine.AddAtSocket("goblin_wheel",0,"Right",(string)catalog.Parts["goblin_wheel"].Sockets[0].Name,0);
                if(wheel<1)throw new Exception("Cold plugin wheel placement failed.");
                using(dynamic physics=Activator.CreateInstance(assembly.GetType("GoblinScrapper.Construction.ContraptionPhysicsWorld")!,machine,new System.Numerics.Vector3(0,1,0),System.Numerics.Quaternion.Identity)!)
                {
                    for(int tick=0;tick<120;tick++)physics.Step(1f/60,1f,0f,false,false,false);
                    var pose=((System.Numerics.Vector3 Position,System.Numerics.Quaternion Rotation))physics.Pose(0);
                    if(!float.IsFinite(pose.Position.X))throw new Exception("Cold plugin physics was invalid.");
                }
                var bepu=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="BepuPhysics");
                if(System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(bepu)!=System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(assembly))
                    throw new Exception("Physics dependency escaped its private plugin context.");
                Console.WriteLine("Goblin plugin cold-load physics with bundled Bepu and no host plugin reference: PASS");
            }
            Call("catalog", "components", new { search = "Camera" });
            var created = Call("scene", "create", new { name = "Main", expected_rev = "new" });
            string path = created.GetProperty("path").GetString()!;
            string rev = created.GetProperty("rev").GetString()!;
            Call("scene", "apply", new { scene = path, expected_rev = rev, edits = new object[] {
                new { op = "create_object", name = "Player", @as = "$player" },
                new { op = "create_object", name = "Muzzle", parent = "$player" },
                new { op = "set_position", target = "$player", x = 1, y = 2, z = 3 }
            } });
            var scene = Call("scene", "inspect", new { scene = path });
            if (scene.GetProperty("total").GetInt32() != 2) throw new InvalidOperationException("Scene persistence failed.");
            Call("blueprint", "create", new { name = "Enemy", expected_rev = "new" });
            var player = Call("blueprint", "create_playable", new {
                name = "Player", model = "Assets/Test.obj", view = "tps", expected_rev = "new" });
            foreach (string view in new[] { "fps", "topdown", "isometric" })
            {
                var variant = Call("blueprint", "create_playable", new {
                    name = "Player_" + view, model = "Assets/Test.obj", view, expected_rev = "new" });
                if (Call("blueprint", "inspect", new { blueprint = variant.GetProperty("path").GetString() })
                    .GetProperty("children").GetArrayLength() < 2)
                    throw new InvalidOperationException($"Incomplete {view} player hierarchy.");
            }
            Call("scene", "place_blueprint", new { scene = path,
                expected_rev = Call("scene", "inspect", new { scene = path }).GetProperty("rev").GetString(),
                blueprint = player.GetProperty("path").GetString(), x = 0, y = 0, z = 0 });
            if (Call("scene", "inspect", new { scene = path }).GetProperty("total").GetInt32() <= 2)
                throw new InvalidOperationException("Playable Blueprint placement failed.");
            var logic = Call("logic", "create", new { name = "Wave", expected_rev = "new" });
            string logicPath = logic.GetProperty("path").GetString()!;
            Call("logic", "add_rule", new { module = logicPath,
                expected_rev = logic.GetProperty("rev").GetString(), name = "Begin",
                conditions = new[] { new { id = "system.always" } },
                actions = new[] { new { id = "time.startTimer" } } });
            if (Call("logic", "inspect", new { module = logicPath }).GetProperty("rules").GetArrayLength() != 1)
                throw new InvalidOperationException("Event rule persistence failed.");
            var referenceRule = Call("logic", "add_rule", new { module = logicPath,
                expected_rev = Call("logic", "inspect", new { module = logicPath }).GetProperty("rev").GetString(),
                name = "Wave label", conditions = new[] { new { id = "system.always" } },
                actions = new object[] { new { id = "ui.setText", args = new {
                    target = "Self", text = new { reference = new { scope = "Scene", member_name = "Wave" } }
                } } } });
            var inspectedRule = Call("logic", "inspect_rule", new { module = logicPath, rule = "Wave label" });
            if (inspectedRule.GetProperty("rule").GetProperty("actions")[0].GetProperty("arguments")
                .GetProperty("text").GetProperty("kind").GetInt32() != 1)
                throw new InvalidOperationException("MCP reference argument was not preserved.");
            var sceneBeforeAttach = Call("scene", "inspect", new { scene = path });
            string scenePlayerId = sceneBeforeAttach.GetProperty("objects").EnumerateArray()
                .First(o => o.GetProperty("name").GetString() == "Player").GetProperty("id").GetString()!;
            Call("scene", "attach_module", new { scene = path, object_id = scenePlayerId,
                @object = scenePlayerId, module = logicPath, expected_rev = sceneBeforeAttach.GetProperty("rev").GetString() });
            if (!Call("scene", "inspect_object", new { scene = path, @object = scenePlayerId })
                .GetProperty("components").EnumerateArray()
                .Any(c => c.GetProperty("type").GetString() == "EventModuleComponent"))
                throw new InvalidOperationException("Scene Event Module attachment failed.");
            Call("blueprint", "attach_module", new { blueprint = player.GetProperty("path").GetString(),
                expected_rev = Call("blueprint", "inspect", new { blueprint = player.GetProperty("path").GetString() })
                    .GetProperty("rev").GetString(), module = logicPath });
            if (Call("blueprint", "inspect", new { blueprint = player.GetProperty("path").GetString() })
                    .GetProperty("event_modules").GetArrayLength() != 1)
                throw new InvalidOperationException("Event Module attachment failed.");
            Call("runtime", "validate");
            Console.WriteLine("MCP self-test passed: model-to-player Blueprint, scene placement, Event Module attachment, persistence, runtime validation.");
        }
        finally
        {
            try { Directory.Delete(root, true); }
            catch (UnauthorizedAccessException) { Console.WriteLine("Plugin cache cleanup deferred until process exit (Windows assembly mapping)."); }
            catch (IOException) { Console.WriteLine("Plugin cache cleanup deferred until process exit."); }
        }
        return Task.CompletedTask;
    }
}
