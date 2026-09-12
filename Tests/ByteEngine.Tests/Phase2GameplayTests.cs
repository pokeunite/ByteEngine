using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Editor.Panels;

namespace ByteEngine.Tests;

internal static class Phase2GameplayTests
{
    public static void Run(string root, AssetDatabase database, AssetManager assets)
    {
        TestManagerStatesAndRestart();
        TestCameraFollow();
        TestSerialization(root, database, assets);
        TestByteArenaTemplate();
    }

    private static void TestManagerStatesAndRestart()
    {
        var scene = new Scene("Arena State");
        GameObject player = scene.CreateGameObject("Player");
        HealthComponent playerHealth = player.AddComponent(new HealthComponent());
        player.AddComponent(new PlayerController3D());
        GameObject enemy = scene.CreateGameObject("Enemy");
        HealthComponent enemyHealth = enemy.AddComponent(new HealthComponent { MaxHealth = 50f, CurrentHealth = 50f });
        enemy.AddComponent(new SimpleEnemyAI3D { TargetId = player.Id, MoveSpeed = 0f, DetectionRange = 0f });
        GameObject managerObject = scene.CreateGameObject("Manager");
        ArenaGameManager manager = managerObject.AddComponent(new ArenaGameManager { PlayerId = player.Id });
        scene.LoadInternal();
        Assert(manager.GameState == ArenaGameState.Playing && manager.RemainingEnemies == 1, "Arena manager begins Playing");
        enemyHealth.Kill();
        Tick(scene, .01);
        Assert(manager.GameState == ArenaGameState.Won && manager.RemainingEnemies == 0, "Arena manager detects win");
        manager.Restart();
        Assert(manager.GameState == ArenaGameState.Playing && manager.RemainingEnemies == 1 && !enemyHealth.IsDead, "Arena manager restarts arena");
        playerHealth.Kill();
        Tick(scene, .01);
        Assert(manager.GameState == ArenaGameState.Lost, "Arena manager detects loss");
    }

    private static void TestCameraFollow()
    {
        var scene = new Scene("Camera Follow");
        GameObject target = scene.CreateGameObject("Hero");
        target.Transform.WorldPosition = new Vector3(2f, 1f, 3f);
        GameObject camera = scene.CreateGameObject("Camera");
        camera.AddComponent(new Camera3D());
        camera.AddComponent(new ThirdPersonCamera3D
        {
            TargetId = target.Id,
            TargetName = "Wrong Name",
            Distance = 5f,
            Height = 3f,
            Pitch = 0f,
            ShoulderOffset = 0f,
            LookAtHeight = 1f,
            FollowSmoothing = 0f
        });
        scene.LoadInternal();
        Assert(Vector3.Distance(camera.Transform.WorldPosition, new Vector3(2f, 4f, 8f)) < .001f,
            "ThirdPersonCamera3D resolves ID and follows behind target");

        var nameScene = new Scene("Camera Name Follow");
        GameObject namedTarget = nameScene.CreateGameObject("Named Player");
        GameObject namedCamera = nameScene.CreateGameObject("Camera");
        namedCamera.AddComponent(new ThirdPersonCamera3D { TargetName = namedTarget.Name, Distance = 2f, Height = 1f, Pitch = 0f, ShoulderOffset = 0f, FollowSmoothing = 0f });
        nameScene.LoadInternal();
        Assert(Vector3.Distance(namedCamera.Transform.WorldPosition, new Vector3(0f, 1f, 2f)) < .001f,
            "ThirdPersonCamera3D safely resolves target name");
    }

