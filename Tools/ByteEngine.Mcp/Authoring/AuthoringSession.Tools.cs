using System.Numerics;
using System.Text.Json;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;

namespace ByteEngine.Mcp.Authoring;

public sealed partial class AuthoringSession
{
    private object ProjectTool(string op, JsonElement? args)
    {
        if (op == "templates") return TemplatesTool();
        if (op == "create") return CreateProjectTool(args);
        if (op == "open")
        {
            Open(Request.Required(args, "path"));
            return new { ok = true, name = Project.Name, path = _projectFile, rev = Rev(_projectFile!), startup_scene = Project.StartupScene };
        }
        if (op == "info")
            return new
            {
                ok = true,
                name = Project.Name,
                path = _projectFile,
                rev = Rev(_projectFile!),
                startup_scene = Project.StartupScene,
                asset_directory = Project.AssetDirectory,
                scene_directory = Project.SceneDirectory,
                assets = Database.Assets.Count,
                scenes = Database.Assets.Count(a => a.Type == AssetType.Scene),
                blueprints = Database.Assets.Count(a => a.Type == AssetType.Blueprint),
                event_modules = Database.Assets.Count(a => a.Type == AssetType.EventModule),
                plugins = _plugins?.LoadedPlugins.Count ?? 0,
                engine_version = Project.EngineVersion
            };
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
        if (op == "tags")
            return new { ok = true, items = Project.Classification.Tags.Select(tag => new { tag.Id, tag.Name }).ToArray() };
        if (op == "layers")
            return new { ok = true, items = Project.Classification.Layers.Select(layer => new { layer.Index, layer.Name }).ToArray() };
        if (op == "add_tag")
        {
            CheckRevision(_projectFile!, args);
            string name = Request.Required(args, "name").Trim();
            if (name.Length == 0) throw new McpFault("INVALID_REQUEST", "Tag name cannot be empty.");
            var existing = Project.Classification.FindTag(name);
            if (existing != null) return new { ok = true, tag = new { existing.Id, existing.Name }, rev = Rev(_projectFile!), created = false };
            var tag = Project.Classification.AddTag(name)
                ?? throw new McpFault("INVALID_REQUEST", "Tag could not be added.");
            _projects.Save(Project, _projectFile!);
            MarkChanged(_projectFile!);
            return new { ok = true, tag = new { tag.Id, tag.Name }, rev = Rev(_projectFile!), created = true };
        }
        if (op == "define_layer")
        {
            CheckRevision(_projectFile!, args);
            int index = Request.Int(args, "index", -1);
            string name = Request.Required(args, "name").Trim();
            if (!Project.Classification.DefineLayer(index, name))
                throw new McpFault("INVALID_REQUEST", "Layer index must be 0..31 and the name must be unique.");
            _projects.Save(Project, _projectFile!);
            MarkChanged(_projectFile!);
            return new { ok = true, layer = new { index, name }, rev = Rev(_projectFile!) };
        }
        throw new McpFault("UNSUPPORTED_CAPABILITY");
    }

