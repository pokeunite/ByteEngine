using System.Text.Json;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.VisualLogic;

namespace ByteEngine.Mcp.Authoring;

public sealed partial class AuthoringSession
{
    private object ProjectTool(string op, JsonElement? args)
    {
        if (op == "open")
        {
            Open(Request.Required(args, "path"));
            return new { ok = true, name = Project.Name, path = _projectFile, rev = Rev(_projectFile!), startup_scene = Project.StartupScene };
        }
        if (op == "info")
            return new { ok = true, name = Project.Name, path = _projectFile, rev = Rev(_projectFile!), startup_scene = Project.StartupScene,
                asset_directory = Project.AssetDirectory, scene_directory = Project.SceneDirectory };
        if (op == "set_startup_scene")
        {
            CheckRevision(_projectFile!, args);
            string path = Request.Required(args, "scene");
            string full = Inside(path, ".bytescene");
            if (!File.Exists(full)) throw new McpFault("NOT_FOUND", "Scene does not exist.");
            Project.StartupScene = Relative(full);
            _projects.Save(Project, _projectFile!);
            MarkChanged(_projectFile!);
            return new { ok = true, rev = Rev(_projectFile!), startup_scene = Project.StartupScene };
        }
        throw new McpFault("UNSUPPORTED_CAPABILITY");
    }

    private object CatalogTool(string op, JsonElement? args)
    {
        string search = Request.String(args, "search") ?? "";
        int limit = Math.Clamp(Request.Int(args, "limit", 20), 1, 100);
        if (op == "components")
            return new { ok = true, total = Components.RegisteredComponentTypes.Count,
                items = Components.RegisteredComponentTypes.Select(x => x.Name).Distinct()
                    .Where(x => x.Contains(search, StringComparison.OrdinalIgnoreCase)).Order().Take(limit).ToArray() };
        if (op == "component")
        {
            string name = Request.Required(args, "name");
            Type type = Components.RegisteredComponentTypes.FirstOrDefault(t =>
                t.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? throw new McpFault("NOT_FOUND", $"Unknown component '{name}'.");
            var instance = Activator.CreateInstance(type) as Component
                ?? throw new McpFault("UNSUPPORTED_CAPABILITY", "This component has no default authoring instance.");
            var serialized = Components.Serialize(instance)
                ?? throw new McpFault("UNSUPPORTED_CAPABILITY", "This component is not serializable.");
            return new { ok = true, name = type.Name, codec = serialized.Type,
                properties = serialized.Properties };
        }
        if (op == "conditions")
            return new { ok = true, total = LogicRegistry.Conditions.Count,
                items = LogicRegistry.Conditions.Where(x => x.Id.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    x.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => x.Category).ThenBy(x => x.Id).Take(limit)
                    .Select(x => new { x.Id, x.DisplayName, x.Category, x.TargetComponent }).ToArray() };
        if (op == "actions")
            return new { ok = true, total = LogicRegistry.Actions.Count,
                items = LogicRegistry.Actions.Where(x => x.Id.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    x.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => x.Category).ThenBy(x => x.Id).Take(limit)
                    .Select(x => new { x.Id, x.DisplayName, x.Category, x.TargetComponent }).ToArray() };
        if (op == "asset_types")
            return new { ok = true, items = Enum.GetNames<AssetType>() };
        throw new McpFault("UNSUPPORTED_CAPABILITY");
    }

    private object AssetTool(string op, JsonElement? args)
    {
        if (op == "refresh")
        {
            Database.Scan();
            return new { ok = true, count = Database.Assets.Count };
        }
        if (op == "find")
        {
            string search = Request.String(args, "search") ?? "";
            string? type = Request.String(args, "type");
            int limit = Math.Clamp(Request.Int(args, "limit", 20), 1, 100);
            var matches = Database.Assets.Where(a => a.ProjectPath.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                (type == null || a.Type.ToString().Equals(type, StringComparison.OrdinalIgnoreCase))).ToArray();
            return new { ok = true, total = matches.Length, items = matches.Take(limit)
                .Select(a => new { handle = AssetHandle(a), path = a.ProjectPath, type = a.Type.ToString(), guid = a.Guid }).ToArray() };
        }
        if (op == "inspect")
        {
            var a = Asset(Request.Required(args, "asset"));
            return new { ok = true, handle = AssetHandle(a), path = a.ProjectPath, type = a.Type.ToString(),
                guid = a.Guid, rev = Rev(a.FullPath) };
        }
        throw new McpFault("UNSUPPORTED_CAPABILITY", "Asset import is not exposed until its editor importer can be reused safely.");
    }

