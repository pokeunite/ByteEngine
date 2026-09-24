using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Editor;

namespace ByteEngine.Tests;

internal static class AuthoringUxTests
{
    public static void Run(string root, AssetDatabase database, AssetManager assets)
    {
        var scenes = new SceneSerializer(new ComponentSerializer(root, database, assets));
        string blueprintPath = Path.Combine(root, "Assets", "BP_Authoring.byteblueprint");
        BlueprintDefinition blueprint = CreateBlueprint(scenes);
        new BlueprintSerializer().Save(blueprint, blueprintPath);
        database.Scan();
        Assert(database.TryGetAsset("Assets/BP_Authoring.byteblueprint", out AssetRecord? asset) && asset != null,
            "Authoring test Blueprint is registered");

        var projectPath = Path.Combine(root, "AuthoringProject.byteproject");
        using EditorProjectContext project = EditorProjectContext.Create(projectPath, _ => { });
        string projectBlueprintPath = Path.Combine(project.ProjectRoot, "Assets", "BP_Authoring.byteblueprint");
        new BlueprintSerializer().Save(blueprint, projectBlueprintPath);
        project.AssetDatabase.Scan();
        Assert(project.AssetDatabase.TryGetAsset("Assets/BP_Authoring.byteblueprint", out AssetRecord? projectAsset) && projectAsset != null,
            "Project Blueprint is registered");

        var scene = new Scene("Blueprint Overrides");
        GameObject first = Place(scene, project, projectAsset!, blueprint);
        GameObject second = Place(scene, project, projectAsset!, blueprint);
        Guid firstInstanceId = first.GetComponent<BlueprintInstance>()!.InstanceId;
        Guid secondInstanceId = second.GetComponent<BlueprintInstance>()!.InstanceId;
        Scene persistedInstances = project.Scenes.CloneForRuntime(scene);
        Guid[] persistedIds = persistedInstances.GameObjects.Select(item => item.GetComponent<BlueprintInstance>())
            .Where(item => item != null).Select(item => item!.InstanceId).ToArray();
        Assert(persistedIds.Contains(firstInstanceId),
            $"Blueprint instance identity survives clone (expected {firstInstanceId}; actual {string.Join(", ", persistedIds)})");
        BlueprintInstance persisted = FindInstance(persistedInstances, firstInstanceId).GetComponent<BlueprintInstance>()!;
        Assert(!string.IsNullOrWhiteSpace(persisted.SourceSnapshot) && persisted.ObjectMap.Count == 1,
            "Blueprint baseline and stable identity map survive scene serialization");

        first.GetComponent<CharacterController3D>()!.MoveSpeed = 9f;
        blueprint.Root.Components.Single(item => item.Type == "CharacterController3D").Properties["moveSpeed"] = 7f;
        new BlueprintSerializer().Save(blueprint, projectAsset!.FullPath);
        project.AssetDatabase.Scan();
        BlueprintInstanceSynchronizer.Propagate(project, scene,
            new AssetReference(projectAsset.Guid, projectAsset.ProjectPath));
        first = FindInstance(scene, firstInstanceId);
        second = FindInstance(scene, secondInstanceId);
        Assert(Near(first.GetComponent<CharacterController3D>()!.MoveSpeed, 9f),
            "Property override survives Blueprint source propagation");
        Assert(Near(second.GetComponent<CharacterController3D>()!.MoveSpeed, 7f),
            "Non-overridden property receives Blueprint source change");
        Assert(first.GetComponent<BlueprintInstance>()!.ModifiedPropertyCount > 0,
            "Modified Blueprint property is tracked as an override");

        first = BlueprintInstanceSynchronizer.Revert(first, project)!;
        Assert(Near(first.GetComponent<CharacterController3D>()!.MoveSpeed, 7f),
            "Revert restores current Blueprint property value");
        first.RemoveComponent(first.GetComponent<CharacterController3D>()!);
        Assert(BlueprintInstanceSynchronizer.Analyze(first, project).RemovedComponents == 1,
            "Removed inherited component is tracked as an override");
        first = BlueprintInstanceSynchronizer.Revert(first, project)!;
        Assert(first.GetComponent<CharacterController3D>() != null, "Revert restores a removed inherited component");
        GameObject addedChild = scene.CreateGameObject("Instance Child");
        addedChild.SetParent(first, false);
        Assert(BlueprintInstanceSynchronizer.Analyze(first, project).AddedChildren == 1,
            "Instance-added child is tracked as an override");
        first = BlueprintInstanceSynchronizer.Revert(first, project)!;
        Assert(first.Children.All(item => item.Name != "Instance Child"), "Revert removes an instance-added child");
        first.GetComponent<CharacterController3D>()!.MoveSpeed = 8f;
        first = BlueprintInstanceSynchronizer.Apply(first, project)!;
        second = FindInstance(scene, secondInstanceId);
        BlueprintDefinition applied = new BlueprintSerializer().Load(projectAsset.FullPath);
        Assert(Near(applied.Root.Components.Single(item => item.Type == "CharacterController3D").Properties["moveSpeed"]!.GetValue<float>(), 8f) &&
            Near(second.GetComponent<CharacterController3D>()!.MoveSpeed, 8f),
            "Apply updates Blueprint source and other inherited instances");

        first.AddComponent(new HealthComponent());
        Assert(BlueprintInstanceSynchronizer.Analyze(first, project).AddedComponents == 1,
            "Added component is tracked as an override");
        first = BlueprintInstanceSynchronizer.Revert(first, project)!;
        Assert(first.GetComponent<HealthComponent>() == null, "Revert removes an instance-added component");
        first.AddComponent(new HealthComponent());
        first = BlueprintInstanceSynchronizer.Apply(first, project)!;
        applied = new BlueprintSerializer().Load(projectAsset.FullPath);
        Assert(applied.Root.Components.Any(item => item.Type == "HealthComponent"),
            "Applying an added component adds it to Blueprint source");

        var staleScene = new Scene("Stale Blueprint Instance");
        _ = Place(staleScene, project, projectAsset, applied);

        GameObjectData child = NewObject("Source Child", applied.Root.Id);
        applied.Children.Add(child);
        new BlueprintSerializer().Save(applied, projectAsset.FullPath);
        project.AssetDatabase.Scan();
        Assert(BlueprintInstanceSynchronizer.RefreshOutdated(project, staleScene) == 1 &&
            staleScene.GameObjects.Any(item => item.Name == "Source Child"),
            "Scene load refreshes stale Blueprint instance children from the saved source");
        BlueprintInstanceSynchronizer.Propagate(project, scene, new AssetReference(projectAsset.Guid, projectAsset.ProjectPath));
        first = FindInstance(scene, firstInstanceId);
        second = FindInstance(scene, secondInstanceId);
        Assert(first.Children.Any(item => item.Name == "Source Child") && second.Children.Any(item => item.Name == "Source Child"),
            "Saved Blueprint child propagates to live instances");

        TestVisualTransform(project, applied);
        TestVisualModelOverride(project);
        TestModelGrounding(project);
        TestPresetAndDependencies();
    }

