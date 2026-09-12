using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Editor;
using ByteEngine.Editor.Panels;

namespace ByteEngine.Tests;

internal static class Phase3IntegrationTests
{
    public static void Run(string root)
    {
        TestCameraMath();
        TestGameplayNodes();
        TestGeneratedAssets(root);
    }

    private static void TestCameraMath()
    {
        var scene = new Scene("Ray Math");
        GameObject cameraObject = scene.CreateGameObject("Camera");
        Camera3D camera = cameraObject.AddComponent(new Camera3D { FieldOfView = 60f });
        (Vector3 origin, Vector3 direction) = camera.ScreenPointToRay(new Vector2(.5f), 16f / 9f);
        Assert(origin == Vector3.Zero && Vector3.Distance(direction, -Vector3.UnitZ) < .001f,
            "Camera3D center screen ray follows camera forward");
        Assert(camera.ScreenPointToRay(new Vector2(1f, .5f), 16f / 9f).Direction.X > 0f,
            "Camera3D screen ray respects horizontal screen position");
        var follow = new ThirdPersonCamera3D { MinPitch = -15f, MaxPitch = 45f, Pitch = 90f };
        Assert(Near(follow.Pitch, 45f), "ThirdPersonCamera3D clamps maximum pitch");
        follow.Pitch = -90f;
        Assert(Near(follow.Pitch, -15f), "ThirdPersonCamera3D clamps minimum pitch");
    }

    private static void TestGameplayNodes()
    {
        VisualLogicRegistry registry = VisualLogicRegistry.CreateDefault();
        var scene = new Scene("Gameplay Nodes");
        GameObject target = scene.CreateGameObject("Target");
        HealthComponent health = target.AddComponent(new HealthComponent());
        var context = new EventExecutionContext { Globals = new VariableStore(), Scene = scene, Self = target };
        Assert(registry.TryGetAction("health.damage", out VisualActionDefinition? damage) && damage != null,
            "Damage Object gameplay action registered");
        damage!.Execute(new VisualInstruction
        {
            Id = "health.damage",
            Arguments = new Dictionary<string, EventValue>
            {
                ["target"] = EventValue.String("Self"),
                ["amount"] = EventValue.Number(25)
            }
        }, context);
        Assert(Near(health.CurrentHealth, 75f), "Damage Object gameplay action executes");
        Assert(registry.TryGetCondition("health.percentAtMost", out VisualConditionDefinition? lowHealth) && lowHealth != null &&
            lowHealth.Evaluate(new VisualInstruction
            {
                Id = "health.percentAtMost",
                Arguments = new Dictionary<string, EventValue> { ["value"] = EventValue.Number(.8) }
            }, context), "Health percent gameplay condition executes");
        Assert(registry.TryGetAction("projectile.fire", out _) && registry.TryGetAction("arena.restart", out _) &&
            registry.TryGetCondition("arena.won", out _) && registry.TryGetCondition("arena.lost", out _),
            "Projectile and arena gameplay nodes registered");
    }

    private static void TestGeneratedAssets(string root)
    {
        string projectPath = Path.Combine(root, "GeneratedByteArena.byteproject");
        using EditorProjectContext context = EditorProjectContext.Create(projectPath, message => throw new InvalidOperationException(message));
        Scene scene = ProjectTemplateFactory.Create(ProjectTemplate.ByteArena);
        ByteArenaAssetFactory.Generate(context, scene);
        string playerPath = Path.Combine(context.ProjectRoot, "Assets", "Blueprints", "BP_Player.byteblueprint");
        string enemyPath = Path.Combine(context.ProjectRoot, "Assets", "Blueprints", "BP_Enemy.byteblueprint");
        string projectilePath = Path.Combine(context.ProjectRoot, "Assets", "Blueprints", "BP_Projectile.byteblueprint");
        string combatPath = Path.Combine(context.ProjectRoot, "Assets", "Events", "PlayerCombat.byteevents");
        string arenaPath = Path.Combine(context.ProjectRoot, "Assets", "Events", "ArenaGame.byteevents");
        Assert(File.Exists(playerPath) && File.Exists(enemyPath) && File.Exists(projectilePath),
            "ByteArena generation creates Blueprint assets");
        Assert(File.Exists(combatPath) && File.Exists(arenaPath), "ByteArena generation creates Event assets");
        BlueprintDefinition player = new BlueprintSerializer().Load(playerPath);
        BlueprintDefinition projectile = new BlueprintSerializer().Load(projectilePath);
        Assert(player.Root.Components.Any(item => item.Type == nameof(PlayerController3D)) &&
            projectile.Root.Components.Any(item => item.Type == nameof(Projectile3D)), "Generated Blueprints contain reusable gameplay components");
        var eventSerializer = new EventModuleSerializer();
        Assert(eventSerializer.Load(combatPath).Rules.Count > 0 && eventSerializer.Load(arenaPath).Rules.Count > 0,
            "Generated Event assets deserialize with meaningful rules");
        ProjectileLauncher3D launcher = scene.FindGameObject("Player")!.GetComponent<ProjectileLauncher3D>()!;
        Assert(context.AssetDatabase.Resolve(launcher.ProjectileBlueprint)?.ProjectPath.EndsWith("BP_Projectile.byteblueprint") == true,
            "Generated projectile Blueprint reference resolves");
        Assert(scene.FindGameObject("Player")!.GetComponent<BlueprintInstance>() is { } instance &&
            context.AssetDatabase.Resolve(instance.Blueprint) != null, "ByteArena scene Blueprint instance resolves");
    }

    private static bool Near(float value, float expected) => Math.Abs(value - expected) < .001f;
    private static void Assert(bool condition, string name) { if (!condition) throw new InvalidOperationException("FAILED: " + name); }
}
