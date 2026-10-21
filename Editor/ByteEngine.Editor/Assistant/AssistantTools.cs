using System.ComponentModel;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
using Component = ByteEngine.Core.Scene.Component;
using ByteEngine.Editor.Panels;

namespace ByteEngine.Editor.Assistant;

/// <summary>Small, native, main-thread tool surface. No model-generated code is executed.</summary>
internal sealed partial class AssistantTools(EditorState state, string projectRoot)
{
    private readonly Guid? _selected = state.SelectedObject?.Id;
    private Scene _scene = state.EditorScene;
    private bool _applied;
    private int _calls, _applyAttempts;
    public bool AllowEdits { get; set; } = true;
    public bool BudgetExhausted => _calls > 16;
    public List<string> Changes { get; } = [];
    private static readonly JsonSerializerOptions Json = JsonSerialization.Options;
    public static readonly string Instructions = """
        You are ByteEngine's game-building assistant inside its editor. Use only the provided native tools; no shell, arbitrary files, generated code or delegation.
        Build the requested result completely using editable engine components, assets and event sheets. Do not substitute a stationary burst for a traveling projectile.
        For character animation requests, FIRST call byte_animations on the character (or model asset) to obtain real clips and rig data. You CAN author and assign native .byteanim profiles with create_animation_profile; do not claim these tools are unavailable. Omitted locomotion clips can be discovered automatically. Reuse existing controllers/cameras rather than duplicate them.
        You CAN create custom layered .bvfx effects, .bmat materials, bounded .obj mesh geometry and .byteblueprint prefabs using byte_apply; query byte_catalog kind=asset_types for schemas. Do not tell the user to create assets you can author.
        Context contains relevant engine recipes. Follow those without redundantly looking up every familiar component. Use byte_catalog queries to batch unfamiliar API searches in ONE call. Use byte_reference for exact engine implementation when guides/catalogues are insufficient. Never guess APIs or repeatedly search synonyms after a missing result.
        $selected is frozen at Send. Inspect relevant objects once, choose a complete approach, then apply a single batch (up to 40 operations). Failed batches roll back and can be corrected once. Successful batches have one scene Undo. New assets persist on Undo for recovery; appends to existing event sheets are restored by Undo. Other existing asset types cannot be overwritten.
        add_component can include properties:{...}; set_properties sets several editable properties at once. Guid properties accept @createdObject or $selected. Asset references accept $asset:name for assets created earlier in the batch. add_event_rule arguments may also use $asset:name.
        create_vfx data:{name,path,preset?,properties?,layers?:[{...layer properties}]}; create_material:{name,path,properties}; save_blueprint target=scene object, data:{name,path}; set_parent:{parent,keepWorld?}; delete_object removes a requested object or your own temporary template. Asset paths must be new paths inside the project's asset directory.
        add_event_rule data includes sheet (a descriptive sheet name, e.g. Player Movement) and name (a descriptive event row name, e.g. Sprint held - run). Related rows MUST use the SAME sheet name; they append to that attached sheet across requests. Default sheet is <object name> Events. Inspect existing sheets first and reuse their name. Different triggers such as sprint held/released need separate rows in ONE sheet; never AND mutually exclusive triggers together. Actions sharing the same conditions belong in one row, in execution order. Do not create one sheet per action or use generic Rule/GUID names. Conditions are AND; system.always runs every frame. Nested logic and existing rule rewriting are unsupported; duplicate row names are rejected.
        Use byte_inspect detail=true only when full property/event arguments are needed; default summaries are compact. Tool-call budget is 16. Keep replies concise and do not claim gameplay testing from property inspection.
        Native EventValue references are supported for live values. A custom VFX asset is configurable particle art, not a generated mesh or texture; explain that boundary honestly.
        """;