    private static BlueprintDefinition CreateBlueprint(SceneSerializer serializer)
    {
        var scene = new Scene("BP Source");
        GameObject root = scene.CreateGameObject("Player");
        root.AddComponent(new CharacterController3D { MoveSpeed = 5f });
        SceneData data = serializer.Serialize(scene);
        return new BlueprintDefinition { Name = "BP_Authoring", Type = BlueprintType.Character, Root = data.GameObjects[0] };
    }

    private static GameObject Place(Scene scene, EditorProjectContext project, AssetRecord asset, BlueprintDefinition blueprint)
    {
        List<GameObjectData> source = new() { blueprint.Root };
        source.AddRange(blueprint.Children);
        GameObject root = project.Scenes.InstantiateHierarchy(scene, source, null,
            out IReadOnlyDictionary<Guid, Guid> map).Single();
        var instance = root.AddComponent(new BlueprintInstance
        {
            Blueprint = new AssetReference(asset.Guid, asset.ProjectPath),
            InstanceId = Guid.NewGuid()
        });
        BlueprintInstanceSynchronizer.Initialize(instance, blueprint, map);
        return root;
    }

    private static void TestVisualTransform(EditorProjectContext project, BlueprintDefinition source)
    {
        var scene = new Scene("Legacy Character Migration");
        GameObject player = scene.CreateGameObject("Player");
        GameObject visual = scene.CreateGameObject("Visual");
        visual.SetParent(player, false);
        visual.Transform.EulerAngles = new Vector3(0f, 180f, 0f);
        visual.Transform.LocalScale = new Vector3(1.5f);
        GameObject imported = scene.CreateGameObject("Imported Model");
        imported.SetParent(visual, false);
        imported.AddComponent(new ModelHierarchyInstance { AppliedImportScale = 1f });
        imported.Transform.LocalPosition = new Vector3(0.2f, 0.3f, 0.4f);
        Vector3 position = imported.Transform.WorldPosition;
        Quaternion rotation = imported.Transform.WorldRotation;
        Vector3 scale = imported.Transform.WorldScale;

        GameObject model = BlueprintAuthoringService.NormalizeCharacterStructure(player);
        Assert(model.Name == "Model" && ReferenceEquals(model.Parent, player) &&
            player.Children.All(child => child.Name != "Visual"), "Legacy Visual wrapper flattens into Model");
        Assert(Vector3.Distance(model.Transform.WorldPosition, position) < 0.001f &&
            Vector3.Distance(model.Transform.WorldScale, scale) < 0.001f &&
            MathF.Abs(Quaternion.Dot(model.Transform.WorldRotation, rotation)) > 0.999f,
            "Legacy model world pose is preserved");
        BlueprintAuthoringService.NormalizeCharacterStructure(player);
        Assert(player.Children.Count(child => child.Name == "Model") == 1 &&
            Vector3.Distance(model.Transform.WorldScale, scale) < 0.001f,
            "Character normalization is idempotent");

        SceneData serialized = project.Scenes.Serialize(scene);
        var blueprint = new BlueprintDefinition
        {
            Name = "BP_Model",
            Type = BlueprintType.Character,
            Root = serialized.GameObjects.Single(item => item.Id == player.Id),
            Children = serialized.GameObjects.Where(item => item.Id != player.Id).ToList()
        };
        string path = Path.Combine(project.ProjectRoot, "Assets", "BP_Model.byteblueprint");
        var serializer = new BlueprintSerializer();
        serializer.Save(blueprint, path);
        BlueprintDefinition loaded = serializer.Load(path);
        Assert(loaded.Children.Any(item => item.Name == "Model" && item.ParentId == player.Id) &&
            loaded.Children.All(item => item.Name != "Visual"), "Migrated Model persists in Blueprint");

        var customScene = new Scene("Custom Legacy Visual");
        GameObject customRoot = customScene.CreateGameObject("Player");
        GameObject customVisual = customScene.CreateGameObject("Visual");
        customVisual.SetParent(customRoot, false);
        customVisual.AddComponent(new HealthComponent());
        GameObject customModel = customScene.CreateGameObject("Model");
        customModel.SetParent(customVisual, false);
        customModel.AddComponent(new ModelHierarchyInstance());
        bool refusedUnsafeMigration = false;
        try { BlueprintAuthoringService.NormalizeCharacterStructure(customRoot); }
        catch (InvalidOperationException) { refusedUnsafeMigration = true; }
        Assert(refusedUnsafeMigration &&
            ReferenceEquals(customModel.Parent, customVisual) &&
            customVisual.GetComponent<HealthComponent>() != null,
            "Custom Visual components are never silently discarded by migration");
    }

