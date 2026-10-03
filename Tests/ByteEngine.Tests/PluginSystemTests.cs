using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Plugins;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
namespace ByteEngine.Tests;
internal static class PluginSystemTests
{
    private sealed class Counter : Component { public int Count { get; set; } }
    private sealed class CounterCodec : IComponentCodec
    {
        public string TypeName => "tests.fixture.Counter";
        public Type ComponentType => typeof(Counter);
        public ComponentData Serialize(Component c, ComponentSerializationContext _) => new() { Type = TypeName, Properties = new() { ["customPayload"] = new JsonObject { ["count"] = ((Counter)c).Count } } };
        public Component Deserialize(ComponentData d, ComponentSerializationContext _) => new Counter { Count = d.Properties["customPayload"]!["count"]!.GetValue<int>() };
    }
    public static void Run(string package)
    {
        string root = Path.Combine(Path.GetTempPath(), "ByteEngine-plugin-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Assets")); Directory.CreateDirectory(Path.Combine(root, "Scenes"));
        void Check(bool good, string label) { if (!good) throw new Exception(label); Console.WriteLine("PASS: " + label); }
        void Reject(Action operation, string label)
        {
            try { operation(); } catch (Exception e) when (e is InvalidDataException or InvalidOperationException) { Check(true, label); return; }
            throw new Exception("Expected rejection: " + label);
        }
        string MakePackage(string name, string manifest, bool traversal = false)
        {
            string path = Path.Combine(root, name + ".byteplugin");
            using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
            using (var writer = new StreamWriter(archive.CreateEntry("plugin.json").Open())) writer.Write(manifest);
            using (var writer = new StreamWriter(archive.CreateEntry("Test.dll").Open())) writer.Write("not executable");
            if (traversal) using (var writer = new StreamWriter(archive.CreateEntry("../escape.txt").Open())) writer.Write("escape");
            return path;
        }
        const string valid = "{\"schemaVersion\":1,\"id\":\"tests.manifest\",\"name\":\"Test\",\"version\":\"1.0.0\",\"assembly\":\"Test.dll\"}";
        try
        {
            Check(ByteEnginePluginPackageManager.ReadManifest(valid).SchemaVersion == 1, "valid manifest");
            Reject(() => ByteEnginePluginPackageManager.ReadManifest(valid.Replace("tests.manifest", "..")), "invalid manifest id");
            Reject(() => ByteEnginePluginPackageManager.Import(root, MakePackage("bad-schema", valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":99"))), "invalid package schema");
            Reject(() => ByteEnginePluginPackageManager.Import(root, MakePackage("traversal", valid, true)), "ZIP traversal rejected");
            Check(!File.Exists(Path.Combine(root, "escape.txt")), "ZIP traversal wrote no outside file");
            using (var empty = ByteEnginePluginManager.LoadProjectPlugins(root, ByteEnginePluginLoadMode.Editor)) Check(empty.LoadedPlugins.Count == 0, "zero plugins is a normal engine session");
            var installed = ByteEnginePluginPackageManager.Import(root, package).Plugin;
            Check(installed.PackagePath != null && File.Exists(installed.PackagePath) && installed.Directory.Contains("PluginCache"), "canonical package and disposable cache");
            Reject(() => ByteEnginePluginPackageManager.Import(root, package), "duplicate plugin id rejected");
            ByteEnginePluginPackageManager.SetEnabled(installed, false);
            using (var disabled = ByteEnginePluginManager.LoadProjectPlugins(root, ByteEnginePluginLoadMode.Editor)) Check(disabled.LoadedPlugins.Count == 0, "disabled plugin is not registered");
            ByteEnginePluginPackageManager.SetEnabled(installed, true);
            using (var loaded = ByteEnginePluginManager.LoadProjectPlugins(root, ByteEnginePluginLoadMode.Editor))
            {
                Check(loaded.LoadedPlugins.Count == 1 && ByteEnginePluginRegistry.Components.Any(p => p.ComponentType.Name == "SpaceBuilder3D"), "component registration");
                var logic = VisualLogicRegistry.CreateDefault();
                Check(logic.TryGetAction("bytebard.spacescraper.setBuildMode", out _) && logic.TryGetCondition("bytebard.spacescraper.buildEnabled", out _), "plugin Event Sheet definitions");
                var argument = logic.GetArguments("bytebard.spacescraper.setBuildMode").Single();
                var authoredDefault = argument.CreateDefault(); authoredDefault.Constant.Boolean = false;
                Check(argument.Name == "enabled" && argument.CreateDefault().Constant.Boolean, "plugin argument metadata and independent authoring defaults");
            }
            var unavailableModule = new EventModuleDefinition { Name = "Unavailable plugin events" };
            unavailableModule.EditorLooseConditions.Add(new() { Id = "unavailable.condition", Arguments = new() { ["number"] = EventValue.Number(73) } });
            unavailableModule.EditorLooseActions.Add(new() { Id = "unavailable.action", Arguments = new() { ["enabled"] = EventValue.Boolean(true) } });
            var eventSerializer = new EventModuleSerializer(); string eventFile = Path.Combine(root, "Assets", "Unavailable.byteevents");
            eventSerializer.Save(unavailableModule, eventFile); var retainedModule = eventSerializer.Load(eventFile); eventSerializer.Save(retainedModule, eventFile);
            Check(retainedModule.EditorLooseConditions.Single().Id == "unavailable.condition" && retainedModule.EditorLooseConditions.Single().Arguments["number"].Constant.Number == 73 && retainedModule.EditorLooseActions.Single().Arguments["enabled"].Constant.Boolean, "unavailable Event Sheet nodes retain IDs and arguments");
            using var database = new AssetDatabase(root, ["Assets", "Scenes"]); using var assets = new AssetManager(database);
            var serializer = new ComponentSerializer(root, database, assets);
            var original = new ComponentData { Type = "tests.fixture.Counter", Enabled = true, Properties = new() { ["customPayload"] = new JsonObject { ["count"] = 73, ["extra"] = new JsonArray(1, "unknown", true) } } };
            var missing = serializer.Deserialize(original)!; var roundtrip = serializer.Serialize(missing)!;
            Check(missing is MissingComponent && !missing.Enabled && roundtrip.Enabled && JsonNode.DeepEquals(original.Properties, roundtrip.Properties), "missing component round-trip preserves complete properties and original enabled state");
            const string id = "tests.fixture"; ByteEnginePluginRegistry.BeginPlugin(id);
            try
            {
                var context = new ByteEnginePluginContext(id); context.RegisterComponent(new CounterCodec(), new("Counter", "Utility"));
                context.RegisterSerializedAlias("LegacyCounter", "tests.fixture.Counter");
                context.RegisterCondition(new() { Id = id + ".positive", Category = "Tests", DisplayName = "Positive", Evaluate = (_, e) => e.Self.GetComponent<Counter>()!.Count > 0 });
                context.RegisterAction(new() { Id = id + ".increment", Category = "Tests", DisplayName = "Increment", Execute = (_, e) => e.Self.GetComponent<Counter>()!.Count++ });
                ByteEnginePluginRegistry.ApplyComponentCodecs(serializer);
                var restored = serializer.Deserialize(roundtrip)!;
                Check(restored is Counter { Count: 73 } && restored.Enabled, "re-enabled codec restores retained component");
                var legacy = new ComponentData { Type = "LegacyCounter", Properties = new() { ["customPayload"] = new JsonObject { ["count"] = 5 } } };
                Check(serializer.Deserialize(legacy) is Counter { Count: 5 }, "legacy serialized alias compatibility");
                Check(serializer.Deserialize(serializer.Serialize(new Counter { Count = 123 })!) is Counter { Count: 123 }, "custom codec serialization");
                var scene = new ByteEngine.Core.Scene.Scene("Plugin Test"); var obj = scene.CreateGameObject("Counter"); obj.AddComponent(new Counter { Count = 1 });
                var execution = new EventExecutionContext { Self = obj, Scene = scene, Globals = new VariableStore() }; var registry = VisualLogicRegistry.CreateDefault();
                registry.TryGetCondition(id + ".positive", out var condition); Check(condition!.Evaluate(new() { Id = condition.Id }, execution), "plugin condition executes");
                registry.TryGetAction(id + ".increment", out var action); action!.Execute(new() { Id = action.Id }, execution); Check(obj.GetComponent<Counter>()!.Count == 2, "plugin action executes");
                var scenes = new SceneSerializer(new ComponentSerializer(root, database, assets));
                var data = new SceneData { Name = "No Plugins", GameObjects = [new() { Name = "Unavailable", Components = [original] }] };
                var noPluginScene = scenes.Deserialize(data); Check(scenes.Serialize(noPluginScene).GameObjects.Single().Components.Single().Type == original.Type, "NoPlugins scene preserves unavailable content");
            }
            finally { ByteEnginePluginRegistry.RemovePlugin(id); }
            var missingDependency = valid.Replace("tests.manifest", "tests.dependency").Replace("\"assembly\":\"Test.dll\"", "\"assembly\":\"Test.dll\",\"dependencies\":[{\"id\":\"missing.required\"}]");
            ByteEnginePluginPackageManager.Import(root, MakePackage("dependency", missingDependency)); var warnings = new List<string>();
            using (var session = ByteEnginePluginManager.LoadProjectPlugins(root, ByteEnginePluginLoadMode.Editor, warnings.Add))
                Check(warnings.Any(w => w.Contains("missing.required")), "missing dependency produces actionable load error");
            var incompatible = valid.Replace("tests.manifest", "tests.incompatible").Replace("\"assembly\":\"Test.dll\"", "\"assembly\":\"Test.dll\",\"minimumEngineVersion\":\"99.0.0\"");
            ByteEnginePluginPackageManager.Import(root, MakePackage("incompatible", incompatible)); warnings.Clear();
            using (var session = ByteEnginePluginManager.LoadProjectPlugins(root, ByteEnginePluginLoadMode.Editor, warnings.Add))
                Check(warnings.Any(w => w.Contains("Requires engine 99.0.0")), "incompatible engine version produces actionable load error");
            Console.WriteLine("PLUGIN ACCEPTANCE TESTS COMPLETE");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}