    public static object[] Definitions => [
        Spec("byte_animations", "List actual model-owned animation clips/durations, skeleton availability and suggested Idle/Walk/Run slots for target object (default $selected) or model asset path. Includes model sources on descendants. Native import/metadata; no file guessing or manual SkeletalMeshRenderer required.", new { target = Str(), model = Str(), search = Str() }),
        Spec("byte_reference", "Search build-matched engine source/documentation excerpts locally. Use exact component/property/action names when the registry or guides cannot explain behavior. Read-only; not code execution. To read past a search snippet, provide exact source and line.", new { search = Str(), source = Str(), line = new { type = "integer", minimum = 1 } }),
        Spec("byte_help", "Search built-in engine setup guides. Empty search lists topics. Use camera, movement, input, events, physics, assets or UI. Component and instruction details remain available via byte_catalog.", new { search = Str() }),
        Spec("byte_inspect", "Inspect selected object or search scene. Default compact object/component/child summary (transform-only nodes hidden unless includeBones=true or searched); detail=true adds properties, variables and event arguments. asset=project path reads a native VFX/material/Blueprint/event asset. Empty target=$selected.",
            new { target = Str(), search = Str(), asset = Str(), includeBones = new { type = "boolean" }, detail = new { type = "boolean" } }),
        Spec("byte_catalog", "Discover loaded engine/plugin APIs or native asset schemas. kind=components|conditions|actions|assets|asset_types|systems. Batch up to8 {kind,search} queries in queries. Component search accepts multiple exact type names separated by spaces; otherwise all search words must match. Returns exact IDs, arguments and property types; use systems for the full category index.",
            new { kind = Str(), search = Str(), queries = new { type = "array", maxItems = 8, items = new { type = "object", properties = new { kind = Str(), search = Str() }, required = new[] { "kind", "search" }, additionalProperties = false } } }),
        Spec("byte_apply", "Apply ONE atomic batch (max 40 operations) with one Undo. Each operation has kind,target,data. target=$selected, exact object GUID/name, or @name of an object created earlier in this batch. create_object data:{name,parent?,primitive?:Cube|Sphere|Plane}; rename_object:{name}; set_transform:{position?:[x,y,z],rotation?:[pitch,yaw,roll] degrees,scale?:[x,y,z]}; add_component:{component:exact type,properties?:{...}}; set_properties:{component,properties:{...}}; create_vfx:{name,path,preset?,properties?,layers?}; create_material:{name,path,properties}; create_mesh:{name,path,vertices,triangles}; create_animation_profile:{name,path,model,locomotion?,sections?}; instantiate_asset:{name,path,position?}; save_blueprint:{name,path}; set_parent:{parent,keepWorld?}; delete_object:{}; set_property:{component,property,value}; set_variable:{name,value}; add_event_rule:{sheet?,name,conditions:[{id,arguments:{...}}],actions:[{id,arguments:{...}}]}. Related rules append to the SAME named attached sheet, preserving existing rules. Use descriptive sheet and row names. Appending existing sheets supports Undo; dirty open sheets must first be saved. Guid properties accept $selected or @createdName to link objects in the same batch. InputActionReference properties accept a named action string. No arbitrary code; use native asset schemas from catalogue.",
            new { label = Str(), operations = new { type = "array", maxItems = 40, items = new { type = "object", properties = new { kind = Str(), target = Str(), data = new { type = "object", additionalProperties = true } }, required = new[] { "kind", "target", "data" }, additionalProperties = false } } })
    ];
    private static object Str() => new { type = "string" };
    internal static object ThreadParameters(string model) => new { model, modelProvider = "openai", cwd = CodexConnection.Workspace, sandbox = "read-only", approvalPolicy = "never", ephemeral = true,
        environments = Array.Empty<object>(), baseInstructions = Instructions, dynamicTools = Definitions,
        config = new Dictionary<string, object> { ["project_doc_max_bytes"] = 0, ["web_search"] = "disabled" } };
    private static object Spec(string name, string description, object properties) => new { type = "function", name, description, deferLoading = false,
        inputSchema = new { type = "object", properties, additionalProperties = false } };
    public string Snapshot() => Serialize(new { project = state.Project.Name, scene = state.EditorScene.Name,
        selected = _selected is {} id && state.EditorScene.FindGameObject(id) is {} obj ? new { id = obj.Id, name = obj.Name } : null,
        mode = state.Mode.ToString(), editPermission = AllowEdits,
        inputActions = state.Project.InputMap.Actions.Select(a => new { name = a.DisplayName, type = a.Type.ToString(), bindings = a.Bindings.Select(InputBindingSummary) }),
        capabilities = "Native scene objects/components, event rules, custom VFX, materials, Blueprint and animation profile assets. byte_animations lists model clips. Use batch catalogue queries and asset_types schemas." });
    private static string InputBindingSummary(ByteEngine.Core.InputSystem.InputBinding b) => b.Type switch
    {
        ByteEngine.Core.InputSystem.InputBindingType.KeyboardKey => "Key:" + b.Key,
        ByteEngine.Core.InputSystem.InputBindingType.MouseButton => "Mouse:" + b.MouseButton,
        ByteEngine.Core.InputSystem.InputBindingType.Keyboard2DComposite => $"Keys:{b.UpKey}/{b.DownKey}/{b.LeftKey}/{b.RightKey}",
        ByteEngine.Core.InputSystem.InputBindingType.KeyboardAxis => $"Axis:{b.NegativeKey}/{b.PositiveKey}",
        ByteEngine.Core.InputSystem.InputBindingType.GamepadButton or ByteEngine.Core.InputSystem.InputBindingType.GamepadStick or ByteEngine.Core.InputSystem.InputBindingType.GamepadTrigger => b.Type + ":" + b.GamepadControl,
        _ => b.Type.ToString()
    };
    public object Execute(string tool, JsonElement args)
    {
        if (++_calls > 16) throw new InvalidOperationException("Task tool-call limit reached. Send a smaller follow-up request.");
        if (!ReferenceEquals(state, EditorState.Active) || !ReferenceEquals(_scene, state.EditorScene))
            throw new InvalidOperationException("The scene changed during this request. Start a new request.");
        return tool switch { "byte_animations" => Animations(args), "byte_reference" => AssistantReference.Search(Text(args, "search"), Text(args, "source"), args.TryGetProperty("line", out var line) ? line.GetInt32() : 1), "byte_help" => AssistantKnowledge.Search(Text(args, "search")), "byte_inspect" => Inspect(args), "byte_catalog" => Catalog(args), "byte_apply" => Apply(args), _ => throw new InvalidOperationException("Unknown ByteEngine tool.") };
    }
    private object Inspect(JsonElement args)
    {
        if (Text(args, "asset") is { Length: > 0 } assetPath) return InspectAsset(assetPath);
        string search = Text(args, "search");
        var objects = state.EditorScene.GameObjects.Where(o => string.IsNullOrEmpty(search) || o.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToArray();
        string token = Text(args, "target", "$selected");
        GameObject? selected = string.IsNullOrEmpty(token) || token == "$selected" ? (_selected is {} id ? state.EditorScene.FindGameObject(id) : null) : Resolve(token, []);
        bool includeBones = args.TryGetProperty("includeBones", out var bones) && bones.GetBoolean();
        var listed = objects.Where(o => includeBones || search.Length > 0 || o.Parent == null || o.Components.Any() || !HasImportedAncestor(o)).ToArray();
        return new { scene = state.EditorScene.Name, activeCamera = state.EditorScene.ActiveCamera?.GameObject.Name, totalMatches = objects.Length, hiddenTransformNodes = objects.Length - listed.Length, objects = listed.Take(40).Select(o => new { id = o.Id, name = o.Name, parentId = o.Parent?.Id }),
            selected = selected == null ? null : args.TryGetProperty("detail", out var detail) && detail.GetBoolean() ? Describe(selected) : (object)new { id = selected.Id, name = selected.Name, active = selected.Active, position = V(selected.Transform.LocalPosition), components = selected.Components.Select(c => new { type = c.GetType().Name, enabled = c.Enabled }), children = selected.Children.Select(c => new { id = c.Id, name = c.Name }) } };
    }
    private static bool HasImportedAncestor(GameObject o)
    {
        for (var parent = o.Parent; parent != null; parent = parent.Parent)
            if (parent.GetComponent<ModelHierarchyInstance>() != null) return true;
        return false;
    }
    private object Describe(GameObject o) => new { id = o.Id, name = o.Name, active = o.Active,
        position = V(o.Transform.LocalPosition), scale = V(o.Transform.LocalScale),
        components = o.Components.Select(c => new { type = c.GetType().Name,
            properties = Editable(c.GetType()).ToDictionary(p => p.Name, p => Friendly(p.GetValue(c))),
            readOnlyValues = c.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.SetMethod?.IsPublic != true && p.GetIndexParameters().Length == 0 && Supported(p.PropertyType) && p.GetCustomAttribute<BrowsableAttribute>()?.Browsable != false).Take(20).ToDictionary(p => p.Name, p => Friendly(p.GetValue(c))) }),
        variables = o.Variables.Take(24).Select(v => new { name = v.Key, value = Short(VariableJson.Write(v.Value), 512) }),
        eventSheets = o.GetComponent<EventModuleComponent>()?.Modules.Select(reference => DescribeSheet(reference)).ToArray() };
    private object DescribeSheet(AssetReference reference)
    {
        try
        {
            string path = Inside(reference.ProjectPath); var module = new EventModuleSerializer().Load(path);
            return new { path = reference.ProjectPath, name = module.Name, rules = module.Rules.Take(12).Select(r => new { ruleId = r.Id, name = r.DisplayName, conditions = r.Conditions.Take(12).Select(c => new { id = c.Id, arguments = c.Arguments }), actions = r.Actions.Take(12).Select(a => new { id = a.Id, arguments = a.Arguments }) }).ToArray() };
        }
        catch { return new { path = reference.ProjectPath, error = "Could not read this event sheet." }; }
    }
    private object Catalog(JsonElement args)
    {
        if (args.TryGetProperty("queries", out var queries))
        {
            if (queries.GetArrayLength() > 8) throw new InvalidOperationException("Batch at most 8 queries.");
            return queries.EnumerateArray().Select(q => new { kind = Text(q, "kind"), search = Text(q, "search"), result = Catalog(q) }).ToArray();
        }
        string search = Text(args, "search"), kind = Text(args, "kind", "components");
        string[] typeNames = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        bool exactTypeList = kind == "components" && typeNames.Length > 1 && typeNames.All(n => ComponentMetadataRegistry.RegisteredTypes.Any(t => t.Name.Equals(n, StringComparison.OrdinalIgnoreCase)));
        bool Match(string text) => exactTypeList ? typeNames.Any(n => text.StartsWith(n + " ", StringComparison.OrdinalIgnoreCase)) : search.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));
        var registry = VisualLogicRegistry.CreateDefault();
        object Arguments(string id) => EffectiveArguments(id, registry).Select(v => new { name = v.Name, label = v.DisplayName, type = v.DefaultValue.Type.ToString(), value = VariableJson.Write(v.DefaultValue), objectPicker = v.IsObjectReference, choices = v.Choices });
        return kind switch
        {
            "asset_types" => AssetSchemas(search),
            "systems" => new { components = ComponentMetadataRegistry.RegisteredTypes.GroupBy(t => ComponentMetadataRegistry.Get(t).Category).Select(g => new { category = g.Key, types = g.Select(t => t.Name) }), actions = registry.Actions.GroupBy(a => a.Category).Select(g => new { category = g.Key, count = g.Count() }), conditions = registry.Conditions.GroupBy(a => a.Category).Select(g => new { category = g.Key, count = g.Count() }) },
            "components" => ComponentMetadataRegistry.RegisteredTypes.Where(t => Match(t.Name + " " + ComponentMetadataRegistry.Get(t))).Take(12)
                .Select(t => new { type = t.Name, label = ComponentMetadataRegistry.DisplayName(t), description = ComponentMetadataRegistry.Get(t).Description,
                    properties = Editable(t).Select(p => new { name = p.Name, type = p.PropertyType.Name, description = ComponentMetadataRegistry.GetProperty(t, p.Name)?.Tooltip ?? p.GetCustomAttribute<DescriptionAttribute>()?.Description, choices = p.PropertyType.IsEnum ? Enum.GetNames(p.PropertyType) : null }), requires = ComponentMetadataRegistry.Get(t).RequiresComponents?.Select(x => x.Name) }).ToArray(),
            "conditions" => registry.Conditions.Where(c => Match(c.Id + " " + c.DisplayName + " " + c.Category)).Take(12).Select(c => new { id = c.Id, name = c.DisplayName, component = c.TargetComponent, arguments = Arguments(c.Id) }).ToArray(),
            "actions" => registry.Actions.Where(a => Match(a.Id + " " + a.DisplayName + " " + a.Category)).Take(12).Select(a => new { id = a.Id, name = a.DisplayName, component = a.TargetComponent, arguments = Arguments(a.Id) }).ToArray(),
            "assets" => Directory.EnumerateFiles(Path.Combine(projectRoot, state.Project.AssetDirectory), "*", SearchOption.AllDirectories).Where(p => !p.EndsWith(".meta") && Match(Path.GetRelativePath(projectRoot, p).Replace('\\', '/'))).Take(12).Select(p => Path.GetRelativePath(projectRoot, p).Replace('\\', '/')).ToArray(),
            _ => throw new InvalidOperationException("kind must be components, conditions, actions, assets, asset_types or systems.")
        };
    }

    private object Apply(JsonElement args)
    {
        if (!AllowEdits) throw new InvalidOperationException("Scene edits are disabled in the assistant panel.");
        if (_applied) throw new InvalidOperationException("Only one edit batch is allowed per request.");
        if (state.Mode != EditorMode.Edit || state.Undo == null) throw new InvalidOperationException("Open a scene in Edit mode first.");
        if (++_applyAttempts > 2) throw new InvalidOperationException("Two apply attempts allowed per request. Start a follow-up to continue.");
        var operations = args.GetProperty("operations");
        if (operations.ValueKind != JsonValueKind.Array || operations.GetArrayLength() is < 1 or > 40) throw new InvalidOperationException("Provide 1 to 40 operations.");
        var created = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        var files = new List<string>();
        var changedFiles = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        try
        {
            state.Undo.ExecuteAtomic(state, "Assistant: " + Text(args, "label", "Edit scene")[..Math.Min(80, Text(args, "label", "Edit scene").Length)], () =>
            {
                foreach (var operation in operations.EnumerateArray())
                {
                    string kind = Text(operation, "kind"), token = Text(operation, "target", "$selected");
                    var data = operation.GetProperty("data");
                    if (kind is "create_vfx" or "create_material" or "create_mesh" or "create_animation_profile") { CreateAsset(kind, data, files); continue; }
                    if (kind == "instantiate_asset")
                    {
                        string alias = "@" + Name(data, "name"); if (created.ContainsKey(alias)) throw new InvalidOperationException("Duplicate created object name.");
                        var instance = InstantiateAsset(data); created.Add(alias, instance); Changes.Add("Placed asset: " + instance.Name); continue;
                    }
                    if (kind == "create_object")
                    {
                        string name = Name(data, "name"); if (created.ContainsKey("@" + name)) throw new InvalidOperationException("Created object names must be unique within a batch.");
                        var obj = state.EditorScene.CreateGameObject(name); created.Add("@" + name, obj);
                        string parent = Text(data, "parent"); if (parent.Length > 0) obj.SetParent(Resolve(parent, created), false);
                        string primitive = Text(data, "primitive");
                        if (primitive.Length > 0)
                        { if (!Enum.TryParse<PrimitiveMeshType>(primitive, true, out var type) || !Enum.IsDefined(type)) throw new InvalidOperationException("Unknown primitive."); obj.AddComponent(new MeshRenderer { UsePrimitive = true, Primitive = type }); }
                        Changes.Add("Created " + name); continue;
                    }
                    var target = Resolve(token, created);
                    switch (kind)
                    {
                        case "rename_object": target.Name = Name(data, "name"); break;
                        case "set_transform":
                            if (data.TryGetProperty("position", out var position)) target.Transform.LocalPosition = Vec3(position);
                            if (data.TryGetProperty("scale", out var scale)) { var v = Vec3(scale); if (Math.Abs(v.X) < .0001f || Math.Abs(v.Y) < .0001f || Math.Abs(v.Z) < .0001f) throw new InvalidOperationException("Scale cannot be zero."); target.Transform.LocalScale = v; }
                            if (data.TryGetProperty("rotation", out var rotation)) { var v = Vec3(rotation) * (MathF.PI / 180); target.Transform.LocalRotation = Quaternion.CreateFromYawPitchRoll(v.Y, v.X, v.Z); }
                            break;
                        case "save_blueprint": SaveBlueprint(target, data, files); break;
                        case "set_parent": target.SetParent(Text(data, "parent").Length == 0 ? null : Resolve(Text(data, "parent"), created), !data.TryGetProperty("keepWorld", out var keepWorld) || keepWorld.GetBoolean()); break;
                        case "delete_object": state.EditorScene.DestroyGameObject(target); break;
                        case "add_component":
                            var addedType = FindType(Name(data, "component")); AddComponent(target, addedType, []);
                            if (data.TryGetProperty("properties", out var initialProperties)) SetProperties(target.Components.Single(c => c.GetType() == addedType), initialProperties, created);
                            break;
                        case "set_properties": SetProperties(target.Components.Single(c => c.GetType() == FindType(Name(data, "component"))), data.GetProperty("properties"), created); break;
                        case "set_property":
                            var component = target.Components.SingleOrDefault(c => c.GetType() == FindType(Name(data, "component"))) ?? throw new InvalidOperationException("Add the component before setting its properties.");
                            var property = Editable(component.GetType()).SingleOrDefault(p => p.Name.Equals(Name(data, "property"), StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException("This property is not editable through the assistant.");
                            property.SetValue(component, property.PropertyType == typeof(Guid) && data.GetProperty("value").ValueKind == JsonValueKind.String && data.GetProperty("value").GetString() is {} refToken && (refToken.StartsWith("@") || refToken == "$selected") ? Resolve(refToken, created).Id : ConvertValue(data.GetProperty("value"), property.PropertyType)); break;
                        case "set_variable": target.Variables.Set(Name(data, "name"), VariableJson.Parse(data.GetProperty("value").GetRawText())); break;
                        case "add_event_rule": AttachRule(target, data, files, changedFiles); break;
                        default: throw new InvalidOperationException("Unsupported operation: " + kind);
                    }
                    Changes.Add(kind.Replace('_', ' ') + ": " + target.Name);
                }
            }, changedFiles);
        }
        catch
        {
            // These are fresh, uniquely named generated assets from this failed transaction only.
            foreach (var file in files) { try { File.Delete(file); } catch { } }
            EventWorkspacePanel.ReloadExternalEdits(changedFiles.Keys);
            _scene = state.EditorScene; _assetAliases.Clear(); Changes.Clear(); throw;
        }
        _applied = true;
        EventWorkspacePanel.ReloadExternalEdits(changedFiles.Keys);
        if (EditorProjectContext.Active is {} activeProject && Path.GetFullPath(activeProject.ProjectRoot) == Path.GetFullPath(projectRoot)) activeProject.AssetDatabase.RefreshPaths(files.Concat(changedFiles.Keys).Where(f => !f.EndsWith(".meta") && !f.EndsWith(".tmp")));
        return new { applied = true, assets = _assetAliases, undo = operations.EnumerateArray().Any(o => Text(o, "kind") is not ("create_vfx" or "create_material" or "create_mesh" or "create_animation_profile" or "save_blueprint")) ? state.Undo.UndoName : null, assetsRemainOnUndo = true, changes = Changes.ToArray(), created = created.Select(p => new { alias = p.Key, id = p.Value.Id, name = p.Value.Name }).ToArray(), saveRequired = true };
    }
    private void AttachRule(GameObject target, JsonElement data, List<string> files, Dictionary<string, byte[]> changedFiles)
    {
        var registry = VisualLogicRegistry.CreateDefault();
        var conditions = InstructionsFrom(data.GetProperty("conditions"), true, registry);
        var actions = InstructionsFrom(data.GetProperty("actions"), false, registry);
        if (conditions.Count == 0 || actions.Count == 0) throw new InvalidOperationException("A rule needs a condition and an action. Use system.always for an unconditional rule.");
        var rule = new EventRuleDefinition { DisplayName = Name(data, "name"), Conditions = conditions, Actions = actions,
            HasExplicitConditionFlow = true, ConnectedConditionIds = conditions.Select(c => c.InstanceId).ToList(), HasExplicitExecutionFlow = true, FirstActionId = actions[0].InstanceId };
        for (int n = 0; n < actions.Count - 1; n++) actions[n].NextActionId = actions[n + 1].InstanceId;
        string sheetName = Text(data, "sheet", target.Name + " Events").Trim();
        if (sheetName.Length is < 1 or > 100 || sheetName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || sheetName.EndsWith('.') ||
            sheetName is "." or "..") throw new InvalidOperationException("Use a short descriptive sheet name without path characters.");
        string relative = state.Project.AssetDirectory.TrimEnd('/', '\\') + "/Events/Assistant/" + sheetName + ".byteevents";
        string path = Inside(relative);
        for (var directory = new DirectoryInfo(Path.GetDirectoryName(path)!); directory != null && directory.FullName.StartsWith(Path.GetFullPath(projectRoot), StringComparison.OrdinalIgnoreCase); directory = directory.Parent)
            if (directory.Exists && directory.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidOperationException("Event assets cannot be edited through linked directories.");
        var component = target.GetComponent<EventModuleComponent>();
        bool exists = File.Exists(path);
        if (exists && component?.Modules.Any(reference => string.Equals(reference.ProjectPath.Replace('\\', '/'), relative.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)) != true)
            throw new InvalidOperationException("That sheet name already exists but is not attached to this object. Choose a distinct descriptive sheet name.");
        if (exists && !files.Contains(path))
        {
            if (state.EditorScene.GameObjects.Where(obj => obj != target).Any(obj => obj.GetComponent<EventModuleComponent>()?.Modules.Any(reference => string.Equals(reference.ProjectPath.Replace('\\', '/'), relative.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)) == true))
                throw new InvalidOperationException("This sheet is shared by other objects. Use a separate sheet name to avoid changing their behavior.");
            EventWorkspacePanel.PrepareExternalEdits([path]);
            changedFiles.TryAdd(path, File.ReadAllBytes(path));
        }
        var serializer = new EventModuleSerializer();
        var module = exists ? serializer.Load(path) : new EventModuleDefinition { Name = sheetName };
        if (module.Rules.Any(existing => existing.DisplayName.Equals(rule.DisplayName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("A rule with this name already exists in the sheet. Inspect existing rules and avoid duplicate behavior.");
        module.Rules.Add(rule);
        if (!exists)
        {
            if (File.Exists(path + ".meta")) throw new InvalidOperationException("An asset metadata file already exists at this path.");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!); files.Add(path); files.Add(path + ".meta");
        }
        serializer.Save(module, path);
        if (!exists)
        {
            Guid assetId = Guid.NewGuid(); File.WriteAllText(path + ".meta", JsonSerializer.Serialize(new { guid = assetId, type = "EventModule" }));
            (component ?? target.AddComponent(new EventModuleComponent())).AddModuleReference(new AssetReference(assetId, relative));
        }
        Changes.Add("Event sheet: " + sheetName + " / " + rule.DisplayName);
    }
    private List<VisualInstruction> InstructionsFrom(JsonElement list, bool condition, VisualLogicRegistry registry)
    {
        if (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() > 12) throw new InvalidOperationException("Use at most 12 instructions per side.");
        var result = new List<VisualInstruction>();
        foreach (var item in list.EnumerateArray())
        {
            string id = Name(item, "id"); IReadOnlyList<VisualArgumentDefinition> definitions;
            if (condition) { if (!registry.TryGetCondition(id, out _)) throw new InvalidOperationException("Unknown condition " + id); }
            else { if (!registry.TryGetAction(id, out _)) throw new InvalidOperationException("Unknown action " + id); }
            definitions = EffectiveArguments(id, registry);
            if (id is "logic.and" or "logic.or" or "logic.not") throw new InvalidOperationException("Current version creates flat AND rules; nested logic blocks are not supported yet.");
            var instruction = EventWorkspacePanel.CreateInstructionDefaults(id, registry);
            if (item.TryGetProperty("arguments", out var args)) foreach (var arg in args.EnumerateObject())
            {
                var expected = definitions.FirstOrDefault(d => d.Name == arg.Name)?.DefaultValue.Type;
                instruction.Arguments[arg.Name] = arg.Value.ValueKind == JsonValueKind.Object && arg.Value.TryGetProperty("kind", out _) ? JsonSerializer.Deserialize<EventValue>(arg.Value.GetRawText(), Json)! : new EventValue { Constant =
                    expected == VariableType.Vector3 ? VariableValue.FromVector3(Vec3(arg.Value)) :
                    expected == VariableType.Vector2 ? VariableValue.FromVector2(new Vector2(Numbers(arg.Value, 2)[0], Numbers(arg.Value, 2)[1])) : VariableJson.Parse(arg.Value.ValueKind == JsonValueKind.String && _assetAliases.TryGetValue(arg.Value.GetString()!, out var assetPath) ? JsonSerializer.Serialize(assetPath) : arg.Value.GetRawText()) };
            }
            result.Add(instruction);
        }
        return result;
    }
    private static IReadOnlyList<VisualArgumentDefinition> EffectiveArguments(string id, VisualLogicRegistry registry)
    {
        var values = registry.GetArguments(id).ToDictionary(a => a.Name, StringComparer.Ordinal);
        foreach (var entry in EventWorkspacePanel.CreateInstructionDefaults(id, registry).Arguments)
            values.TryAdd(entry.Key, new VisualArgumentDefinition(entry.Key, entry.Key, entry.Value.Constant) { IsObjectReference = entry.Key == "target" });
        return values.Values.ToArray();
    }
    private GameObject Resolve(string token, Dictionary<string, GameObject> created)
    {
        if (created.TryGetValue(token, out var result)) return result;
        if (token == "$selected") return (_selected is {} id ? state.EditorScene.FindGameObject(id) : null) ?? throw new InvalidOperationException("No selected object. Select one or provide an exact object ID.");
        if (Guid.TryParse(token, out var guid)) return state.EditorScene.FindGameObject(guid) ?? throw new InvalidOperationException("Object ID no longer exists.");
        var matches = state.EditorScene.GameObjects.Where(o => o.Name == token).ToArray();
        return matches.Length == 1 ? matches[0] : throw new InvalidOperationException("Use an exact object GUID; this name is missing or ambiguous.");
    }
    private static Type FindType(string name) => ComponentMetadataRegistry.RegisteredTypes.SingleOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException("Unknown or ambiguous component type. Use byte_catalog first.");
    private static void AddComponent(GameObject obj, Type type, HashSet<Type> visiting)
    {
        if (obj.Components.Any(c => c.GetType() == type)) return;
        if (!visiting.Add(type)) throw new InvalidOperationException("Cyclic component dependency.");
        foreach (var required in ComponentMetadataRegistry.Get(type).RequiresComponents ?? []) AddComponent(obj, required, visiting);
        obj.AddComponent((Component)(Activator.CreateInstance(type) ?? throw new InvalidOperationException("Component cannot be created."))); visiting.Remove(type);
    }
    internal static IEnumerable<PropertyInfo> Editable(Type type) => type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.SetMethod?.IsPublic == true && p.GetIndexParameters().Length == 0 && p.GetCustomAttribute<BrowsableAttribute>()?.Browsable != false && ComponentMetadataRegistry.GetProperty(type, p.Name)?.ReadOnly != true && Supported(p.PropertyType));
    private static bool Supported(Type t) => t.IsEnum || t == typeof(string) || t == typeof(bool) || t == typeof(int) || t == typeof(uint) || t == typeof(float) || t == typeof(double) || t == typeof(Guid) || t == typeof(Vector2) || t == typeof(Vector3) || t == typeof(Vector4) || t == typeof(Quaternion) || t == typeof(AssetReference) || t == typeof(ByteEngine.Core.InputSystem.InputActionReference);
    private object ConvertValue(JsonElement value, Type type)
    {
        if (type == typeof(ByteEngine.Core.InputSystem.InputActionReference)) return ByteEngine.Core.InputSystem.InputActionReference.Named(value.GetString() ?? throw new InvalidOperationException("Use an input action name."));
        if (type == typeof(Vector3)) return Vec3(value);
        if (type == typeof(Vector2)) { var a = Numbers(value, 2); return new Vector2(a[0], a[1]); }
        if (type == typeof(Vector4)) { var a = Numbers(value, 4); return new Vector4(a[0], a[1], a[2], a[3]); }
        if (type == typeof(Quaternion)) { var a = Numbers(value, 4); var q = new Quaternion(a[0], a[1], a[2], a[3]); if (q.LengthSquared() < .00001f) throw new InvalidOperationException("Invalid rotation."); return Quaternion.Normalize(q); }
        if (type == typeof(AssetReference)) { string path = value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetProperty("path").GetString()!; if (path.Length == 0) return AssetReference.Empty; path = _assetAliases.GetValueOrDefault(path, path); if (!File.Exists(Inside(path))) throw new InvalidOperationException("Asset does not exist in this project."); return new AssetReference(path); }
        if (type.IsEnum) return Enum.TryParse(type, value.GetString(), true, out var e) && e != null && Enum.IsDefined(type, e) ? e : throw new InvalidOperationException("Invalid enum choice.");
        if (type == typeof(float) || type == typeof(double)) { double n = value.GetDouble(); if (!double.IsFinite(n) || Math.Abs(n) > 1e9) throw new InvalidOperationException("Number is outside the supported range."); }
        return JsonSerializer.Deserialize(value.GetRawText(), type, Json) ?? throw new InvalidOperationException("Value cannot be null.");
    }
    private string Inside(string relative)
    {
        string root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(root, relative));
        if (Path.IsPathRooted(relative) || !path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Use an asset path inside this project."); return path;
    }
    private static float[] Numbers(JsonElement value, int count) { var a = value.EnumerateArray().Select(x => x.GetSingle()).ToArray(); if (a.Length != count || a.Any(x => !float.IsFinite(x) || Math.Abs(x) > 1e6)) throw new InvalidOperationException("Invalid vector."); return a; }
    private static Vector3 Vec3(JsonElement value) { var a = Numbers(value, 3); return new(a[0], a[1], a[2]); }
    private static float[] V(Vector3 v) => [v.X, v.Y, v.Z];
    private static object? Friendly(object? value) => value switch { string s => Short(s, 1024), Vector3 v => V(v), Vector2 v => new[] { v.X, v.Y }, Vector4 v => new[] { v.X, v.Y, v.Z, v.W }, Quaternion q => new[] { q.X, q.Y, q.Z, q.W }, Enum e => e.ToString(), AssetReference a => a.ProjectPath, _ => value };
    private static string Short(string value, int limit) => value.Length <= limit ? value : value[..limit] + " [truncated]";
    private static readonly JsonSerializerOptions CompactJson = new(Json) { WriteIndented = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    internal static string Serialize(object value) => JsonSerializer.Serialize(value, CompactJson);
    internal static string Text(JsonElement value, string name, string fallback = "") => value.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? fallback : fallback;
    private static string Name(JsonElement value, string name) { string result = Text(value, name).Trim(); if (result.Length is < 1 or > 160) throw new InvalidOperationException("Missing or overlong " + name); return result; }
}