    private static void TestSerialization(string root, AssetDatabase database, AssetManager assets)
    {
        var serializer = new SceneSerializer(new ComponentSerializer(root, database, assets));
        var scene = new Scene("Phase 2 Persistence");
        GameObject target = scene.CreateGameObject("Target");
        GameObject item = scene.CreateGameObject("Components");
        item.AddComponent(new PlayerController3D { UseLocalOrientation = false });
        item.AddComponent(new PlayerShooter3D { Automatic = false });
        item.AddComponent(new ThirdPersonCamera3D
        {
            TargetId = target.Id,
            TargetName = target.Name,
            Distance = 9f,
            Height = 5f,
            LookAtHeight = 1.5f,
            FollowSmoothing = 6f,
            Yaw = 35f,
            Pitch = 25f,
            MinPitch = -20f,
            MaxPitch = 60f,
            MouseSensitivity = .22f,
            ShoulderOffset = .75f
        });
        item.AddComponent(new ArenaGameManager { PlayerId = target.Id, PlayerName = target.Name });

        Scene clone = serializer.CloneForRuntime(scene);
        GameObject copy = clone.FindGameObject("Components")!;
        PlayerController3D controller = copy.GetComponent<PlayerController3D>()!;
        PlayerShooter3D shooter = copy.GetComponent<PlayerShooter3D>()!;
        ThirdPersonCamera3D camera = copy.GetComponent<ThirdPersonCamera3D>()!;
        ArenaGameManager manager = copy.GetComponent<ArenaGameManager>()!;
        Assert(!controller.UseLocalOrientation, "PlayerController3D serialization");
        Assert(!shooter.Automatic, "PlayerShooter3D serialization");
        Assert(camera.TargetId == target.Id && camera.TargetName == target.Name && Near(camera.Distance, 9f) &&
            Near(camera.Height, 5f) && Near(camera.LookAtHeight, 1.5f) && Near(camera.FollowSmoothing, 6f) &&
            Near(camera.Yaw, 35f) && Near(camera.Pitch, 25f) && Near(camera.MinPitch, -20f) &&
            Near(camera.MaxPitch, 60f) && Near(camera.MouseSensitivity, .22f) && Near(camera.ShoulderOffset, .75f),
            "ThirdPersonCamera3D serialization");
        Assert(manager.PlayerId == target.Id && manager.PlayerName == target.Name, "ArenaGameManager serialization");

        string path = Path.Combine(root, "Scenes", "Phase2Gameplay.bytescene");
        serializer.Save(scene, path);
        GameObject diskCopy = serializer.Load(path).FindGameObject("Components")!;
        Assert(diskCopy.GetComponent<PlayerController3D>() != null && diskCopy.GetComponent<PlayerShooter3D>() != null &&
            diskCopy.GetComponent<ThirdPersonCamera3D>() != null && diskCopy.GetComponent<ArenaGameManager>() != null,
            "All Phase 2 components survive scene save/load");
    }

    private static void TestByteArenaTemplate()
    {
        Scene arena = ProjectTemplateFactory.Create(ProjectTemplate.ByteArena);
        GameObject player = arena.FindGameObject("Player")!;
        Assert(arena.Name == "ByteArena" && arena.FindGameObject("Ground")?.GetComponent<GroundSurface>() != null,
            "ByteArena template contains ground");
        Assert(player.GetComponent<CharacterController3D>() != null && player.GetComponent<HealthComponent>() != null &&
            player.GetComponent<ProjectileLauncher3D>() != null && player.GetComponent<PlayerController3D>() != null &&
            player.GetComponent<PlayerShooter3D>() != null, "ByteArena template contains playable player");
        Assert(arena.FindComponent<Camera3D>() != null && arena.FindComponent<ThirdPersonCamera3D>() != null,
            "ByteArena template contains follow camera");
        Assert(arena.GameObjects.Count(item => item.GetComponent<SimpleEnemyAI3D>() != null && item.GetComponent<HealthComponent>() != null) >= 3,
            "ByteArena template contains three enemies");
        Assert(arena.FindComponent<ArenaGameManager>() != null, "ByteArena template contains game manager");
    }

    private static void Tick(Scene scene, double deltaTime) { Time.Update(deltaTime); scene.UpdateInternal(); }
    private static bool Near(float value, float expected) => Math.Abs(value - expected) < .001f;
    private static void Assert(bool condition, string name) { if (!condition) throw new InvalidOperationException("FAILED: " + name); }
}