    private static void TestVisualModelOverride(EditorProjectContext project)
    {
        var scene = new Scene("Model Override");
        GameObject player = scene.CreateGameObject("Player");
        GameObject model = BlueprintAuthoringService.EnsureModelRoot(player);
        model.AddComponent(new ModelHierarchyInstance { AppliedImportScale = 1f });

        var modelOverride = BlueprintAuthoringService.AddComponent(player, new VisualModelOverride());
        Assert(ReferenceEquals(modelOverride.GameObject, model) &&
            Near(modelOverride.ScaleMultiplier.X, 1f) &&
            player.Transform.LocalScale == Vector3.One,
            "Root Add Component routes Model visual override to Model");
        modelOverride.RotationDegrees = new Vector3(0f, 180f, 0f);
        modelOverride.ScaleMultiplier = Vector3.One * 1.5f;
        Assert(Near(model.Transform.LocalScale.X, 1.5f) &&
            Vector3.Transform(Vector3.UnitZ, model.Transform.LocalRotation).Z < -.99f,
            "Model override edits only the Model transform");

        Scene restored = project.Scenes.CloneForRuntime(scene);
        GameObject restoredModel = restored.FindGameObject("Model")!;
        VisualModelOverride restoredOverride = restoredModel.GetComponent<VisualModelOverride>()!;
        Assert(Near(restoredOverride.ImportScale, 1f) &&
            Near(restoredOverride.ScaleMultiplier.X, 1.5f) &&
            restored.FindGameObject("Player")!.Transform.LocalScale == Vector3.One,
            "Model override survives scene serialization");
    }

