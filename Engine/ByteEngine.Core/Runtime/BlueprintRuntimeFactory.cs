using System.Numerics;

using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.VisualLogic;

using RuntimeScene =
    ByteEngine.Core.Scene.Scene;

namespace ByteEngine.Core.Runtime;

/// <summary>
/// Instantiates Byte Blueprints directly into the live runtime scene.
///
/// This is deliberately separate from EditorSceneCommands because runtime
/// spawning must not touch editor selection, undo, or scene dirty state.
/// </summary>
public static class BlueprintRuntimeFactory
{
    public static GameObject? Spawn(
        RuntimeScene scene,
        AssetReference blueprintReference,
        Vector3 worldPosition,
        AssetDatabase database,
        ByteEngine.Core.Serialization.SceneSerializer scenes,
        Action<string>? warningSink)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        ArgumentNullException.ThrowIfNull(
            blueprintReference);

        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(scenes);

        AssetRecord? asset =
            database.Resolve(
                blueprintReference);

        if (asset ==
            null)
        {
            warningSink?.Invoke(
                $"Spawn Blueprint failed: asset '{blueprintReference}' could not be resolved.");

            return null;
        }

        if (asset.Type !=
            AssetType.Blueprint)
        {
            warningSink?.Invoke(
                $"Spawn Blueprint failed: '{asset.ProjectPath}' is not a Byte Blueprint.");

            return null;
        }

        BlueprintDefinition blueprint;

        try
        {
            blueprint =
                new BlueprintSerializer(database)
                    .Load(
                        asset.FullPath);
        }
        catch (Exception exception)
        {
            warningSink?.Invoke(
                $"Spawn Blueprint failed for '{asset.ProjectPath}': {exception.Message}");

            return null;
        }

        var sourceObjects =
            new List<GameObjectData>
            {
                blueprint.Root
            };

        sourceObjects.AddRange(
            blueprint.Children);

        IReadOnlyList<GameObject> roots;
        IReadOnlyDictionary<Guid, Guid> objectMap;

        try
        {
            roots =
                scenes.InstantiateHierarchy(
                    scene,
                    sourceObjects,
                    null,
                    out objectMap);
        }
        catch (Exception exception)
        {
            warningSink?.Invoke(
                $"Could not instantiate Blueprint '{asset.ProjectPath}': {exception.Message}");

            return null;
        }

        GameObject? root =
            roots.FirstOrDefault();

        if (root ==
            null)
        {
            warningSink?.Invoke(
                $"Blueprint '{asset.ProjectPath}' contains no root GameObject.");

            return null;
        }

        root.Transform.WorldPosition =
            worldPosition;

        foreach (VariableData variable
                 in blueprint.Variables)
        {
            root.Variables.Set(
                variable.Name,
                variable.Value.Clone());
        }

        BlueprintInstance? existingInstance =
            root.GetComponent<BlueprintInstance>();

        if (existingInstance ==
            null)
        {
            root.AddComponent(
                new BlueprintInstance
                {
                    Blueprint =
                        new AssetReference(
                            asset.Guid,
                            asset.ProjectPath),

                    InstanceId =
                        Guid.NewGuid()
                });
        }

        else
        {
            existingInstance.Blueprint =
                new AssetReference(
                    asset.Guid,
                    asset.ProjectPath);

            existingInstance.InstanceId =
                Guid.NewGuid();
        }

        BlueprintInstance instance = existingInstance ?? root.GetComponent<BlueprintInstance>()!;
        instance.SourceSnapshot = System.Text.Json.JsonSerializer.Serialize(sourceObjects, ByteEngine.Core.Serialization.JsonSerialization.Options);
        instance.ObjectMap.Clear();
        foreach (var pair in objectMap) instance.ObjectMap[pair.Key] = pair.Value;
        instance.ModifiedPropertyCount = instance.AddedComponentCount = instance.RemovedComponentCount =
            instance.AddedChildCount = instance.RemovedChildCount = 0;
        instance.LastPropagation = "Initialized from Blueprint source";

        AttachBlueprintEventModules(
            root,
            blueprint,
            database,
            warningSink);

        return root;
    }

    private static void AttachBlueprintEventModules(
        GameObject root,
        BlueprintDefinition blueprint,
        AssetDatabase database,
        Action<string>? warningSink)
    {
        if (blueprint.EventModules.Count ==
            0)
        {
            return;
        }

        EventModuleComponent runner =
            root.GetComponent<EventModuleComponent>()
            ?? root.AddComponent(
                new EventModuleComponent());

        var serializer =
            new EventModuleSerializer();

        foreach (Guid moduleGuid
                 in blueprint.EventModules)
        {
            if (moduleGuid ==
                Guid.Empty)
            {
                continue;
            }

            if (!database.TryGetAsset(
                    moduleGuid,
                    out AssetRecord? asset) ||
                asset ==
                    null)
            {
                runner.AddModuleReference(
                    new AssetReference(
                        moduleGuid));

                warningSink?.Invoke(
                    $"Blueprint '{blueprint.Name}' references missing Event Module {moduleGuid}.");

                continue;
            }

            var reference =
                new AssetReference(
                    asset.Guid,
                    asset.ProjectPath);

            runner.AddModuleReference(
                reference);

            if (asset.Type !=
                AssetType.EventModule)
            {
                warningSink?.Invoke(
                    $"Blueprint '{blueprint.Name}' references '{asset.ProjectPath}', but it is not an Event Module.");

                continue;
            }

            try
            {
                EventModuleDefinition definition =
                    serializer.Load(
                        asset.FullPath);

                runner.AddResolvedModule(
                    reference,
                    definition);
            }
            catch (Exception exception)
            {
                warningSink?.Invoke(
                    $"Could not load Event Module '{asset.ProjectPath}' for Blueprint '{blueprint.Name}': {exception.Message}");
            }
        }
    }
}