    private object SceneTool(string op, JsonElement? args)
    {
        if (op == "list")
            return AssetTool("find", JsonSerializer.SerializeToElement(new { search = Request.String(args, "search") ?? "",
                type = AssetType.Scene.ToString(), limit = Request.Int(args, "limit", 20) }));
        if (op == "create")
        {
            string name = Request.Required(args, "name");
            string path = Request.String(args, "path") ?? $"{Project.SceneDirectory}/{name}.bytescene";
            string full = Inside(path, ".bytescene");
            CheckRevision(full, args);
            if (File.Exists(full)) throw new McpFault("ALREADY_EXISTS");
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            Scenes.Save(new Scene(name, Project.Classification), full);
            Database.Scan();
            MarkChanged(full);
            return new { ok = true, path = Relative(full), rev = Rev(full) };
        }
        AssetRecord asset = Asset(Request.Required(args, "scene"), AssetType.Scene);
        if (op == "inspect_object")
        {
            var scene = Scenes.Load(asset.FullPath);
            string token = Request.Required(args, "object");
            GameObject? target = Guid.TryParse(token, out Guid id)
                ? scene.FindGameObject(id) : scene.FindGameObject(token);
            if (target == null) throw new McpFault("NOT_FOUND", $"Object '{token}' was not found.");
            return new { ok = true, rev = Rev(asset.FullPath), id = target.Id, target.Name,
                parent = target.Parent?.Id, target.Active,
                position = new { x = target.Transform.WorldPosition.X, y = target.Transform.WorldPosition.Y,
                    z = target.Transform.WorldPosition.Z },
                scale = new { x = target.Transform.WorldScale.X, y = target.Transform.WorldScale.Y,
                    z = target.Transform.WorldScale.Z },
                components = target.Components.Select(c => Components.Serialize(c)).Where(c => c != null).ToArray() };
        }
        if (op == "attach_module")
        {
            CheckRevision(asset.FullPath, args);
            AssetRecord module = Asset(Request.Required(args, "module"), AssetType.EventModule);
            var scene = Scenes.Load(asset.FullPath);
            string token = Request.Required(args, "object");
            GameObject? target = Guid.TryParse(token, out Guid id)
                ? scene.FindGameObject(id) : scene.FindGameObject(token);
            if (target == null) throw new McpFault("NOT_FOUND", $"Object '{token}' was not found.");
            var events = target.GetComponent<EventModuleComponent>() ?? target.AddComponent(new EventModuleComponent());
            events.AddModuleReference(new AssetReference(module.Guid, module.ProjectPath));
            if (!Request.Bool(args, "dry_run")) { Scenes.Save(scene, asset.FullPath); MarkChanged(asset.FullPath); }
            return new { ok = true, object_id = target.Id, module = module.ProjectPath,
                rev = Rev(asset.FullPath), dry_run = Request.Bool(args, "dry_run") };
        }
        if (op == "place_blueprint")
        {
            CheckRevision(asset.FullPath, args);
            AssetRecord blueprint = Asset(Request.Required(args, "blueprint"), AssetType.Blueprint);
            var scene = Scenes.Load(asset.FullPath);
            var position = new System.Numerics.Vector3(Request.Float(args, "x"), Request.Float(args, "y"), Request.Float(args, "z"));
            string? warning = null;
            GameObject placed = BlueprintRuntimeFactory.Spawn(scene,
                new AssetReference(blueprint.Guid, blueprint.ProjectPath), position,
                Database, Scenes, message => warning = message)
                ?? throw new McpFault("INVALID_REQUEST", warning ?? "Could not place Blueprint.");
            if (!Request.Bool(args, "dry_run")) { Scenes.Save(scene, asset.FullPath); MarkChanged(asset.FullPath); }
            return new { ok = true, object_id = placed.Id, name = placed.Name,
                rev = Rev(asset.FullPath), dry_run = Request.Bool(args, "dry_run") };
        }
        if (op == "inspect")
        {
            var scene = Scenes.Load(asset.FullPath);
            int limit = Math.Clamp(Request.Int(args, "limit", 40), 1, 200);
            return new { ok = true, name = scene.Name, rev = Rev(asset.FullPath),
                objects = scene.GameObjects.Take(limit).Select(o => new {
                    id = o.Id, o.Name, parent = o.Parent?.Id, active = o.Active,
                    components = o.Components.Select(c => c.GetType().Name).ToArray()
                }).ToArray(), total = scene.GameObjectCount };
        }
        if (op == "apply")
        {
            CheckRevision(asset.FullPath, args);
            var scene = Scenes.Load(asset.FullPath);
            var data = Scenes.Serialize(scene);
            int count = ApplyObjectEdits(data.GameObjects, Request.Get(args, "edits"));
            ValidateHierarchy(data.GameObjects);
            if (!Request.Bool(args, "dry_run"))
            {
                var validated = Scenes.Deserialize(data);
                Scenes.Save(validated, asset.FullPath);
                MarkChanged(asset.FullPath);
            }
            return new { ok = true, applied = count, dry_run = Request.Bool(args, "dry_run"), rev = Rev(asset.FullPath) };
        }
        throw new McpFault("UNSUPPORTED_CAPABILITY");
    }

