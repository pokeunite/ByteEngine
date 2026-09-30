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
            Directory.Delete(root, true);
        }
        return Task.CompletedTask;
    }
}
