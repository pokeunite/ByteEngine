using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.VisualLogic;

namespace ByteEngine.Editor;

internal sealed record BlueprintPromotionResult(
    AssetRecord Asset,
    BlueprintDefinition Blueprint,
    BlueprintInstance Instance);

/// <summary>
/// Promotes a materialized scene hierarchy into a Blueprint while preserving
/// the existing hierarchy as the first live instance of that asset.
/// </summary>
internal static class BlueprintPromotionService
{
    public static BlueprintPromotionResult Promote(
        EditorProjectContext project,
        GameObject selected,
        string path)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(selected);
        if (selected.Scene == null) throw new InvalidOperationException("The selected object is not in a scene.");
        if (selected.GetComponent<BlueprintInstance>() != null)
            throw new InvalidOperationException("The selected object is already a Blueprint instance. Unpack it before creating a new Blueprint.");

        SceneData serialized = project.Scenes.Serialize(selected.Scene);
        HashSet<Guid> hierarchyIds = Descendants(selected).Select(item => item.Id).Append(selected.Id).ToHashSet();
        GameObjectData root = serialized.GameObjects.FirstOrDefault(item => item.Id == selected.Id)
            ?? throw new InvalidOperationException($"Could not serialize selected GameObject '{selected.Name}'.");
        root.ParentId = null;

        List<GameObjectData> children = serialized.GameObjects
            .Where(item => item.Id != selected.Id && hierarchyIds.Contains(item.Id))
            .ToList();
        foreach (GameObjectData item in children.Prepend(root))
            item.Components.RemoveAll(component => component.Type.Equals(nameof(BlueprintInstance), StringComparison.OrdinalIgnoreCase));

        var blueprint = new BlueprintDefinition
        {
            Name = Path.GetFileNameWithoutExtension(path),
            Type = selected.GetComponent<CharacterController3D>() != null
                ? BlueprintType.Character
                : BlueprintType.GenericObject,
            Root = root,
            Children = children,
            Variables = root.Variables.Select(variable => new VariableData
            {
                Name = variable.Name,
                Value = variable.Value.Clone()
            }).ToList(),
            EventModules = selected.GetComponent<EventModuleComponent>()?.Modules
                .Where(reference => reference.Guid != Guid.Empty)
                .Select(reference => reference.Guid)
                .Distinct()
                .ToList() ?? new List<Guid>()
        };

        new BlueprintSerializer().Save(blueprint, path);
        project.AssetDatabase.Scan();
        string projectPath = Path.GetRelativePath(project.ProjectRoot, path)
            .Replace(Path.DirectorySeparatorChar, '/');
        if (!project.AssetDatabase.TryGetAsset(projectPath, out AssetRecord? asset) || asset == null)
            throw new InvalidOperationException("The new Blueprint was saved but could not be registered.");

        var instance = new BlueprintInstance
        {
            Blueprint = new AssetReference(asset.Guid, asset.ProjectPath),
            InstanceId = Guid.NewGuid()
        };
        selected.AddComponent(instance);
        BlueprintInstanceSynchronizer.Initialize(
            instance,
            blueprint,
            hierarchyIds.ToDictionary(id => id, id => id));
        instance.LastPropagation = "Promoted existing scene object to Blueprint instance";
        return new BlueprintPromotionResult(asset, blueprint, instance);
    }

    public static int CountInstances(Scene scene, AssetReference reference) =>
        scene.GameObjects.Count(item =>
            item.GetComponent<BlueprintInstance>() is { } instance &&
            Matches(instance.Blueprint, reference));

    public static int UnpackInstances(Scene scene, AssetReference reference)
    {
        int count = 0;
        foreach (GameObject root in scene.GameObjects.ToArray())
        {
            BlueprintInstance? instance = root.GetComponent<BlueprintInstance>();
            if (instance == null || !Matches(instance.Blueprint, reference)) continue;
            instance.Blueprint = AssetReference.Empty;
            instance.SourceSnapshot = string.Empty;
            instance.ObjectMap.Clear();
            instance.InstanceId = Guid.Empty;
            instance.ModifiedPropertyCount = instance.AddedComponentCount = instance.RemovedComponentCount = 0;
            instance.AddedChildCount = instance.RemovedChildCount = 0;
            instance.LastPropagation = "Unpacked";
            root.RemoveComponent(instance);
            count++;
        }
        return count;
    }

    private static IEnumerable<GameObject> Descendants(GameObject root)
    {
        foreach (GameObject child in root.Children)
        {
            yield return child;
            foreach (GameObject descendant in Descendants(child)) yield return descendant;
        }
    }

    private static bool Matches(AssetReference left, AssetReference right) =>
        left.Guid != Guid.Empty && right.Guid != Guid.Empty ? left.Guid == right.Guid :
        !string.IsNullOrWhiteSpace(left.CachedProjectPath) &&
        string.Equals(left.CachedProjectPath, right.CachedProjectPath, StringComparison.OrdinalIgnoreCase);
}