    private object BlueprintTool(string op, JsonElement? args)
    {
        if (op == "list")
            return AssetTool("find", JsonSerializer.SerializeToElement(new { search = Request.String(args, "search") ?? "",
                type = AssetType.Blueprint.ToString(), limit = Request.Int(args, "limit", 20) }));
        if (op == "create")
        {
            string name = Request.Required(args, "name");
            string path = Request.String(args, "path") ?? $"{Project.AssetDirectory}/{name}.byteblueprint";
            string full = Inside(path, ".byteblueprint");
            CheckRevision(full, args);
            if (File.Exists(full)) throw new McpFault("ALREADY_EXISTS");
            var blueprint = new BlueprintDefinition { Name = name, Root = new GameObjectData { Id = Guid.NewGuid(), Name = name } };
            string? kind = Request.String(args, "type");
            if (kind != null && !Enum.TryParse(kind, true, out BlueprintType parsed))
                throw new McpFault("INVALID_REQUEST", "Unknown Blueprint type.");
            if (kind != null) blueprint.Type = Enum.Parse<BlueprintType>(kind, true);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            Blueprints.Save(blueprint, full);
            Database.Scan();
            MarkChanged(full);
            return new { ok = true, path = Relative(full), rev = Rev(full) };
        }
        if (op == "create_playable")
        {
            string name = Request.Required(args, "name");
            string path = Request.String(args, "path") ?? $"{Project.AssetDirectory}/{name}.byteblueprint";
            string full = Inside(path, ".byteblueprint");
            CheckRevision(full, args);
            if (File.Exists(full)) throw new McpFault("ALREADY_EXISTS");
            AssetRecord model = Asset(Request.Required(args, "model"), AssetType.Model3D);
            var hierarchy = PlayablePreset.Build(name, Request.String(args, "view") ?? "tps", model, Assets,
                Project.Classification, Scenes);
            var blueprint = new BlueprintDefinition { Name = name, Type = BlueprintType.Character,
                Root = hierarchy.Root, Children = hierarchy.Children };
            if (!Request.Bool(args, "dry_run"))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                Blueprints.Save(blueprint, full);
                Database.Scan();
                MarkChanged(full);
            }
            return new { ok = true, path = Relative(full), model = model.ProjectPath, view = Request.String(args, "view") ?? "tps",
                objects = 1 + hierarchy.Children.Count, rev = Rev(full), dry_run = Request.Bool(args, "dry_run") };
        }
        AssetRecord asset = Asset(Request.Required(args, "blueprint"), AssetType.Blueprint);
        var bp = Blueprints.Load(asset.FullPath);
        if (op == "inspect")
            return new { ok = true, name = bp.Name, type = bp.Type.ToString(), rev = Rev(asset.FullPath),
                root = new { bp.Root.Id, bp.Root.Name }, children = bp.Children.Select(x => new { x.Id, x.Name, x.ParentId }).ToArray(),
                event_modules = bp.EventModules };
        if (op == "attach_module")
        {
            CheckRevision(asset.FullPath, args);
            AssetRecord module = Asset(Request.Required(args, "module"), AssetType.EventModule);
            if (!bp.EventModules.Contains(module.Guid)) bp.EventModules.Add(module.Guid);
            if (!Request.Bool(args, "dry_run")) { Blueprints.Save(bp, asset.FullPath); MarkChanged(asset.FullPath); }
            return new { ok = true, module = module.ProjectPath, rev = Rev(asset.FullPath),
                dry_run = Request.Bool(args, "dry_run") };
        }
        if (op == "apply")
        {
            CheckRevision(asset.FullPath, args);
            var objects = new List<GameObjectData> { bp.Root };
            objects.AddRange(bp.Children);
            int count = ApplyObjectEdits(objects, Request.Get(args, "edits"));
            if (objects.Count == 0) throw new McpFault("INVALID_REQUEST", "Cannot remove Blueprint root.");
            ValidateHierarchy(objects);
            bp.Root = objects[0];
            bp.Children = objects.Skip(1).ToList();
            if (!Request.Bool(args, "dry_run"))
            {
                Blueprints.Save(bp, asset.FullPath);
                MarkChanged(asset.FullPath);
            }
            return new { ok = true, applied = count, dry_run = Request.Bool(args, "dry_run"), rev = Rev(asset.FullPath) };
        }
        throw new McpFault("UNSUPPORTED_CAPABILITY");
    }

