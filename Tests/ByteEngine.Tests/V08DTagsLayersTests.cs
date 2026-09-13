using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Classification;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Editor;

namespace ByteEngine.Tests;

internal static class V08DTagsLayersTests
{
    public static void Run(string root, AssetDatabase database, AssetManager assets)
    {
        TestDefinitionsAndMasks();
        TestObjectsIndexesFilteringAndSerialization(root, database, assets);
        TestProjectPersistence(root);
        TestByteGraph();
        TestBlueprintOverrides(root);
    }

    private static void TestDefinitionsAndMasks()
    {
        ClassificationSettings settings = ClassificationSettings.CreateDefault();
        TagDefinition enemy = settings.FindTag("Enemy")!;
        Guid stable = enemy.Id;
        Assert(settings.AddTag(" enemy ") == null, "Tag duplicates are rejected case-insensitively");
        Assert(settings.RenameTag(stable, "Hostile") && settings.FindTag(stable)?.Name == "Hostile",
            "Tag rename preserves stable GUID");
        LayerMask mask = LayerMask.FromLayers(1, 3);
        Assert(mask.Contains(1) && mask.Contains(3) && !mask.Contains(2), "LayerMask FromLayers and Contains");
        mask = mask.Add(2).Remove(1);
        Assert(!mask.Contains(1) && mask.Contains(2) && LayerMask.All.Contains(31) && LayerMask.None == LayerMask.FromLayers(),
            "LayerMask All None Add Remove");
        settings.CollisionMatrix.SetInteraction(2, 3, false);
        Assert(!settings.CollisionMatrix.ShouldInteract(2, 3) && !settings.CollisionMatrix.ShouldInteract(3, 2),
            "Collision matrix changes are symmetric");
    }

    private static void TestObjectsIndexesFilteringAndSerialization(string root, AssetDatabase database, AssetManager assets)
    {
        ClassificationSettings settings = ClassificationSettings.CreateDefault();
        Guid playerTag = settings.FindTag("Player")!.Id;
        Guid enemyTag = settings.FindTag("Enemy")!.Id;
        int playerLayer = settings.FindLayer("Player")!.Index;
        int enemyLayer = settings.FindLayer("Enemy")!.Index;
        int worldLayer = settings.FindLayer("World")!.Index;
        var scene = new Scene("Classification", settings);
        GameObject player = scene.CreateGameObject("Hero");
        player.Layer = playerLayer; player.AddTag(playerTag); player.AddTag(enemyTag); player.AddTag(enemyTag);
        Assert(player.Tags.Count == 2 && player.HasTag("Player") && scene.CountWithTag(enemyTag) == 1,
            "GameObjects support multiple non-duplicate tags and indexed name lookup");
        player.RemoveTag(enemyTag);
        Assert(scene.CountWithTag(enemyTag) == 0, "Tag index updates immediately on removal");
        player.AddTag(enemyTag);

        GameObject wall = scene.CreateGameObject("Wall"); wall.Layer = worldLayer;
        wall.Transform.LocalPosition = new Vector3(0, 0, -4);
        wall.AddComponent(new BoxCollider3D { Size = Vector3.One });
        GameObject bandit = scene.CreateGameObject("Bandit"); bandit.Layer = enemyLayer; bandit.AddTag(enemyTag);
        bandit.Transform.LocalPosition = new Vector3(3, 0, -4);
        BoxCollider3D banditCollider = bandit.AddComponent(new BoxCollider3D { Size = Vector3.One });
        Assert(scene.FindGameObjectsOnLayer(enemyLayer).Single() == bandit &&
               scene.FindGameObjects(LayerMask.FromLayers(worldLayer, enemyLayer)).Count() == 2,
            "Scene layer indexes answer layer-mask queries");

        settings.CollisionMatrix.SetInteraction(playerLayer, enemyLayer, false);
        Assert(!CollisionFilter.ShouldInteract(player, null, bandit, banditCollider, settings),
            "Central collision filter applies project matrix");
        settings.CollisionMatrix.SetInteraction(playerLayer, enemyLayer, true);
        banditCollider.UseProjectMatrix = false;
        banditCollider.CollisionMask = LayerMask.FromLayers(worldLayer);
        Assert(!CollisionFilter.ShouldInteract(player, null, bandit, banditCollider, settings),
            "Collider custom mask restricts otherwise-enabled interactions");
        banditCollider.CollisionMask = LayerMask.FromLayers(playerLayer);
        Assert(CollisionFilter.ShouldInteract(player, null, bandit, banditCollider, settings),
            "Collider custom mask accepts selected layers");
        banditCollider.UseProjectMatrix = true;
        settings.CollisionMatrix.SetInteraction(playerLayer, enemyLayer, false);

        Assert(GameplayQuery3D.Raycast(scene, Vector3.Zero, -Vector3.UnitZ, out RaycastHit3D hit, 20f,
                   player, LayerMask.FromLayers(worldLayer, enemyLayer), player) && hit.GameObject == wall,
            "Query mask and matrix preserve included World hits");
        Assert(!GameplayQuery3D.Raycast(scene, new Vector3(3, 0, 0), -Vector3.UnitZ, out _, 20f,
                   player, LayerMask.FromLayers(enemyLayer), player),
            "Query collision matrix filters disabled Player-to-Enemy pair");
        settings.CollisionMatrix.SetInteraction(playerLayer, enemyLayer, true);
        Assert(GameplayQuery3D.Raycast(scene, new Vector3(3, 0, 0), -Vector3.UnitZ, out hit, 20f,
                   player, LayerMask.FromLayers(enemyLayer), player) && hit.GameObject == bandit,
            "Re-enabling matrix interaction restores query hits");

        var serializer = new SceneSerializer(new ComponentSerializer(root, database, assets), settings);
        Scene clone = serializer.CloneForRuntime(scene);
        GameObject clonedPlayer = clone.FindGameObject(player.Id)!;
        BoxCollider3D clonedBanditCollider = clone.FindGameObject(bandit.Id)!.GetComponent<BoxCollider3D>()!;
        Assert(clonedPlayer.Layer == playerLayer && clonedPlayer.HasTag(playerTag) && clonedBanditCollider.UseProjectMatrix,
            "Play clone preserves tags, layer, and collider filtering");
        clonedPlayer.RemoveTag(playerTag); clonedPlayer.Layer = 6;
        Assert(player.HasTag(playerTag) && player.Layer == playerLayer, "Play classification changes remain isolated");

        Scene legacy = serializer.Deserialize(new SceneData
        {
            Name = "Legacy", SceneId = Guid.NewGuid(),
            GameObjects = { new GameObjectData { Id = Guid.NewGuid(), Name = "Old" } }
        });
        Assert(legacy.GameObjects[0].Tags.Count == 0 && legacy.GameObjects[0].Layer == 0,
            "Legacy scenes migrate to no tags and Default layer");
        scene.DestroyGameObject(bandit);
        Assert(scene.CountWithTag(enemyTag) == 1 && scene.FindGameObjectsOnLayer(enemyLayer).Count == 0,
            "Object deletion removes stale classification index entries");
        Assert(!settings.TryRemoveTag(playerTag, scene.GameObjects, out int uses) && uses == 1,
            "Referenced tags cannot be removed");
        Assert(!settings.TryClearLayer(playerLayer, scene.GameObjects, out uses) && uses == 1,
            "Referenced layers cannot be cleared");
    }

