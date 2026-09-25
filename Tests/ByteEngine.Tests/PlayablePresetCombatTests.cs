using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Editor;

namespace ByteEngine.Tests;

internal static class PlayablePresetCombatTests
{
    public static void Run(string root, AssetDatabase database, AssetManager assets)
    {
        TestPlayerPresets(root, database, assets);
        TestModelToPlayer(root);
        TestMovingPlayerCamera();
        TestClearPlayerSpawn();
        TestRaycastAndCombat();
        TestProjectileHit();
    }

    private static void TestPlayerPresets(string root, AssetDatabase database, AssetManager assets)
    {
        var scene = new Scene("Player presets");
        GameObject player = scene.CreateGameObject("Player");
        BlueprintAuthoringService.SetupPlayablePlayer(player);
        BlueprintAuthoringService.SetupPlayablePlayer(player);
        Assert(player.GetComponent<CharacterController3D>() != null &&
               player.GetComponent<PlayerController3D>() != null &&
               player.GetComponent<CameraBoom3D>() != null &&
               player.GetComponent<HealthComponent>() != null &&
               player.GetComponent<ProjectileLauncher3D>() != null &&
               player.GetComponent<PlayerShooter3D>() != null,
            "Playable TPS uses existing movement, camera, health and weapon components");
        Assert(player.Components.Count(item => item is HealthComponent) == 1 &&
               player.Components.Count(item => item is ProjectileLauncher3D) == 1 &&
               player.Components.Count(item => item is PlayerShooter3D) == 1,
            "Applying the player preset twice does not duplicate gameplay components");

        BlueprintAuthoringService.ApplyPlayerViewPreset(player, PlayerViewPreset.FirstPerson);
        Assert(player.GetComponent<CameraBoom3D>()!.ArmLength < .3f &&
               !player.GetComponent<PlayerShooter3D>()!.AimAtPointer,
            "FPS uses a head-mounted center-screen aim");
        BlueprintAuthoringService.ApplyPlayerViewPreset(player, PlayerViewPreset.TopDownTwinStick);
        Assert(player.GetComponent<PlayerShooter3D>()!.AimAtPointer &&
               !player.GetComponent<PlayerController3D>()!.AcceptLookInput &&
               !player.GetComponent<CameraBoom3D>()!.UseControlRotation,
            "Top-down twin-stick keeps a fixed camera and aims with the pointer");
        BlueprintAuthoringService.ApplyPlayerViewPreset(player, PlayerViewPreset.Isometric);
        Assert(MathF.Abs(player.GetComponent<CameraBoom3D>()!.Yaw - 45f) < .01f &&
               player.GetComponent<PlayerController3D>()!.ControlYaw == 45f,
            "Isometric movement orientation follows the fixed camera yaw");

        Camera3D camera = player.Children.Single(child => child.GetComponent<Camera3D>() != null)
            .GetComponent<Camera3D>()!;
        Vector3 stickRight = PlayerShooter3D.CalculateStickAimDirection(camera, Vector2.UnitX);
        Assert(stickRight.X > .99f && MathF.Abs(stickRight.Y) < .001f,
            "Right-stick aim follows the camera's horizontal right axis");

        var clone = new SceneSerializer(new ComponentSerializer(root, database, assets))
            .CloneForRuntime(scene);
        GameObject copied = clone.FindGameObject("Player")!;
        Assert(copied.GetComponent<PlayerShooter3D>()!.AimAtPointer &&
               !copied.GetComponent<PlayerController3D>()!.AcceptLookInput,
            "View-preset aiming and movement survive Play-mode cloning");
    }

