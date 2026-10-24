using System.Numerics;
using System.Reflection;
using System.Text.Json;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.Vfx;

namespace ByteEngine.Editor.Assistant;

internal sealed partial class AssistantTools
{
    private readonly Dictionary<string, string> _assetAliases = new(StringComparer.Ordinal);

    private object InspectAsset(string relative)
    {
        string path = Inside(relative);
        if (!File.Exists(path) || new FileInfo(path).Length > 512 * 1024) throw new InvalidOperationException("Asset is missing or exceeds the 512 KB inspection limit.");
        object content = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".byteai" => ByteEngine.Core.AI.BehaviorTreeSerializer.Load(path),
            ".bvfx" => VfxEffectSerializer.Load(path),
            ".bmat" => MaterialAssetSerializer.Load(path),
            ".byteblueprint" => new BlueprintSerializer(EditorProjectContext.Active?.AssetDatabase).Load(path),
            ".byteevents" => new ByteEngine.Core.VisualLogic.EventModuleSerializer().Load(path),
            ".byteanim" => ByteEngine.Core.Animation.AnimationProfileSerializer.Load(path),
            _ => throw new InvalidOperationException("Inspect supports AI behaviours, native VFX, materials, Blueprints, animation profiles and event sheets; use catalogue for model metadata.")
        };
        string json = Serialize(content);
        if (json.Length > 24000) return new { path = relative, note = "Asset exceeds response budget. Inspect relevant attached components and use schema/reference lookup instead." };
        return new { path = relative, content };
    }

    private void SetProperties(object component, JsonElement properties, Dictionary<string, GameObject> created)
    {
        foreach (var entry in properties.EnumerateObject())
        {
            var property = Editable(component.GetType()).SingleOrDefault(p => p.Name.Equals(entry.Name, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("Unknown or unsupported editable property: " + entry.Name);
            object value = property.PropertyType == typeof(Guid) && entry.Value.ValueKind == JsonValueKind.String && entry.Value.GetString() is {} token && (token.StartsWith('@') || token == "$selected")
                ? Resolve(token, created).Id : ConvertValue(entry.Value, property.PropertyType);
            property.SetValue(component, value);
        }
    }

    private static object Schema(Type type) => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.SetMethod?.IsPublic == true && p.GetIndexParameters().Length == 0)
        .Select(p => new { name = p.Name, type = p.PropertyType.Name, choices = p.PropertyType.IsEnum ? Enum.GetNames(p.PropertyType) : null });

    private static object AssetSchemas(string search)
    {
        var list = new List<object>();
        bool Matches(string text) => search.Length == 0 || search.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(w => text.Contains(w, StringComparison.OrdinalIgnoreCase));
        if (Matches("vfx fireball particles effect")) list.Add(new { kind = "create_vfx", extension = ".bvfx", data = "{name,path,preset?,properties?:{Loop,Duration,ParticleBudget},layers?:[{layer properties}]}. Preset supplies a starting point; layers replaces its layers. Vector values use arrays. LocalSpace=true follows projectile; false leaves a trail. Use $asset:name afterward.", presets = Enum.GetNames<VfxPreset>(), effect = Schema(typeof(VfxEffect)), layer = Schema(typeof(VfxLayer)), curve = "{keys:[{time:0,value:1},{time:1,value:0}]} (at most 8 keys)" });
        if (Matches("material bmat surface color emission")) list.Add(new { kind = "create_material", extension = ".bmat", data = "{name,path,properties:{...MaterialParameters}}. Vector values use arrays; textures use existing project-relative paths. Set MeshRenderer.MaterialAssetReference=$asset:name.", properties = Schema(typeof(MaterialParameters)) });
        if (Matches("mesh geometry model obj asset")) list.Add(new { kind = "create_mesh", extension = ".obj", data = "{name,path,vertices:[[x,y,z],...],triangles:[[a,b,c],...]}. 3..4096 vertices, up to8192 triangles, zero-based indices, counterclockwise winding, flat normals generated. Native OBJ importer reads this asset. instantiate_asset:{name,path,position?} places a project Model3D/Blueprint using the native editor importer; @name references its root afterward." });
        if (Matches("animation profile locomotion idle walk run rig character")) list.Add(new { kind = "create_animation_profile", extension = ".byteanim", data = "{name,path,model,locomotion?:{Idle,Walk,Run,Jump,Fall,Land,RunThreshold,MoveThreshold,RootMotionMode,...},sections?:{Actions,BlendSpaces,Layers,SyncGroups,StateGraph,Procedural}}. byte_animations lists exact model-owned clips, durations and recommended slots. Omitted locomotion clips are discovered; unsupported names are rejected. Assign AnimationController.AnimationProfile=$asset:name on character root. Native controller handles skeletal playback, do not add a second renderer manually.", locomotion = Schema(typeof(ByteEngine.Core.Animation.AnimationLocomotionProfile)), sections = Schema(typeof(ByteEngine.Core.Animation.AnimationProfile)), action = Schema(typeof(ByteEngine.Core.Animation.AnimationActionProfile)) });
        if (Matches("blueprint prefab projectile asset object")) list.Add(new { kind = "save_blueprint", extension = ".byteblueprint", data = "target=scene object; data:{name,path}. Captures target and descendants through native serializers; root position becomes zero. Existing scene object remains; delete your temporary template afterward with delete_object. Reference the asset using $asset:name in components/event arguments. All paths must be NEW paths inside the project's asset directory." });
        if (Matches("ai behavior behaviour enemy perception hearing blackboard squad tactical subtree services")) list.Add(AIAssetSchema());
        return list;
    }

    private (string Path, string Relative, string Alias) NewAsset(JsonElement data, string extension, List<string> files)
    {
        string relative = Name(data, "path").Replace('\\', '/');
        string assetRoot = state.Project.AssetDirectory.Replace('\\', '/').TrimEnd('/') + "/";
        string path = Inside(relative);
        if (!relative.StartsWith(assetRoot, StringComparison.OrdinalIgnoreCase) || !path.StartsWith(Path.GetFullPath(Path.Combine(projectRoot, assetRoot)), StringComparison.OrdinalIgnoreCase) || !relative.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use a new " + extension + " path under " + assetRoot);
        // Generated assets must never follow a symlink/junction out of the project.
        for (var dir = new DirectoryInfo(Path.GetDirectoryName(path)!); dir != null && dir.FullName.StartsWith(Path.GetFullPath(projectRoot), StringComparison.OrdinalIgnoreCase); dir = dir.Parent)
            if (dir.Exists && (dir.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Asset directories cannot be symbolic links or junctions.");
        if (File.Exists(path) || File.Exists(path + ".meta")) throw new InvalidOperationException("Asset already exists. Choose a new path; existing assets are preserved.");
        string alias = "$asset:" + Name(data, "name");
        if (_assetAliases.ContainsKey(alias)) throw new InvalidOperationException("Asset names must be unique within a batch.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        files.Add(path); files.Add(path + ".meta"); files.Add(path + ".tmp");
        return (path, Path.GetRelativePath(projectRoot, path).Replace('\\', '/'), alias);
    }

    private void FinishAsset((string Path, string Relative, string Alias) asset, AssetType type)
    {
        File.WriteAllText(asset.Path + ".meta", JsonSerializer.Serialize(new { guid = Guid.NewGuid(), type = type.ToString() }));
        _assetAliases.Add(asset.Alias, asset.Relative);
        Changes.Add("Created " + type + ": " + asset.Relative);
    }

    private void CreateAsset(string kind, JsonElement data, List<string> files)
    {
        if (kind == "create_ai_behavior") { CreateAIBehavior(data, files); return; }
        if (kind == "create_animation_profile") { CreateAnimationProfile(data, files); return; }
        if (kind == "create_vfx")
        {
            var asset = NewAsset(data, ".bvfx", files);
            var effect = new VfxEffect();
            if (data.TryGetProperty("preset", out var preset))
                effect = VfxPresets.Create((VfxPreset)ConvertValue(preset, typeof(VfxPreset)));
            if (data.TryGetProperty("properties", out var properties)) SetAssetProperties(effect, properties, ["Version", "Layers"]);
            effect.Name = Name(data, "name");
            if (data.TryGetProperty("layers", out var layers))
            {
                if (layers.ValueKind != JsonValueKind.Array || layers.GetArrayLength() is < 1 or > 8) throw new InvalidOperationException("Provide 1 to 8 VFX layers.");
                effect.Layers.Clear();
                foreach (var definition in layers.EnumerateArray()) { var layer = new VfxLayer(); SetAssetProperties(layer, definition, []); effect.Layers.Add(layer); }
            }
            if (effect.Layers.Count == 0) throw new InvalidOperationException("A custom effect needs a preset or layers.");
            effect.ParticleBudget = Math.Clamp(effect.ParticleBudget, 1, 2048);
            effect.Validate();
            // The runtime reserves layer capacities in order; prevent later layers being starved.
            int requested = effect.Layers.Sum(l => l.MaxParticles);
            if (requested > effect.ParticleBudget)
            {
                effect.ParticleBudget = Math.Max(effect.ParticleBudget, effect.Layers.Count);
                int spare = effect.ParticleBudget - effect.Layers.Count;
                foreach (var layer in effect.Layers) layer.MaxParticles = 1 + (int)((long)spare * layer.MaxParticles / requested);
                Changes.Add("Balanced VFX layer capacities within particle budget.");
            }
            VfxEffectSerializer.Save(asset.Path, effect);
            FinishAsset(asset, AssetType.VfxEffect);
        }
        else if (kind == "create_mesh")
        {
            var asset = NewAsset(data, ".obj", files);
            var vertices = data.GetProperty("vertices"); var triangles = data.GetProperty("triangles");
            if (vertices.GetArrayLength() is < 3 or > 4096 || triangles.GetArrayLength() is < 1 or > 8192) throw new InvalidOperationException("Mesh budget: 3..4096 vertices and 1..8192 triangles.");
            var points = vertices.EnumerateArray().Select(Vec3).ToArray();
            var text = new System.Text.StringBuilder("# ByteEngine assistant native mesh\no Mesh\n");
            foreach (var v in points) text.AppendLine(FormattableString.Invariant($"v {v.X:R} {v.Y:R} {v.Z:R}"));
            int normalIndex = 0;
            foreach (var face in triangles.EnumerateArray())
            {
                var ids = face.EnumerateArray().Select(x => x.GetInt32()).ToArray();
                if (ids.Length != 3 || ids.Any(i => i < 0 || i >= points.Length)) throw new InvalidOperationException("Invalid triangle indices.");
                Vector3 normal = Vector3.Cross(points[ids[1]] - points[ids[0]], points[ids[2]] - points[ids[0]]);
                if (normal.LengthSquared() < 1e-12f) throw new InvalidOperationException("Degenerate triangle.");
                normal = Vector3.Normalize(normal); normalIndex++;
                text.AppendLine(FormattableString.Invariant($"vn {normal.X:R} {normal.Y:R} {normal.Z:R}"));
                text.AppendLine($"f {ids[0] + 1}//{normalIndex} {ids[1] + 1}//{normalIndex} {ids[2] + 1}//{normalIndex}");
            }
            File.WriteAllText(asset.Path, text.ToString()); FinishAsset(asset, AssetType.Model3D);
        }
        else
        {
            var asset = NewAsset(data, ".bmat", files);
            var material = new MaterialAsset { Name = Name(data, "name") };
            SetAssetProperties(material.Standard, data.GetProperty("properties"), []);
            MaterialAssetSerializer.Save(asset.Path, material);
            FinishAsset(asset, AssetType.Material);
        }
    }

    private GameObject InstantiateAsset(JsonElement data)
    {
        var project = EditorProjectContext.Active;
        if (project == null || Path.GetFullPath(project.ProjectRoot) != Path.GetFullPath(projectRoot)) throw new InvalidOperationException("Native asset placement requires the matching open editor project.");
        string path = Name(data, "path"); path = _assetAliases.GetValueOrDefault(path, path);
        string fullPath = Inside(path); project.AssetDatabase.RefreshPaths([fullPath]);
        var asset = project.AssetDatabase.Resolve(new AssetReference(path)) ?? throw new InvalidOperationException("Asset not found.");
        Vector3 position = data.TryGetProperty("position", out var v) ? Vec3(v) : Vector3.Zero;
        GameObject root = asset.Type switch
        {
            AssetType.Model3D => EditorSceneCommands.CreateModel(state, project, asset, position, new EditorLog()),
            AssetType.Blueprint => EditorSceneCommands.CreateBlueprintInstance(state, project, asset, position, new EditorLog()),
            _ => throw new InvalidOperationException("Place a Model3D or Blueprint asset.")
        };
        root.Name = Name(data, "name"); return root;
    }

    private void SetAssetProperties(object target, JsonElement data, string[] blocked, int depth = 0)
    {
        if (depth > 8) throw new InvalidOperationException("Profile nesting exceeds the supported depth.");
        foreach (var entry in data.EnumerateObject())
        {
            var property = target.GetType().GetProperties().SingleOrDefault(p => p.Name.Equals(entry.Name, StringComparison.OrdinalIgnoreCase) && p.SetMethod?.IsPublic == true);
            if (property == null || blocked.Contains(property.Name)) throw new InvalidOperationException("Unknown or unsupported asset property: " + entry.Name);
            object? value;
            if (property.PropertyType == typeof(VfxCurve))
            {
                if (entry.Value.ValueKind == JsonValueKind.Null) value = null;
                else
                {
                    var keys = entry.Value.GetProperty("keys");
                    if (keys.GetArrayLength() > 8) throw new InvalidOperationException("A curve supports at most 8 keys.");
                    value = new VfxCurve { Keys = keys.EnumerateArray().Select(k => new VfxCurveKey(k.GetProperty("time").GetSingle(), k.GetProperty("value").GetSingle())).ToList() };
                }
            }
            else if (Supported(property.PropertyType)) value = ConvertValue(entry.Value, property.PropertyType);
            else if (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(List<>) && (property.PropertyType.GetGenericArguments()[0].Namespace == "ByteEngine.Core.Animation" || Supported(property.PropertyType.GetGenericArguments()[0])))
            {
                if (entry.Value.ValueKind != JsonValueKind.Array || entry.Value.GetArrayLength() > 64) throw new InvalidOperationException("Profile lists support up to 64 items.");
                var items = (System.Collections.IList)Activator.CreateInstance(property.PropertyType)!;
                foreach (var element in entry.Value.EnumerateArray())
                {
                    var elementType = property.PropertyType.GetGenericArguments()[0];
                    if (Supported(elementType)) items.Add(ConvertValue(element, elementType));
                    else { var child = Activator.CreateInstance(elementType) ?? throw new InvalidOperationException("Unsupported profile list item."); SetAssetProperties(child, element, [], depth + 1); items.Add(child); }
                }
                value = items;
            }
            else if (property.PropertyType.Namespace == "ByteEngine.Core.Animation" && !property.PropertyType.IsValueType)
            {
                value = Activator.CreateInstance(property.PropertyType) ?? throw new InvalidOperationException("Unsupported profile section.");
                SetAssetProperties(value, entry.Value, [], depth + 1);
            }
            else throw new InvalidOperationException("Unsupported asset property: " + entry.Name);
            property.SetValue(target, value);
        }
    }

    private void SaveBlueprint(GameObject source, JsonElement data, List<string> files)
    {
        var asset = NewAsset(data, ".byteblueprint", files);
        BlueprintDefinition Build(SceneSerializer serializer)
        {
            var root = serializer.SerializeObject(source); root.ParentId = null; root.ParentSocket = ""; root.AttachmentOffset = null;
            root.Transform.LocalPosition = new Vector3Data();
            return new BlueprintDefinition { Name = Name(data, "name"), Root = root,
                Children = state.EditorScene.GameObjects.Where(o => o.IsDescendantOf(source)).Select(serializer.SerializeObject).ToList() };
        }
        BlueprintDefinition blueprint;
        if (EditorProjectContext.Active is {} project && Path.GetFullPath(project.ProjectRoot) == Path.GetFullPath(projectRoot)) blueprint = Build(project.Scenes);
        else
        {
            using var db = new AssetDatabase(projectRoot, [state.Project.AssetDirectory], readOnly: true);
            blueprint = Build(new SceneSerializer(new ComponentSerializer(projectRoot, db, new AssetManager(db))));
        }
        new BlueprintSerializer().Save(blueprint, asset.Path);
        FinishAsset(asset, AssetType.Blueprint);
    }
}
