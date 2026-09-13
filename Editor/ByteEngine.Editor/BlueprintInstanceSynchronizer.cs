using System.Text.Json;
using System.Text.Json.Nodes;
using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;

namespace ByteEngine.Editor;

using RuntimeScene = ByteEngine.Core.Scene.Scene;

internal readonly record struct BlueprintOverrideSummary(
    int ModifiedProperties,
    int AddedComponents,
    int RemovedComponents,
    int AddedChildren,
    int RemovedChildren)
{
    public int Total => ModifiedProperties + AddedComponents + RemovedComponents + AddedChildren + RemovedChildren;
}

internal static class BlueprintInstanceSynchronizer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        IncludeFields = true
    };

    public static void Initialize(BlueprintInstance instance, BlueprintDefinition blueprint,
        IReadOnlyDictionary<Guid, Guid> objectMap)
    {
        instance.SourceSnapshot = SerializeObjects(Objects(blueprint));
        instance.ObjectMap.Clear();
        foreach ((Guid source, Guid placed) in objectMap) instance.ObjectMap[source] = placed;
        SetSummary(instance, default);
        instance.LastPropagation = "Initialized from Blueprint source";
    }

    public static BlueprintOverrideSummary Analyze(GameObject root, EditorProjectContext project)
    {
        BlueprintInstance? instance = root.GetComponent<BlueprintInstance>();
        if (instance == null || !TryLoadBlueprint(project, instance, out BlueprintDefinition? latest, out _)) return default;
        EnsureLegacyBaseline(root, instance, latest!);
        List<GameObjectData> baseline = DeserializeObjects(instance.SourceSnapshot);
        if (baseline.Count == 0) baseline = Objects(latest!);
        List<GameObjectData> current = Capture(root, project, instance, baseline);
        _ = Merge(baseline, current, baseline, out BlueprintOverrideSummary summary);
        SetSummary(instance, summary);
        return summary;
    }

    public static GameObject? Revert(GameObject root, EditorProjectContext project)
    {
        BlueprintInstance? instance = root.GetComponent<BlueprintInstance>();
        if (instance == null || !TryLoadBlueprint(project, instance, out BlueprintDefinition? latest, out AssetRecord? asset)) return null;
        EnsureLegacyBaseline(root, instance, latest!);
        return Replace(root, project, instance, latest!, Objects(latest!), default,
            new AssetReference(asset!.Guid, asset.ProjectPath), "Reverted to current Blueprint source");
    }

    public static GameObject? Apply(GameObject root, EditorProjectContext project)
    {
        BlueprintInstance? instance = root.GetComponent<BlueprintInstance>();
        if (instance == null || !TryLoadBlueprint(project, instance, out BlueprintDefinition? latest, out AssetRecord? asset)) return null;
        EnsureLegacyBaseline(root, instance, latest!);
        List<GameObjectData> baseline = DeserializeObjects(instance.SourceSnapshot);
        if (baseline.Count == 0) baseline = Objects(latest!);
        List<GameObjectData> current = Capture(root, project, instance, baseline);
        GameObjectData? currentRoot = current.FirstOrDefault(item => item.ParentId == null);
        if (currentRoot == null) return null;

        latest!.Root = Clone(currentRoot);
        latest.Root.ParentId = null;
        latest.Children = current.Where(item => item.Id != currentRoot.Id).Select(Clone).ToList();
        new BlueprintSerializer().Save(latest, asset!.FullPath);
        project.AssetDatabase.Scan();

        Guid appliedInstanceId = instance.InstanceId;
        RuntimeScene scene = root.Scene!;
        Propagate(project, scene, new AssetReference(asset.Guid, asset.ProjectPath));
        return scene.GameObjects.FirstOrDefault(item => item.GetComponent<BlueprintInstance>()?.InstanceId == appliedInstanceId);
    }

    public static void Propagate(EditorProjectContext project, RuntimeScene scene, AssetReference blueprintReference)
    {
        GameObject[] roots = scene.GameObjects
            .Where(item => item.GetComponent<BlueprintInstance>() is { } instance && Matches(instance.Blueprint, blueprintReference))
            .ToArray();
        foreach (GameObject root in roots) Refresh(root, project);
    }

    private static GameObject? Refresh(GameObject root, EditorProjectContext project)
    {
        BlueprintInstance? instance = root.GetComponent<BlueprintInstance>();
        if (instance == null || !TryLoadBlueprint(project, instance, out BlueprintDefinition? latest, out AssetRecord? asset)) return null;
        List<GameObjectData> baseline = DeserializeObjects(instance.SourceSnapshot);
        if (baseline.Count == 0) baseline = Objects(latest!);
        List<GameObjectData> current = Capture(root, project, instance, baseline);
        List<GameObjectData> merged = Merge(baseline, current, Objects(latest!), out BlueprintOverrideSummary summary);
        return Replace(root, project, instance, latest!, merged, summary,
            new AssetReference(asset!.Guid, asset.ProjectPath), "Updated from Blueprint; instance overrides preserved");
    }

    private static GameObject? Replace(GameObject oldRoot, EditorProjectContext project, BlueprintInstance oldInstance,
        BlueprintDefinition latest, List<GameObjectData> merged, BlueprintOverrideSummary summary,
        AssetReference reference, string status)
    {
        RuntimeScene? scene = oldRoot.Scene;
        if (scene == null) return null;
        Guid instanceId = oldInstance.InstanceId;
        Vector3 position = oldRoot.Transform.WorldPosition;
        Quaternion rotation = oldRoot.Transform.WorldRotation;
        Vector3 scale = oldRoot.Transform.WorldScale;
        Dictionary<Guid, Guid> preferred = new(oldInstance.ObjectMap);
        foreach (GameObjectData item in merged)
            if (!preferred.ContainsKey(item.Id)) preferred[item.Id] = item.Id;

        scene.DestroyGameObject(oldRoot);
        IReadOnlyList<GameObject> roots = project.Scenes.InstantiateHierarchy(scene, merged, preferred, out IReadOnlyDictionary<Guid, Guid> map);
        GameObject? newRoot = roots.FirstOrDefault();
        if (newRoot == null) return null;
        newRoot.Transform.WorldPosition = position;
        newRoot.Transform.WorldRotation = rotation;
        newRoot.Transform.WorldScale = scale;
        var newInstance = new BlueprintInstance { Blueprint = reference, InstanceId = instanceId };
        newRoot.AddComponent(newInstance);
        Initialize(newInstance, latest, map);
        SetSummary(newInstance, summary);
        newInstance.LastPropagation = status;
        return newRoot;
    }

    private static List<GameObjectData> Capture(GameObject root, EditorProjectContext project,
        BlueprintInstance instance, IReadOnlyList<GameObjectData> baseline)
    {
        HashSet<Guid> subtree = Descendants(root).Select(item => item.Id).Append(root.Id).ToHashSet();
        Dictionary<Guid, Guid> reverse = instance.ObjectMap.ToDictionary(pair => pair.Value, pair => pair.Key);
        List<GameObjectData> result = project.Scenes.Serialize(root.Scene!).GameObjects
            .Where(item => subtree.Contains(item.Id)).Select(Clone).ToList();
        foreach (GameObjectData item in result)
        {
            Guid instanceId = item.Id;
            item.Id = reverse.GetValueOrDefault(instanceId, instanceId);
            if (item.ParentId.HasValue) item.ParentId = reverse.GetValueOrDefault(item.ParentId.Value, item.ParentId.Value);
            item.Components.RemoveAll(component => component.Type.Equals("BlueprintInstance", StringComparison.OrdinalIgnoreCase));
        }

        GameObjectData? rootData = result.FirstOrDefault(item => item.ParentId == null);
        GameObjectData? baselineRoot = baseline.FirstOrDefault(item => item.ParentId == null);
        if (rootData != null && baselineRoot != null) rootData.Transform = Clone(baselineRoot.Transform);
        return result;
    }

    private static List<GameObjectData> Merge(IReadOnlyList<GameObjectData> baseline,
        IReadOnlyList<GameObjectData> current, IReadOnlyList<GameObjectData> latest,
        out BlueprintOverrideSummary summary)
    {
        var counts = new Counts();
        Dictionary<Guid, GameObjectData> oldById = baseline.ToDictionary(item => item.Id);
        Dictionary<Guid, GameObjectData> currentById = current.ToDictionary(item => item.Id);
        Dictionary<Guid, GameObjectData> latestById = latest.ToDictionary(item => item.Id);
        var result = new List<GameObjectData>();

        foreach (GameObjectData source in latest)
        {
            if (!oldById.TryGetValue(source.Id, out GameObjectData? old))
            {
                result.Add(Clone(source));
                continue;
            }
            if (!currentById.TryGetValue(source.Id, out GameObjectData? placed))
            {
                if (old.ParentId != null) counts.RemovedChildren++;
                continue;
            }
            result.Add(MergeObject(old, placed, source, counts));
        }

        foreach (GameObjectData added in current.Where(item => !oldById.ContainsKey(item.Id) && !latestById.ContainsKey(item.Id)))
        {
            counts.AddedChildren++;
            result.Add(Clone(added));
        }

        summary = new BlueprintOverrideSummary(counts.ModifiedProperties, counts.AddedComponents,
            counts.RemovedComponents, counts.AddedChildren, counts.RemovedChildren);
        return result;
    }

    private static GameObjectData MergeObject(GameObjectData old, GameObjectData current,
        GameObjectData latest, Counts counts)
    {
        var merged = Clone(latest);
        merged.Name = Choose(old.Name, current.Name, latest.Name, counts);
        merged.Active = Choose(old.Active, current.Active, latest.Active, counts);
        merged.ParentId = Choose(old.ParentId, current.ParentId, latest.ParentId, counts);
        merged.Transform = MergeValue(old.Transform, current.Transform, latest.Transform, counts);
        merged.Variables = MergeValue(old.Variables, current.Variables, latest.Variables, counts);
        merged.Components.Clear();

        Dictionary<string, ComponentData> oldComponents = old.Components.ToDictionary(item => item.Type, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, ComponentData> currentComponents = current.Components.ToDictionary(item => item.Type, StringComparer.OrdinalIgnoreCase);
        foreach (ComponentData source in latest.Components)
        {
            if (!oldComponents.TryGetValue(source.Type, out ComponentData? oldComponent))
            {
                merged.Components.Add(Clone(source));
                continue;
            }
            if (!currentComponents.TryGetValue(source.Type, out ComponentData? placedComponent))
            {
                counts.RemovedComponents++;
                continue;
            }
            merged.Components.Add(new ComponentData
            {
                Type = source.Type,
                Enabled = Choose(oldComponent.Enabled, placedComponent.Enabled, source.Enabled, counts),
                Properties = MergeProperties(oldComponent.Properties, placedComponent.Properties, source.Properties, counts)
            });
        }
        HashSet<string> latestComponentTypes = latest.Components.Select(item => item.Type)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (ComponentData added in current.Components.Where(item =>
                     !oldComponents.ContainsKey(item.Type) && !latestComponentTypes.Contains(item.Type)))
        {
            counts.AddedComponents++;
            merged.Components.Add(Clone(added));
        }
        return merged;
    }

    private static JsonObject MergeProperties(JsonObject old, JsonObject current, JsonObject latest, Counts counts)
    {
        var merged = new JsonObject();
        foreach (string key in latest.Select(pair => pair.Key).Union(current.Select(pair => pair.Key), StringComparer.OrdinalIgnoreCase))
        {
            old.TryGetPropertyValue(key, out JsonNode? oldValue);
            current.TryGetPropertyValue(key, out JsonNode? currentValue);
            latest.TryGetPropertyValue(key, out JsonNode? latestValue);
            if (!old.ContainsKey(key))
            {
                if (current.ContainsKey(key)) { counts.ModifiedProperties++; merged[key] = currentValue?.DeepClone(); }
                else merged[key] = latestValue?.DeepClone();
            }
            else if (!current.ContainsKey(key)) counts.ModifiedProperties++;
            else if (JsonNode.DeepEquals(oldValue, currentValue)) merged[key] = latestValue?.DeepClone();
            else if (oldValue is JsonObject oldObject && currentValue is JsonObject currentObject && latestValue is JsonObject latestObject)
                merged[key] = MergeProperties(oldObject, currentObject, latestObject, counts);
            else { counts.ModifiedProperties++; merged[key] = currentValue?.DeepClone(); }
        }
        return merged;
    }

    private static T Choose<T>(T old, T current, T latest, Counts counts)
    {
        if (EqualityComparer<T>.Default.Equals(old, current)) return latest;
        counts.ModifiedProperties++;
        return current;
    }

    private static T MergeValue<T>(T old, T current, T latest, Counts counts)
    {
        JsonNode? oldNode = JsonSerializer.SerializeToNode(old, JsonOptions);
        JsonNode? currentNode = JsonSerializer.SerializeToNode(current, JsonOptions);
        if (JsonNode.DeepEquals(oldNode, currentNode)) return Clone(latest);
        counts.ModifiedProperties++;
        return Clone(current);
    }

    private static IEnumerable<GameObject> Descendants(GameObject root)
    {
        foreach (GameObject child in root.Children)
        {
            yield return child;
            foreach (GameObject descendant in Descendants(child)) yield return descendant;
        }
    }

    private static bool TryLoadBlueprint(EditorProjectContext project, BlueprintInstance instance,
        out BlueprintDefinition? blueprint, out AssetRecord? asset)
    {
        asset = project.AssetDatabase.Resolve(instance.Blueprint);
        if (asset == null || asset.Type != AssetType.Blueprint)
        {
            blueprint = null;
            instance.LastPropagation = "Failed: Blueprint asset could not be resolved";
            return false;
        }
        blueprint = new BlueprintSerializer().Load(asset.FullPath);
        return true;
    }

    private static void EnsureLegacyBaseline(GameObject root, BlueprintInstance instance, BlueprintDefinition blueprint)
    {
        if (!string.IsNullOrWhiteSpace(instance.SourceSnapshot) && instance.ObjectMap.Count > 0) return;
        List<GameObjectData> source = Objects(blueprint);
        instance.SourceSnapshot = SerializeObjects(source);
        instance.ObjectMap.Clear();
        GameObjectData sourceRoot = source.First(item => item.ParentId == null);
        instance.ObjectMap[sourceRoot.Id] = root.Id;
        var placedByParent = Descendants(root).GroupBy(item => item.Parent!.Id)
            .ToDictionary(group => group.Key, group => group.ToList());
        bool progress;
        do
        {
            progress = false;
            foreach (GameObjectData child in source.Where(item => item.ParentId.HasValue && !instance.ObjectMap.ContainsKey(item.Id)))
            {
                if (!instance.ObjectMap.TryGetValue(child.ParentId!.Value, out Guid placedParentId) ||
                    !placedByParent.TryGetValue(placedParentId, out List<GameObject>? candidates)) continue;
                HashSet<Guid> used = instance.ObjectMap.Values.ToHashSet();
                GameObject? match = candidates.FirstOrDefault(item => !used.Contains(item.Id) &&
                    item.Name.Equals(child.Name, StringComparison.OrdinalIgnoreCase)) ??
                    candidates.FirstOrDefault(item => !used.Contains(item.Id));
                if (match == null) continue;
                instance.ObjectMap[child.Id] = match.Id;
                progress = true;
            }
        } while (progress);
        instance.LastPropagation = "Legacy instance baseline initialized";
    }

    private static bool Matches(AssetReference left, AssetReference right) =>
        left.Guid != Guid.Empty && right.Guid != Guid.Empty ? left.Guid == right.Guid :
        !string.IsNullOrWhiteSpace(left.CachedProjectPath) &&
        string.Equals(left.CachedProjectPath, right.CachedProjectPath, StringComparison.OrdinalIgnoreCase);
    private static List<GameObjectData> Objects(BlueprintDefinition blueprint) =>
        new[] { Clone(blueprint.Root) }.Concat(blueprint.Children.Select(Clone)).ToList();
    private static string SerializeObjects(IReadOnlyList<GameObjectData> objects) => JsonSerializer.Serialize(objects, JsonOptions);
    private static List<GameObjectData> DeserializeObjects(string json) => string.IsNullOrWhiteSpace(json)
        ? new List<GameObjectData>()
        : JsonSerializer.Deserialize<List<GameObjectData>>(json, JsonOptions) ?? new List<GameObjectData>();
    private static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)!;
    private static void SetSummary(BlueprintInstance instance, BlueprintOverrideSummary summary)
    {
        instance.ModifiedPropertyCount = summary.ModifiedProperties;
        instance.AddedComponentCount = summary.AddedComponents;
        instance.RemovedComponentCount = summary.RemovedComponents;
        instance.AddedChildCount = summary.AddedChildren;
        instance.RemovedChildCount = summary.RemovedChildren;
    }

    private sealed class Counts
    {
        public int ModifiedProperties;
        public int AddedComponents;
        public int RemovedComponents;
        public int AddedChildren;
        public int RemovedChildren;
    }
}