    private static void TestProjectPersistence(string root)
    {
        var project = new ProjectData { Name = "Classification" };
        TagDefinition custom = project.Classification.AddTag("Destructible")!;
        project.Classification.DefineLayer(7, "Destructible");
        project.Classification.CollisionMatrix.SetInteraction(2, 7, false);
        string path = Path.Combine(root, "TagsLayers.byteproject");
        var serializer = new ProjectSerializer(); serializer.Save(project, path);
        ProjectData loaded = serializer.Load(path);
        Assert(loaded.Classification.FindTag(custom.Id)?.Name == "Destructible" &&
               loaded.Classification.FindLayer(7)?.Name == "Destructible" &&
               !loaded.Classification.CollisionMatrix.ShouldInteract(2, 7),
            "Project settings round-trip stable tags, layers, and collision matrix");
    }

    private static void TestByteGraph()
    {
        ClassificationSettings settings = ClassificationSettings.CreateDefault();
        var scene = new Scene("Graph", settings);
        GameObject self = scene.CreateGameObject("Self");
        Guid enemy = settings.FindTag("Enemy")!.Id;
        var context = new EventExecutionContext { Globals = new VariableStore(), Scene = scene, Self = self };
        VisualLogicRegistry registry = VisualLogicRegistry.CreateDefault();
        var add = new VisualInstruction { Id = "object.addTag", Arguments =
            { ["target"] = EventValue.String("Self"), ["tag"] = EventValue.String(enemy.ToString()) } };
        registry.TryGetAction(add.Id, out VisualActionDefinition? addDefinition); addDefinition!.Execute(add, context);
        registry.TryGetCondition("object.hasTag", out VisualConditionDefinition? hasDefinition);
        Assert(hasDefinition!.Evaluate(add, context), "ByteGraph Add Tag and Object Has Tag use stable GUID");
        var layer = new VisualInstruction { Id = "object.setLayer", Arguments =
            { ["target"] = EventValue.String("Self"), ["layer"] = EventValue.Number(3) } };
        registry.TryGetAction(layer.Id, out VisualActionDefinition? layerDefinition); layerDefinition!.Execute(layer, context);
        registry.TryGetCondition("object.isOnLayer", out VisualConditionDefinition? onLayer);
        Assert(onLayer!.Evaluate(layer, context), "ByteGraph Set Layer and Object Is On Layer use stable index");
    }