    private static void TestModelGrounding(EditorProjectContext project)
    {
        string path = Path.Combine(project.ProjectRoot, "Assets", "GroundProbe.obj");
        File.WriteAllText(path,
            "v 0 -0.25 0\nv 0 1.25 0\nv 0.2 1.25 0\nf 1 2 3\n");
        project.AssetDatabase.Scan();
        Assert(project.AssetDatabase.TryGetAsset("Assets/GroundProbe.obj", out AssetRecord? asset) &&
            asset != null, "Grounding fixture is registered");
        var scene = new Scene("Grounding");
        GameObject root = scene.CreateGameObject("Character");
        GameObject model = BlueprintAuthoringService.EnsureModelRoot(root);
        model.AddComponent(new ModelHierarchyInstance
        {
            Model = new AssetReference(asset!.Guid, asset.ProjectPath)
        });
        Assert(BlueprintAuthoringService.GroundModelAtFeet(model, project.Assets) &&
            Near(model.Transform.LocalPosition.Y, 0.25f),
            "Model-only offset puts authored feet at root plane");
        Assert(!BlueprintAuthoringService.GroundModelAtFeet(model, project.Assets) &&
            Near(model.Transform.LocalPosition.Y, 0.25f),
            "Grounding does not accumulate on repeated setup");
        Scene restored = project.Scenes.CloneForRuntime(scene);
        ModelHierarchyInstance restoredMarker =
            restored.FindGameObject("Model")!.GetComponent<ModelHierarchyInstance>()!;
        Assert(restoredMarker.AutoGrounded &&
            Near(restored.FindGameObject("Model")!.Transform.LocalPosition.Y, 0.25f),
            "Grounding offset and one-time marker survive save/load");
    }
    private static void TestPresetAndDependencies()
    {
        var scene = new Scene("Preset");
        GameObject player = scene.CreateGameObject("Player");
        BlueprintAuthoringService.AddComponent(player, new PlayerController3D());
        Assert(player.GetComponent<CharacterController3D>() != null,
            "Adding Player Input automatically adds Character Movement dependency");
        BlueprintAuthoringService.SetupThirdPersonCharacter(player);
        Assert(player.Children.Count(child => child.Name == "Model") == 1 &&
            player.Children.All(child => child.Name != "Visual") &&
            player.Transform.LocalScale == Vector3.One &&
            MathF.Abs(player.Transform.EulerAngles.Y) < 0.001f,
            "New Character preset has one direct Model child and a unit gameplay root");
        GameObject camera = player.Children.Single(item => item.GetComponent<Camera3D>() != null);
        Assert(player.GetComponent<CapsuleCollider3D>() != null && player.GetComponent<CharacterController3D>() != null &&
            player.GetComponent<PlayerController3D>() != null && player.GetComponent<CameraBoom3D>() != null &&
            camera.GetComponent<Camera3D>() != null && ReferenceEquals(scene.ActiveCamera, camera.GetComponent<Camera3D>()),
            "Third Person Character preset creates movement, input, collision, boom, child camera and ownership");
    }

    private static GameObjectData NewObject(string name, Guid parentId) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        ParentId = parentId,
        Transform = new TransformData
        {
            LocalPosition = new Vector3Data(),
            LocalRotation = new QuaternionData(),
            LocalScale = new Vector3Data { X = 1f, Y = 1f, Z = 1f }
        }
    };
    private static GameObject FindInstance(Scene scene, Guid instanceId) => scene.GameObjects
        .Single(item => item.GetComponent<BlueprintInstance>()?.InstanceId == instanceId);
    private static bool Near(float value, float expected) => Math.Abs(value - expected) < .001f;
    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + name);
    }
}