    private Type? FindComponentType(string name)
    {
        string? canonical = Components.GetCanonicalTypeName(name);
        var matches = Components.RegisteredComponentTypes.Where(t =>
            t.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.FullName, name, StringComparison.OrdinalIgnoreCase) ||
            canonical != null && Components.GetSerializedTypeName(t) == canonical).ToArray();
        if (matches.Length > 1) throw new McpFault("INVALID_COMPONENT", "Ambiguous component name; use its canonical codec name.");
        return matches.SingleOrDefault();
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
            Type type = FindComponentType(name)
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
                    .Select(x => new { x.Id, x.DisplayName, x.Category, x.TargetComponent, arguments = x.Arguments.Select(a => new { a.Name, a.DisplayName, type = a.DefaultValue.Type.ToString(), default_value = a.DefaultValue.BoxedValue }).ToArray() }).ToArray() };
        if (op == "actions")
            return new { ok = true, total = LogicRegistry.Actions.Count,
                items = LogicRegistry.Actions.Where(x => x.Id.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    x.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => x.Category).ThenBy(x => x.Id).Take(limit)
                    .Select(x => new { x.Id, x.DisplayName, x.Category, x.TargetComponent, arguments = x.Arguments.Select(a => new { a.Name, a.DisplayName, type = a.DefaultValue.Type.ToString(), default_value = a.DefaultValue.BoxedValue }).ToArray() }).ToArray() };
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
                guid = a.Guid, rev = Rev(a.FullPath), detail = AssetDetail(a, args) };
        }
        if (op == "import") return ImportAssetTool(args, folder: false);
        if (op == "import_folder") return ImportAssetTool(args, folder: true);
        if (op == "set_import_settings") return SetImportSettingsTool(args);
        if (op == "reimport") return ReimportAssetTool(args);
        throw new McpFault("UNSUPPORTED_CAPABILITY");
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
                parent = target.Parent?.Id, target.Active, layer = target.Layer,
                tags = target.Tags.ToArray(),
                position = new { x = target.Transform.WorldPosition.X, y = target.Transform.WorldPosition.Y,
                    z = target.Transform.WorldPosition.Z },
                local_position = new { x = target.Transform.LocalPosition.X, y = target.Transform.LocalPosition.Y,
                    z = target.Transform.LocalPosition.Z },
                rotation = new { x = target.Transform.EulerAngles.X, y = target.Transform.EulerAngles.Y,
                    z = target.Transform.EulerAngles.Z },
                scale = new { x = target.Transform.WorldScale.X, y = target.Transform.WorldScale.Y,
                    z = target.Transform.WorldScale.Z },
                local_scale = new { x = target.Transform.LocalScale.X, y = target.Transform.LocalScale.Y,
                    z = target.Transform.LocalScale.Z },
                variables = target.Variables.Select(pair => new { name = pair.Key, pair.Value }).ToArray(),
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
            int count = ApplyObjectEdits(data.GameObjects, Request.Get(args, "edits"), data.Variables);
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
        if (op == "extract")
        {
            string name = Request.Required(args, "name");
            string path = Request.String(args, "path") ?? $"{Project.AssetDirectory}/{name}.byteblueprint";
            string full = Inside(path, ".byteblueprint");
            CheckRevision(full, args);
            if (File.Exists(full)) throw new McpFault("ALREADY_EXISTS");
            AssetRecord sceneAsset = Asset(Request.Required(args, "scene"), AssetType.Scene);
            var scene = Scenes.Load(sceneAsset.FullPath);
            string token = Request.Required(args, "object");
            GameObject? source = Guid.TryParse(token, out Guid sourceId)
                ? scene.FindGameObject(sourceId) : scene.FindGameObject(token);
            if (source == null) throw new McpFault("NOT_FOUND", $"Object '{token}' was not found.");

            SceneData sceneData = Scenes.Serialize(scene);
            var objects = sceneData.GameObjects;
            var subset = new List<GameObjectData>();
            void Collect(Guid id)
            {
                var node = objects.FirstOrDefault(item => item.Id == id);
                if (node == null) return;
                subset.Add(node);
                foreach (var child in objects.Where(item => item.ParentId == id)) Collect(child.Id);
            }
            Collect(source.Id);
            subset[0].ParentId = null;
            subset[0].AttachmentOffset = null;

            var blueprint = new BlueprintDefinition
            {
                Name = name,
                Root = subset[0],
                Children = subset.Skip(1).ToList()
            };
            string? kind = Request.String(args, "type");
            if (kind != null)
            {
                if (!Enum.TryParse(kind, true, out BlueprintType parsed))
                    throw new McpFault("INVALID_REQUEST", "Unknown Blueprint type.");
                blueprint.Type = parsed;
            }
            if (!Request.Bool(args, "dry_run"))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                Blueprints.Save(blueprint, full);
                Database.Scan();
                MarkChanged(full);
            }
            return new { ok = true, path = Relative(full), objects = subset.Count,
                rev = Rev(full), dry_run = Request.Bool(args, "dry_run") };
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
            int count = ApplyObjectEdits(objects, Request.Get(args, "edits"), bp.Variables);
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
            foreach (var argument in LogicRegistry.GetArguments(id)) instruction.Arguments[argument.Name] = argument.CreateDefault();
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
        if (op != "validate")
            throw new McpFault("UNSUPPORTED_CAPABILITY", "Use be_export for build and export operations.");
        var issues = new List<object>();
        string startupFull = Inside(Project.StartupScene, ".bytescene");
        bool startupExists = File.Exists(startupFull);
        if (!startupExists)
            issues.Add(new { kind = "missing_startup_scene", path = Project.StartupScene, message = "Startup scene does not exist." });
        int scenes = 0, blueprints = 0, modules = 0, startupObjects = 0;
        foreach (var asset in Database.Assets.OrderBy(item => item.ProjectPath).ToArray())
        {
            try
            {
                if (asset.Type == AssetType.Scene)
                {
                    var scene = Scenes.Load(asset.FullPath);
                    scenes++;
                    if (string.Equals(asset.ProjectPath, Project.StartupScene, StringComparison.OrdinalIgnoreCase))
                        startupObjects = scene.GameObjectCount;
                }
                else if (asset.Type == AssetType.Blueprint)
                {
                    Blueprints.Load(asset.FullPath);
                    blueprints++;
                }
                else if (asset.Type == AssetType.EventModule)
                {
                    Modules.Load(asset.FullPath);
                    modules++;
                }
            }
            catch (Exception exception)
            {
                issues.Add(new { kind = asset.Type.ToString(), path = asset.ProjectPath, message = exception.Message });
            }
        }
        return new
        {
            ok = issues.Count == 0,
            startup_scene = Project.StartupScene,
            startup_scene_exists = startupExists,
            startup_objects = startupObjects,
            scenes,
            blueprints,
            event_modules = modules,
            issues
        };
    }

    private int ApplyObjectEdits(List<GameObjectData> objects, JsonElement edits, List<VariableData>? variables = null)
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
                Type? runtimeType = FindComponentType(type);
                ComponentData? defaults = runtimeType == null ? null : Components.Serialize((Component)Activator.CreateInstance(runtimeType)!);
                if (defaults == null) throw new McpFault("INVALID_COMPONENT", $"Unknown component '{type}'.");
                string codecName = defaults.Type;
                var component = obj.Components.FirstOrDefault(c => (Components.GetCanonicalTypeName(c.Type) ?? c.Type).Equals(codecName, StringComparison.OrdinalIgnoreCase));
                if (component != null) component.Type = codecName;
                if (component == null) { component = defaults; obj.Components.Add(component); }
                foreach (var pair in Request.Properties(Request.Get(edit, "properties")))
                    component.Properties[pair.Key] = System.Text.Json.Nodes.JsonNode.Parse(pair.Value.GetRawText());
            }
            else if (op == "remove_component")
            {
                var obj = Find(); string type = Request.Required(edit, "type");
                string canonical = Components.GetCanonicalTypeName(type) ?? (FindComponentType(type) is { } registered ? Components.GetSerializedTypeName(registered) : null) ?? type;
                obj.Components.RemoveAll(c => (Components.GetCanonicalTypeName(c.Type) ?? c.Type).Equals(canonical, StringComparison.OrdinalIgnoreCase));
            }
            else if (op == "set_position" || op == "set_scale" || op == "set_rotation")
            {
                var obj = Find();
                if (op == "set_rotation")
                {
                    obj.Transform.LocalRotation = EulerQuaternion(
                        Request.Float(edit, "x"), Request.Float(edit, "y"), Request.Float(edit, "z"));
                }
                else
                {
                    var vec = new Vector3Data { X = Request.Float(edit, "x"), Y = Request.Float(edit, "y"),
                        Z = Request.Float(edit, "z", op == "set_scale" ? 1 : 0) };
                    if (op == "set_position") obj.Transform.LocalPosition = vec;
                    else obj.Transform.LocalScale = vec;
                }
            }
            else if (op == "set_transform")
            {
                var obj = Find();
                JsonElement position = Request.Get(edit, "position");
                if (position.ValueKind == JsonValueKind.Object) obj.Transform.LocalPosition = ReadVector(position, 0f);
                JsonElement rotation = Request.Get(edit, "rotation");
                if (rotation.ValueKind == JsonValueKind.Object)
                    obj.Transform.LocalRotation = EulerQuaternion(
                        Request.Float(rotation, "x"), Request.Float(rotation, "y"), Request.Float(rotation, "z"));
                JsonElement scale = Request.Get(edit, "scale");
                if (scale.ValueKind == JsonValueKind.Object) obj.Transform.LocalScale = ReadVector(scale, 1f);
            }
            else if (op == "duplicate_object")
            {
                var source = Find();
                string? parentToken = Request.String(edit, "parent");
                var subtree = new List<GameObjectData>();
                void Collect(GameObjectData node)
                {
                    subtree.Add(node);
                    foreach (var child in objects.Where(o => o.ParentId == node.Id)) Collect(child);
                }
                Collect(source);
                var map = subtree.ToDictionary(o => o.Id, _ => Guid.NewGuid());
                var clones = new List<GameObjectData>(subtree.Count);
                foreach (var original in subtree)
                {
                    var clone = CloneObject(original);
                    clone.Id = map[original.Id];
                    if (original.ParentId is Guid parentId && map.TryGetValue(parentId, out Guid mappedParent))
                        clone.ParentId = mappedParent;
                    if (original == source)
                    {
                        string? name = Request.String(edit, "name");
                        if (name != null) clone.Name = name;
                        if (parentToken != null) clone.ParentId = parentToken == "none" ? null : FindId(parentToken);
                    }
                    foreach (var component in clone.Components)
                        SceneSerializer.RemapObjectReferences(component.Properties, map);
                    clones.Add(clone);
                }
                objects.AddRange(clones);
                string? alias = Request.String(edit, "as");
                if (alias != null) temporary[alias] = clones[0].Id;
            }
            else if (op == "set_tags")
            {
                var obj = Find();
                if (Request.Bool(edit, "clear"))
                {
                    obj.Tags.Clear();
                }
                else
                {
                    var ids = new List<Guid>();
                    foreach (var item in Request.Items(Request.Get(edit, "tags")))
                    {
                        if (item.ValueKind != JsonValueKind.String) throw new McpFault("INVALID_REQUEST", "Tags must be names or GUIDs.");
                        string name = item.GetString()!;
                        var tag = Guid.TryParse(name, out Guid id)
                            ? Project.Classification.FindTag(id) : Project.Classification.FindTag(name);
                        if (tag == null)
                            throw new McpFault("NOT_FOUND", $"Unknown tag '{name}'. Add it with be_project(op:\"add_tag\").");
                        ids.Add(tag.Id);
                    }
                    obj.Tags = ids.Distinct().ToList();
                }
            }
            else if (op == "set_variable")
            {
                string name = Request.Required(edit, "name");
                List<VariableData> scope = target == null
                    ? variables ?? throw new McpFault("INVALID_REQUEST", "This document has no scene/blueprint variables.")
                    : Find().Variables;
                int index = scope.FindIndex(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (Request.Bool(edit, "remove"))
                {
                    if (index >= 0) scope.RemoveAt(index);
                }
                else if (index >= 0)
                {
                    scope[index].Value = ReadVariable(Request.Get(edit, "value"));
                }
                else
                {
                    scope.Add(new VariableData { Name = name, Value = ReadVariable(Request.Get(edit, "value")) });
                }
            }
            else throw new McpFault("UNSUPPORTED_CAPABILITY", $"Unknown edit '{op}'.");
            count++;
        }
        return count;
    }

    private static QuaternionData EulerQuaternion(float x, float y, float z)
    {
        var rotation = Quaternion.CreateFromYawPitchRoll(
            y * MathF.PI / 180f, x * MathF.PI / 180f, z * MathF.PI / 180f);
        return new QuaternionData { X = rotation.X, Y = rotation.Y, Z = rotation.Z, W = rotation.W };
    }

    private static Vector3Data ReadVector(JsonElement value, float fallback) => new()
    {
        X = Request.Float(value, "x", fallback),
        Y = Request.Float(value, "y", fallback),
        Z = Request.Float(value, "z", fallback)
    };

    private static VariableValue ReadVariable(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number => VariableValue.FromNumber(value.GetDouble()),
        JsonValueKind.String => VariableValue.FromString(value.GetString()!),
        JsonValueKind.True => VariableValue.FromBoolean(true),
        JsonValueKind.False => VariableValue.FromBoolean(false),
        JsonValueKind.Object when value.TryGetProperty("z", out _) =>
            VariableValue.FromVector3(new Vector3(Request.Float(value, "x"), Request.Float(value, "y"), Request.Float(value, "z"))),
        JsonValueKind.Object =>
            VariableValue.FromVector2(new Vector2(Request.Float(value, "x"), Request.Float(value, "y"))),
        _ => throw new McpFault("INVALID_REQUEST", "Variable values must be a number, string, boolean, {x,y} or {x,y,z}.")
    };

    private static readonly JsonSerializerOptions CloneJson = new();

    private static GameObjectData CloneObject(GameObjectData source) =>
        JsonSerializer.Deserialize<GameObjectData>(
            JsonSerializer.Serialize(source, CloneJson), CloneJson)!;

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