    private static void TestMovingPlayerCamera()
    {
        var scene = new Scene("Moving player camera");
        GameObject floor = scene.CreateGameObject("Floor");
        floor.Transform.WorldPosition = new Vector3(0f, -1f, -2.960387f);
        floor.Transform.LocalScale = new Vector3(425.3202f, 1f, 213.81699f);
        floor.AddComponent(new BoxCollider3D { Size = new Vector3(1f, .05f, 1f) });
        GameObject player = scene.CreateGameObject("Player");
        player.Transform.WorldPosition = new Vector3(.2280643f, 0f, 2.9583619f);
        BlueprintAuthoringService.SetupPlayablePlayer(player);
        CapsuleCollider3D capsule = player.GetComponent<CapsuleCollider3D>()!;
        capsule.Radius = .15829384f;
        capsule.Height = 1.7639999f;
        capsule.Center = new Vector3(0f, .88199997f, -.02769928f);
        player.GetComponent<PlayerController3D>()!.Enabled = false;
        CharacterController3D motor = player.GetComponent<CharacterController3D>()!;
        CameraBoom3D boom = player.GetComponent<CameraBoom3D>()!;
        scene.LoadInternal();
        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;
        float maxCameraOffsetError = 0f;
        float previousZ = player.Transform.WorldPosition.Z;
        float minForwardStep = float.MaxValue;
        float maxForwardStep = float.MinValue;
        for (int frame = 0; frame < 120; frame++)
        {
            motor.Move(-Vector3.UnitZ);
            ByteEngine.Core.Time.Update(1.0 / 60.0);
            scene.UpdateInternal();
            float forwardStep = previousZ - player.Transform.WorldPosition.Z;
            previousZ = player.Transform.WorldPosition.Z;
            if (frame < 20) continue;
            minForwardStep = Math.Min(minForwardStep, forwardStep);
            maxForwardStep = Math.Max(maxForwardStep, forwardStep);
            minHeight = Math.Min(minHeight, player.Transform.WorldPosition.Y);
            maxHeight = Math.Max(maxHeight, player.Transform.WorldPosition.Y);
            Vector3 expected = player.Transform.WorldPosition +
                Vector3.UnitY * boom.PivotHeight +
                CameraBoom3D.CalculateOrbitVector(boom.DesiredBoomYaw, boom.DesiredBoomPitch, boom.ArmLength) +
                CameraBoom3D.CalculateCameraRight(boom.DesiredBoomYaw) * boom.ShoulderOffset;
            maxCameraOffsetError = Math.Max(maxCameraOffsetError,
                Vector3.Distance(expected, boom.ActualSocketPosition));
        }
        Assert(maxHeight - minHeight < .02f,
            $"Grounded moving player height stays stable ({minHeight:F3}..{maxHeight:F3})");
        Assert(maxForwardStep - minForwardStep < .01f,
            $"Moving character has stable horizontal steps ({minForwardStep:F3}..{maxForwardStep:F3})");
        Assert(maxCameraOffsetError < .05f,
            $"TPS camera follows moving player without offset jitter ({maxCameraOffsetError:F3})");
    }

    private static void TestClearPlayerSpawn()
    {
        var scene = new Scene("Clear player spawn");
        GameObject floor = scene.CreateGameObject("Ground");
        floor.Transform.WorldPosition = new Vector3(0f, -1f, -2.960387f);
        floor.Transform.LocalScale = new Vector3(425.3202f, 1f, 213.81699f);
        floor.AddComponent(new BoxCollider3D { Size = new Vector3(1f, .05f, 1f) });
        floor.AddComponent(new GroundSurface());
        GameObject obstacle = scene.CreateGameObject("Cube");
        obstacle.Transform.WorldPosition = new Vector3(0f, -.6994685f, 0f);
        obstacle.Transform.LocalRotation = new Quaternion(.20596746f, 0f, 0f, .97855884f);
        obstacle.Transform.LocalScale = new Vector3(1.6421329f, 1f, 4.2508183f);
        obstacle.AddComponent(new BoxCollider3D { Size = Vector3.One });
        Vector3 spawn = BlueprintAuthoringService.FindClearPlayerSpawn(scene);
        Assert(spawn.Z >= 2.9f && spawn.Y > -1f && spawn.Y < -.9f,
            $"New player spawns on open ground beyond the sloped cube ({spawn})");
    }

    private static void TestModelToPlayer(string root)
    {
        string projectPath = Path.Combine(root, "PlayablePresetProject.byteproject");
        using EditorProjectContext project = EditorProjectContext.Create(projectPath,
            message => throw new InvalidOperationException(message));
        foreach (PlayerViewPreset preset in Enum.GetValues<PlayerViewPreset>())
        {
            var (blueprintRoot, blueprintChildren) =
                BlueprintAuthoringService.CreatePlayableBlueprintHierarchy(project, "Preset Player", preset);
            Assert(blueprintRoot.Components.Any(item => item.Type == "CharacterController3D") &&
                   blueprintRoot.Components.Any(item => item.Type == "PlayerShooter3D") &&
                   blueprintChildren.Any(item => item.Name == "Camera") &&
                   blueprintChildren.Any(item => item.Name == "Model"),
                $"{preset} creates a playable Blueprint hierarchy");
            var boomData = blueprintRoot.Components.Single(item => item.Type == "CameraBoom3D");
            var shooterData = blueprintRoot.Components.Single(item => item.Type == "PlayerShooter3D");
            bool fixedView = preset is PlayerViewPreset.TopDownTwinStick or PlayerViewPreset.Isometric;
            Assert(boomData.Properties["useControlRotation"]!.GetValue<bool>() == !fixedView &&
                   shooterData.Properties["aimAtPointer"]!.GetValue<bool>() == fixedView,
                $"{preset} persists its camera and aiming configuration");
        }
        string modelPath = Path.Combine(project.ProjectRoot, "Assets", "VisiblePlayer.obj");
        File.WriteAllText(modelPath,
            "v -50 0 -20\n" + "v 50 0 -20\n" + "v 0 200 20\n" + "f 1 2 3\n");
        project.AssetDatabase.Scan();
        Assert(project.AssetDatabase.TryGetAsset("Assets/VisiblePlayer.obj", out AssetRecord? asset) &&
               asset != null, "Visible model fixture registered");
        var scene = new Scene("Model player");
        var state = new EditorState
        {
            EditorScene = scene,
            Project = project.Project,
            ProjectFilePath = project.ProjectFilePath
        };
        GameObject player = EditorSceneCommands.CreateModel(state, project, asset!,
            Vector3.Zero, new EditorLog());
        BlueprintAuthoringService.SetupPlayablePlayer(player, project.Assets);
        Assert(player.GetComponent<CapsuleCollider3D>() is { } capsule &&
               capsule.VisualBounds.Y > 1.6f && capsule.VisualBounds.Y < 2f,
            "Model-to-player preset scales render geometry to a visible human height");
        string blueprintPath = Path.Combine(project.ProjectRoot, "Assets", "VisiblePlayer.byteblueprint");
        BlueprintPromotionResult result = BlueprintPromotionService.Promote(project, player, blueprintPath);
        Assert(result.Blueprint.Type == BlueprintType.Character &&
               ReferenceEquals(player.GetComponent<BlueprintInstance>(), result.Instance),
            "Model-to-player workflow produces a Character Blueprint and live scene instance");
    }

