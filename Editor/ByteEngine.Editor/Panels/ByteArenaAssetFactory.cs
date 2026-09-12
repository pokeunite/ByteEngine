using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.VisualLogic;

namespace ByteEngine.Editor.Panels;

internal static class ByteArenaAssetFactory
{
    public static void Generate(EditorProjectContext context, Scene scene)
    {
        string blueprintsDirectory = Path.Combine(context.ProjectRoot, context.Project.AssetDirectory, "Blueprints");
        string eventsDirectory = Path.Combine(context.ProjectRoot, context.Project.AssetDirectory, "Events");
        Directory.CreateDirectory(blueprintsDirectory);
        Directory.CreateDirectory(eventsDirectory);

        string projectilePath = Path.Combine(blueprintsDirectory, "BP_Projectile.byteblueprint");
        var projectileScene = new Scene("Projectile Blueprint Source");
        GameObject projectile = projectileScene.CreateGameObject("Projectile");
        projectile.Transform.LocalScale = new Vector3(.18f);
        projectile.AddComponent(new MeshRenderer
        {
            Primitive = PrimitiveMeshType.Sphere,
            Material = new Material { BaseColor = new Vector4(1f, .72f, .08f, 1f) }
        });
        projectile.AddComponent(new Projectile3D { Damage = 25f, Radius = .08f });
        projectile.AddComponent(new LifetimeComponent { LifetimeSeconds = 4f });
        var projectileBlueprint = new BlueprintDefinition
        {
            Name = "BP_Projectile",
            Root = context.Scenes.Serialize(projectileScene).GameObjects.Single()
        };
        new BlueprintSerializer().Save(projectileBlueprint, projectilePath);
        context.AssetDatabase.Scan();
        AssetRecord projectileAsset = RequireAsset(context, "Assets/Blueprints/BP_Projectile.byteblueprint");

        GameObject player = scene.FindGameObject("Player") ?? throw new InvalidDataException("ByteArena Player is missing.");
        player.GetComponent<ProjectileLauncher3D>()!.ProjectileBlueprint =
            new AssetReference(projectileAsset.Guid, projectileAsset.ProjectPath);
        GameObject enemy = scene.GameObjects.First(item => item.GetComponent<SimpleEnemyAI3D>() != null);
        SceneData sceneData = context.Scenes.Serialize(scene);
        GameObjectData playerData = sceneData.GameObjects.Single(item => item.Id == player.Id);
        GameObjectData enemyData = sceneData.GameObjects.Single(item => item.Id == enemy.Id);
        ComponentData? enemyAi = enemyData.Components.FirstOrDefault(item => item.Type == nameof(SimpleEnemyAI3D));
        if (enemyAi != null) enemyAi.Properties["targetId"] = Guid.Empty.ToString();

        string playerPath = Path.Combine(blueprintsDirectory, "BP_Player.byteblueprint");
        string enemyPath = Path.Combine(blueprintsDirectory, "BP_Enemy.byteblueprint");
        var playerBlueprint = new BlueprintDefinition { Name = "BP_Player", Type = BlueprintType.Character, Root = playerData };
        var enemyBlueprint = new BlueprintDefinition { Name = "BP_Enemy", Type = BlueprintType.Character, Root = enemyData };
        var blueprintSerializer = new BlueprintSerializer();
        blueprintSerializer.Save(playerBlueprint, playerPath);
        blueprintSerializer.Save(enemyBlueprint, enemyPath);
        context.AssetDatabase.Scan();
        AssetRecord playerAsset = RequireAsset(context, "Assets/Blueprints/BP_Player.byteblueprint");
        AssetRecord enemyAsset = RequireAsset(context, "Assets/Blueprints/BP_Enemy.byteblueprint");

        var playerCombat = new EventModuleDefinition
        {
            Name = "PlayerCombat",
            TargetBlueprintGuid = playerAsset.Guid,
            TargetBlueprintPath = playerAsset.ProjectPath,
            RequiredComponents = new List<string> { nameof(HealthComponent) },
            Rules = new List<EventRuleDefinition>
            {
                new()
                {
                    EditorTitle = "Low-health recovery example",
                    Conditions = new List<VisualInstruction>
                    {
                        Instruction("health.percentAtMost", ("target", EventValue.String("Self")), ("value", EventValue.Number(.5)))
                    },
                    Actions = new List<VisualInstruction>
                    {
                        Instruction("health.heal", ("target", EventValue.String("Self")), ("amount", EventValue.Number(10)))
                    }
                }
            }
        };
        var arenaGame = new EventModuleDefinition
        {
            Name = "ArenaGame",
            RequiredComponents = new List<string> { nameof(ArenaGameManager) },
            Rules = new List<EventRuleDefinition>
            {
                new()
                {
                    EditorTitle = "Arena win and restart example",
                    Conditions = new List<VisualInstruction>
                    {
                        Instruction("arena.won", ("target", EventValue.String("Self")))
                    },
                    Actions = new List<VisualInstruction>
                    {
                        Instruction("arena.restart", ("target", EventValue.String("Self")))
                    }
                }
            }
        };
        string playerEventsPath = Path.Combine(eventsDirectory, "PlayerCombat.byteevents");
        string arenaEventsPath = Path.Combine(eventsDirectory, "ArenaGame.byteevents");
        var eventSerializer = new EventModuleSerializer();
        eventSerializer.Save(playerCombat, playerEventsPath);
        eventSerializer.Save(arenaGame, arenaEventsPath);
        context.AssetDatabase.Scan();
        playerBlueprint.EventModules.Add(RequireAsset(context, "Assets/Events/PlayerCombat.byteevents").Guid);
        blueprintSerializer.Save(playerBlueprint, playerPath);

        player.AddComponent(new BlueprintInstance { Blueprint = new AssetReference(playerAsset.Guid, playerAsset.ProjectPath) });
        foreach (GameObject instance in scene.GameObjects.Where(item => item.GetComponent<SimpleEnemyAI3D>() != null))
            instance.AddComponent(new BlueprintInstance { Blueprint = new AssetReference(enemyAsset.Guid, enemyAsset.ProjectPath) });
        context.AssetDatabase.Scan();
    }

    private static VisualInstruction Instruction(string id, params (string Name, EventValue Value)[] arguments) => new()
    {
        Id = id,
        Arguments = arguments.ToDictionary(item => item.Name, item => item.Value, StringComparer.OrdinalIgnoreCase)
    };

    private static AssetRecord RequireAsset(EditorProjectContext context, string projectPath) =>
        context.AssetDatabase.TryGetAsset(projectPath, out AssetRecord? asset) && asset != null
            ? asset
            : throw new InvalidDataException($"Generated ByteArena asset was not registered: {projectPath}");
}
