using System.Numerics;
using ByteEngine.Core;
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
        TestCameraFollowAndAim();
        TestDirectionalFire();
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

    private static void TestCameraFollowAndAim()
    {
        var scene = new Scene("TPS Camera");
        GameObject player = scene.CreateGameObject("Player");
        player.AddComponent(new ProjectileLauncher3D { MuzzleOffset = new Vector3(0f, 0f, -.5f) });
        GameObject cameraObject = scene.CreateGameObject("Camera");
        Camera3D camera = cameraObject.AddComponent(new Camera3D());
        cameraObject.AddComponent(new ThirdPersonCamera3D
        {
            TargetId = player.Id,
            Distance = 5f,
            Height = 1f,
            Yaw = 90f,
            Pitch = 0f,
            ShoulderOffset = 0f,
            FollowSmoothing = 0f
        });
        scene.LoadInternal();
        Assert(Vector3.Distance(cameraObject.Transform.WorldPosition, new Vector3(5f, 1f, 0f)) < .001f,
            "ThirdPersonCamera3D yaw controls orbit position");
        player.Transform.WorldPosition = new Vector3(0f, 0f, 2f);
        Time.Update(.016);
        scene.UpdateInternal();
        Assert(Vector3.Distance(cameraObject.Transform.WorldPosition, new Vector3(5f, 1f, 2f)) < .001f,
            "ThirdPersonCamera3D follows translated target");
        Vector3 aim = PlayerShooter3D.CalculateAimDirection(player, camera, scene, 16f / 9f);
        Assert(float.IsFinite(aim.X) && float.IsFinite(aim.Y) && float.IsFinite(aim.Z) && Near(aim.Length(), 1f),
            "PlayerShooter3D center-screen aim helper returns normalized direction");
    }

    private static void TestDirectionalFire()
    {
        var scene = new Scene("Directional Launcher");
        GameObject shooter = scene.CreateGameObject("Shooter");
        var launcher = shooter.AddComponent(new ProjectileLauncher3D { ProjectileSpeed = 30f, FireCooldown = 0f });
        scene.LoadInternal();
        int before = scene.GameObjectCount;
        Assert(launcher.Fire(Vector3.Zero) == null && scene.GameObjectCount == before && launcher.CanFire,
            "ProjectileLauncher3D safely rejects zero direction");
        GameObject? spawned = launcher.Fire(new Vector3(4f, 0f, 0f));
        Vector3 velocity = spawned!.GetComponent<Projectile3D>()!.Velocity;
        Assert(Vector3.Distance(velocity, new Vector3(30f, 0f, 0f)) < .001f,
            "ProjectileLauncher3D Fire(direction) normalizes and applies projectile speed");
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
        scene.LoadInternal();
        GameObject? blueprintProjectile = launcher.Fire(Vector3.UnitX);
        Assert(blueprintProjectile?.GetComponent<Projectile3D>() is { } runtimeProjectile &&
            blueprintProjectile.GetComponent<ByteEngine.Core.Graphics.ThreeD.MeshRenderer>() != null &&
            runtimeProjectile.Velocity.X > 0f && Near(runtimeProjectile.Velocity.Length(), launcher.ProjectileSpeed),
            "Configured launcher spawns visible Blueprint projectile in requested direction");
        Assert(scene.FindGameObject("Player")!.GetComponent<BlueprintInstance>() is { } instance &&
            context.AssetDatabase.Resolve(instance.Blueprint) != null, "ByteArena scene Blueprint instance resolves");
    }

    private static bool Near(float value, float expected) => Math.Abs(value - expected) < .001f;
    private static void Assert(bool condition, string name) { if (!condition) throw new InvalidOperationException("FAILED: " + name); }
}