    private static void TestBlueprintOverrides(string root)
    {
        string projectPath = Path.Combine(root, "V08DProject.byteproject");
        using EditorProjectContext project = EditorProjectContext.Create(projectPath, _ => { });
        ClassificationSettings settings = project.Project.Classification;
        Guid enemy = settings.FindTag("Enemy")!.Id;
        Guid boss = settings.AddTag("Boss")!.Id;
        Guid ranged = settings.AddTag("Ranged")!.Id;
        int enemyLayer = settings.FindLayer("Enemy")!.Index;
        int triggerLayer = settings.FindLayer("Trigger")!.Index;
        int projectileLayer = settings.FindLayer("PlayerProjectile")!.Index;
        project.SaveProject();
        var sourceScene = new Scene("Source", settings);
        GameObject sourceObject = sourceScene.CreateGameObject("BP_Enemy");
        sourceObject.AddTag(enemy); sourceObject.Layer = enemyLayer;
        GameObjectData sourceData = project.Scenes.Serialize(sourceScene).GameObjects.Single();
        var blueprint = new BlueprintDefinition { Name = "BP_Enemy", Type = BlueprintType.Character, Root = sourceData };
        string path = Path.Combine(project.ProjectRoot, "Assets", "BP_Enemy.byteblueprint");
        new BlueprintSerializer().Save(blueprint, path); project.AssetDatabase.Scan();
        project.AssetDatabase.TryGetAsset("Assets/BP_Enemy.byteblueprint", out AssetRecord? asset);
        var scene = new Scene("Instances", settings);
        GameObject first = Place(scene, project, asset!, blueprint);
        GameObject second = Place(scene, project, asset!, blueprint);
        Guid firstId = first.GetComponent<BlueprintInstance>()!.InstanceId;
        Guid secondId = second.GetComponent<BlueprintInstance>()!.InstanceId;
        first.Layer = triggerLayer; first.AddTag(boss);
        blueprint.Root.Layer = projectileLayer; blueprint.Root.Tags.Add(ranged);
        new BlueprintSerializer().Save(blueprint, asset!.FullPath); project.AssetDatabase.Scan();
        BlueprintInstanceSynchronizer.Propagate(project, scene, new AssetReference(asset.Guid, asset.ProjectPath));
        first = Find(scene, firstId); second = Find(scene, secondId);
        Assert(first.Layer == triggerLayer && first.HasTag(boss) && first.HasTag(ranged),
            "Blueprint layer override and per-tag override survive source propagation");
        Assert(second.Layer == projectileLayer && second.HasTag(ranged), "Inherited classifications receive source changes");
        first = BlueprintInstanceSynchronizer.Revert(first, project)!;
        Assert(first.Layer == projectileLayer && !first.HasTag(boss) && first.HasTag(ranged),
            "Blueprint Revert restores source classifications");
        first.AddTag(boss);
        first = BlueprintInstanceSynchronizer.Apply(first, project)!;
        second = Find(scene, secondId);
        Assert(new BlueprintSerializer().Load(asset.FullPath).Root.Tags.Contains(boss) && second.HasTag(boss),
            "Blueprint Apply writes tags to source and propagates them");
    }

    private static GameObject Place(Scene scene, EditorProjectContext project, AssetRecord asset, BlueprintDefinition blueprint)
    {
        GameObject root = project.Scenes.InstantiateHierarchy(scene, new[] { blueprint.Root }, null,
            out IReadOnlyDictionary<Guid, Guid> map).Single();
        var instance = root.AddComponent(new BlueprintInstance
        {
            Blueprint = new AssetReference(asset.Guid, asset.ProjectPath), InstanceId = Guid.NewGuid()
        });
        BlueprintInstanceSynchronizer.Initialize(instance, blueprint, map);
        return root;
    }
    private static GameObject Find(Scene scene, Guid instanceId) =>
        scene.GameObjects.Single(item => item.GetComponent<BlueprintInstance>()?.InstanceId == instanceId);
    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + name);
    }
}