    private object LogicTool(string op, JsonElement? args)
    {
        if (op == "list")
            return AssetTool("find", JsonSerializer.SerializeToElement(new { search = Request.String(args, "search") ?? "",
                type = AssetType.EventModule.ToString(), limit = Request.Int(args, "limit", 20) }));
        if (op == "create")
        {
            string name = Request.Required(args, "name");
            string path = Request.String(args, "path") ?? $"{Project.AssetDirectory}/{name}.byteevents";
            string full = Inside(path, ".byteevents");
            CheckRevision(full, args);
            if (File.Exists(full)) throw new McpFault("ALREADY_EXISTS");
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            Modules.Save(new EventModuleDefinition { Name = name }, full);
            Database.Scan();
            MarkChanged(full);
            return new { ok = true, path = Relative(full), rev = Rev(full) };
        }
        AssetRecord asset = Asset(Request.Required(args, "module"), AssetType.EventModule);
        var module = Modules.Load(asset.FullPath);
        if (op == "inspect_rule")
        {
            string token = Request.Required(args, "rule");
            EventRuleDefinition? rule = Guid.TryParse(token, out Guid id)
                ? module.Rules.FirstOrDefault(r => r.Id == id)
                : module.Rules.FirstOrDefault(r => r.DisplayName.Equals(token, StringComparison.OrdinalIgnoreCase));
            if (rule == null) throw new McpFault("NOT_FOUND", $"Rule '{token}' was not found.");
            return new { ok = true, rev = Rev(asset.FullPath), rule };
        }
        if (op == "inspect")
            return new { ok = true, name = module.Name, rev = Rev(asset.FullPath),
                rules = module.Rules.Select(r => new { r.Id, r.DisplayName, r.Enabled,
                    conditions = r.Conditions.Select(i => i.Id).ToArray(),
                    actions = r.Actions.Select(i => i.Id).ToArray() }).ToArray() };
        if (op == "add_rule")
        {
            CheckRevision(asset.FullPath, args);
            string name = Request.Required(args, "name");
            if (module.Rules.Any(r => r.DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase)))
                throw new McpFault("ALREADY_EXISTS", "Rule names must be unique in a module.");
            var rule = new EventRuleDefinition { DisplayName = name, EditorTitle = name };
            rule.Conditions = BuildInstructions(Request.Get(args, "conditions"), true);
            rule.Actions = BuildInstructions(Request.Get(args, "actions"), false);
            if (rule.Conditions.Count == 0) throw new McpFault("INVALID_REQUEST", "Provide at least one condition.");
            if (rule.Actions.Count == 0) throw new McpFault("INVALID_REQUEST", "Provide at least one action.");
            module.Rules.Add(rule);
            if (!Request.Bool(args, "dry_run")) { Modules.Save(module, asset.FullPath); MarkChanged(asset.FullPath); }
            return new { ok = true, rule_id = rule.Id, conditions = rule.Conditions.Count,
                actions = rule.Actions.Count, dry_run = Request.Bool(args, "dry_run"), rev = Rev(asset.FullPath) };
        }
        throw new McpFault("UNSUPPORTED_CAPABILITY");
    }

    private List<VisualInstruction> BuildInstructions(JsonElement entries, bool condition)
    {
        var result = new List<VisualInstruction>();
        foreach (var item in Request.Items(entries))
        {
            string id = Request.Required(item, "id");
            bool known = condition ? LogicRegistry.TryGetCondition(id, out _) : LogicRegistry.TryGetAction(id, out _);
            if (!known) throw new McpFault("INVALID_NODE", $"Unknown {(condition ? "condition" : "action")} '{id}'.");
            var instruction = new VisualInstruction { Id = id };
            foreach (var pair in Request.Properties(Request.Get(item, "args")))
                instruction.Arguments[pair.Key] = pair.Value.ValueKind switch
                {
                    JsonValueKind.String => EventValue.String(pair.Value.GetString()!),
                    JsonValueKind.True => EventValue.Boolean(true),
                    JsonValueKind.False => EventValue.Boolean(false),
                    JsonValueKind.Number => EventValue.Number(pair.Value.GetDouble()),
                    JsonValueKind.Object => ReferenceValue(pair.Value),
                    _ => throw new McpFault("INVALID_REQUEST", $"Argument '{pair.Key}' needs a constant or reference.")
                };
            result.Add(instruction);
        }
        return result;
    }

    private static EventValue ReferenceValue(JsonElement value)
    {
        JsonElement data = Request.Get(value, "reference");
        if (data.ValueKind != JsonValueKind.Object)
            throw new McpFault("INVALID_REQUEST", "Reference arguments need a 'reference' object.");
        if (!Enum.TryParse(Request.Required(data, "scope"), true, out ByteEngine.Core.Variables.VariableScope scope))
            throw new McpFault("INVALID_REQUEST", "Unknown reference scope.");
        string? objectId = Request.String(data, "object_id");
        if (objectId != null && !Guid.TryParse(objectId, out _))
            throw new McpFault("INVALID_REQUEST", "Reference object_id must be a GUID.");
        return EventValue.FromReference(new ByteEngine.Core.Variables.VariableReference
        {
            Scope = scope,
            ObjectId = objectId == null ? null : Guid.Parse(objectId),
            ObjectName = Request.String(data, "object_name"),
            ComponentType = Request.String(data, "component_type"),
            MemberName = Request.Required(data, "member_name")
        });
    }

    private object RuntimeTool(string op, JsonElement? args)
    {
        if (op != "validate") throw new McpFault("UNSUPPORTED_CAPABILITY", "Build and Play are not enabled in V1.");
        string full = Inside(Project.StartupScene, ".bytescene");
        if (!File.Exists(full)) return new { ok = false, code = "MISSING_STARTUP_SCENE", path = Project.StartupScene };
        var scene = Scenes.Load(full);
        return new { ok = true, startup_scene = Project.StartupScene, objects = scene.GameObjectCount };
    }

    private int ApplyObjectEdits(List<GameObjectData> objects, JsonElement edits)
    {
        var temporary = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        int count = 0;
        foreach (JsonElement edit in Request.Items(edits))
        {
            string op = Request.Required(edit, "op");
            string? target = Request.String(edit, "target");
            Guid FindId(string key) => key == "root" && objects.Count > 0 ? objects[0].Id :
                temporary.TryGetValue(key, out Guid temp) ? temp :
                Guid.TryParse(key, out Guid parsed) ? parsed :
                throw new McpFault("NOT_FOUND", $"Unknown object '{key}'.");
            GameObjectData Find() => objects.FirstOrDefault(o => o.Id == FindId(target ?? ""))
                ?? throw new McpFault("NOT_FOUND", $"Unknown object '{target}'.");
            if (op == "create_object")
            {
                string name = Request.Required(edit, "name");
                var obj = new GameObjectData { Id = Guid.NewGuid(), Name = name };
                string? parent = Request.String(edit, "parent");
                if (parent != null) obj.ParentId = FindId(parent);
                objects.Add(obj);
                string? alias = Request.String(edit, "as");
                if (alias != null) temporary[alias] = obj.Id;
            }
            else if (op == "rename") Find().Name = Request.Required(edit, "name");
            else if (op == "set_active") Find().Active = Request.Bool(edit, "value");
            else if (op == "set_layer")
            {
                int layer = Request.Int(edit, "layer", -1);
                if (layer is < 0 or >= 32) throw new McpFault("INVALID_REQUEST", "Layer must be 0..31.");
                Find().Layer = layer;
            }
            else if (op == "set_parent")
            {
                var obj = Find();
                string? parent = Request.String(edit, "parent");
                obj.ParentId = parent == null ? null : FindId(parent);
                if (obj.ParentId == obj.Id) throw new McpFault("INVALID_REQUEST", "An object cannot parent itself.");
            }
            else if (op == "delete_object")
            {
                var obj = Find();
                if (obj == objects[0] && objects.Count > 1)
                    throw new McpFault("INVALID_REQUEST", "Cannot remove root object while children remain.");
                var pending = new HashSet<Guid> { obj.Id };
                bool changed;
                do { changed = false; foreach (var child in objects.Where(o => o.ParentId is Guid p && pending.Contains(p)))
                    changed |= pending.Add(child.Id); } while (changed);
                objects.RemoveAll(o => pending.Contains(o.Id));
            }
            else if (op == "set_component")
            {
                var obj = Find();
                string type = Request.Required(edit, "type");
                if (!Components.RegisteredComponentTypes.Any(t => t.Name.Equals(type, StringComparison.OrdinalIgnoreCase)))
                    throw new McpFault("INVALID_COMPONENT", $"Unknown component '{type}'.");
                var component = obj.Components.FirstOrDefault(c => c.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
                if (component == null) { component = new ComponentData { Type = type }; obj.Components.Add(component); }
                foreach (var pair in Request.Properties(Request.Get(edit, "properties")))
                    component.Properties[pair.Key] = System.Text.Json.Nodes.JsonNode.Parse(pair.Value.GetRawText());
            }
            else if (op == "remove_component")
            {
                var obj = Find(); string type = Request.Required(edit, "type");
                obj.Components.RemoveAll(c => c.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
            }
            else if (op == "set_position" || op == "set_scale")
            {
                var obj = Find();
                var vec = new Vector3Data { X = Request.Float(edit, "x"), Y = Request.Float(edit, "y"),
                    Z = Request.Float(edit, "z", op == "set_scale" ? 1 : 0) };
                if (op == "set_position") obj.Transform.LocalPosition = vec;
                else obj.Transform.LocalScale = vec;
            }
            else throw new McpFault("UNSUPPORTED_CAPABILITY", $"Unknown edit '{op}'.");
            count++;
        }
        return count;
    }

    private static void ValidateHierarchy(List<GameObjectData> objects)
    {
        var byId = objects.ToDictionary(x => x.Id);
        foreach (var obj in objects)
        {
            var visited = new HashSet<Guid> { obj.Id };
            Guid? parent = obj.ParentId;
            while (parent is Guid id)
            {
                if (!byId.TryGetValue(id, out var node))
                    throw new McpFault("INVALID_REQUEST", $"Missing parent for '{obj.Name}'.");
                if (!visited.Add(id))
                    throw new McpFault("INVALID_REQUEST", $"Parent cycle involving '{obj.Name}'.");
                parent = node.ParentId;
            }
        }
    }
}