    private static void TestRaycastAndCombat()
    {
        var scene = new Scene("Raycast events");
        GameObject source = scene.CreateGameObject("Gun");
        GameObject owner = scene.CreateGameObject("Player");
        owner.AddComponent(new CharacterController3D());
        owner.AddComponent(new BoxCollider3D { Size = Vector3.One * 2f });
        source.SetParent(owner, false);
        GameObject target = scene.CreateGameObject("Target");
        target.Transform.WorldPosition = new Vector3(0f, 0f, -5f);
        target.AddComponent(new BoxCollider3D { Size = Vector3.One });
        HealthComponent health = target.AddComponent(new HealthComponent());
        var context = new EventExecutionContext
        {
            Globals = new VariableStore(),
            Scene = scene,
            Self = source
        };
        VisualLogicRegistry registry = VisualLogicRegistry.CreateDefault();
        var cast = new VisualInstruction
        {
            Id = "physics.rayHitsAnything",
            Arguments = new Dictionary<string, EventValue>
            {
                ["source"] = EventValue.String("Self"),
                ["direction"] = EventValue.Vector3(-Vector3.UnitZ),
                ["distance"] = EventValue.Number(20)
            }
        };
        Assert(registry.TryGetCondition(cast.Id, out VisualConditionDefinition? ray) &&
               ray!.Evaluate(cast, context) && context.LastRaycastHit?.GameObject == target,
            "Ray Hits Anything records the object struck");
        Assert(registry.TryGetCondition("physics.lastRayHit", out VisualConditionDefinition? hitCondition) &&
               hitCondition!.Evaluate(new VisualInstruction(), context),
            "Later conditions can test the last raycast");
        registry.TryGetAction("physics.saveRayHit", out VisualActionDefinition? save);
        save!.Execute(new VisualInstruction
        {
            Arguments = new Dictionary<string, EventValue> { ["prefix"] = EventValue.String("Shot") }
        }, context);
        Assert(source.Variables["ShotHit"].Boolean &&
               source.Variables["ShotPoint"].Vector3.Z < -4f &&
               source.Variables["ShotObjectId"].String == target.Id.ToString(),
            "Raycast point, object, normal and distance are available to Event Sheet variables");
        registry.TryGetAction("health.damage", out VisualActionDefinition? damage);
        damage!.Execute(new VisualInstruction
        {
            Arguments = new Dictionary<string, EventValue>
            {
                ["target"] = EventValue.String("Last Ray Hit"),
                ["amount"] = EventValue.Number(25)
            }
        }, context);
        Assert(MathF.Abs(health.CurrentHealth - 75f) < .001f,
            "Existing Damage Object action can target Last Ray Hit");
        cast.Arguments["direction"] = EventValue.Vector3(Vector3.UnitZ);
        Assert(!ray!.Evaluate(cast, context) && context.LastRaycastHit == null,
            "A missed ray clears its previous hit");
    }

    private static void TestProjectileHit()
    {
        var scene = new Scene("Playable weapon hit");
        GameObject player = scene.CreateGameObject("Player");
        ProjectileLauncher3D launcher = player.AddComponent(new ProjectileLauncher3D
        {
            ProjectileSpeed = 60f, Damage = 20f, FireCooldown = 0f
        });
        GameObject target = scene.CreateGameObject("Target");
        target.Transform.WorldPosition = new Vector3(0f, 0f, -5f);
        target.AddComponent(new BoxCollider3D { Size = Vector3.One });
        HealthComponent health = target.AddComponent(new HealthComponent());
        scene.LoadInternal();
        Assert(launcher.Fire(-Vector3.UnitZ) != null, "Playable weapon spawns a visible projectile");
        ByteEngine.Core.Time.Update(.1);
        scene.UpdateInternal();
        Assert(health.CurrentHealth < 100f, "Projectile hit applies configured damage to a target");
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + name);
    }
}
