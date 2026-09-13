using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;

namespace ByteEngine.Tests;

internal static class TpsCameraSystemTests
{
    public static void Run(string root, AssetDatabase database, AssetManager assets)
    {
        TestActiveCameraOwnership();
        TestCameraBoomMathAndHierarchy();
        TestCameraCollision();
        TestDiagnostics();
        TestSerialization(root, database, assets);
    }

    private static void TestActiveCameraOwnership()
    {
        var scene = new Scene("Camera Ownership");
        Camera3D first = scene.CreateGameObject("First Camera").AddComponent(new Camera3D());
        Camera3D second = scene.CreateGameObject("Second Camera").AddComponent(new Camera3D());
        Assert(ReferenceEquals(scene.ActiveCamera, first), "First enabled camera is deterministic fallback");
        scene.SetActiveCamera(second);
        Assert(ReferenceEquals(scene.ActiveCamera, second) && second.ActiveGameCamera && !first.ActiveGameCamera,
            "Scene explicitly selects one active gameplay camera");
        scene.CreateGameObject("Later Camera").AddComponent(new Camera3D());
        Assert(ReferenceEquals(scene.ActiveCamera, second), "Adding cameras does not replace explicit active camera");
        second.Enabled = false;
        Assert(ReferenceEquals(scene.ActiveCamera, first), "Disabled active camera falls back cleanly");
        second.Enabled = true;
        scene.DestroyGameObject(second.GameObject);
        Assert(ReferenceEquals(scene.ActiveCamera, first), "Destroyed active camera falls back cleanly");
    }

    private static void TestCameraBoomMathAndHierarchy()
    {
        Scene scene = CreateRig(out GameObject player, out GameObject cameraObject, out CameraBoom3D boom, out _);
        scene.LoadInternal();
        Vector3 pivot = player.Transform.WorldPosition + Vector3.UnitY * boom.PivotHeight;
        Assert(Near(cameraObject.Transform.WorldPosition, pivot + new Vector3(0f, 0f, 5f)),
            "Child camera follows boom socket without mouse input");

        boom.Yaw = 90f;
        boom.Pitch = 0f;
        boom.SnapToSocket();
        Assert(Near(cameraObject.Transform.WorldPosition, pivot + new Vector3(-5f, 0f, 0f)),
            "Camera boom yaw orbits horizontally");

        boom.Pitch = 30f;
        boom.SnapToSocket();
        Assert(Near(cameraObject.Transform.WorldPosition,
            pivot + CameraBoom3D.CalculateOrbitVector(90f, 30f, 5f)), "Camera boom pitch orbits vertically");

        Vector3 translation = new(4f, 2f, -3f);
        player.Transform.WorldPosition += translation;
        boom.SnapToSocket();
        Assert(Near(cameraObject.Transform.WorldPosition,
            player.Transform.WorldPosition + Vector3.UnitY * boom.PivotHeight +
            CameraBoom3D.CalculateOrbitVector(90f, 30f, 5f)), "Translated character moves camera rig");

        boom.Pitch = 500f;
        Assert(Near(boom.Pitch, boom.MaxPitch), "Camera boom clamps pitch");
        boom.Pitch = 0f;
        boom.ArmLength = 8f;
        boom.ShoulderOffset = 1.25f;
        boom.Yaw = 0f;
        boom.SnapToSocket();
        Vector3 expected = player.Transform.WorldPosition + Vector3.UnitY * boom.PivotHeight + new Vector3(1.25f, 0f, 8f);
        Assert(Near(cameraObject.Transform.WorldPosition, expected), "Arm length and shoulder offset affect socket independently");
        Assert(ReferenceEquals(cameraObject.Parent, player), "Camera remains authored as player child");
    }

    private static void TestCameraCollision()
    {
        Scene scene = CreateRig(out _, out GameObject cameraObject, out CameraBoom3D boom, out _);
        GameObject wall = scene.CreateGameObject("Camera Wall");
        wall.Transform.WorldPosition = new Vector3(0f, 1.5f, 2.5f);
        wall.AddComponent(new BoxCollider3D { Size = new Vector3(4f, 4f, .25f) });
        boom.EnableCameraCollision = true;
        scene.LoadInternal();
        Assert(boom.ActualArmLength < boom.ArmLength && cameraObject.Transform.WorldPosition.Z < 2.5f,
            "Camera collision shortens boom before wall");
        scene.DestroyGameObject(wall);
        boom.SnapToSocket();
        Assert(Near(boom.ActualArmLength, boom.ArmLength) && Near(cameraObject.Transform.WorldPosition.Z, boom.ArmLength),
            "Camera boom restores desired length after obstacle disappears");
    }

    private static void TestDiagnostics()
    {
        Scene scene = CreateRig(out _, out _, out CameraBoom3D boom, out Camera3D camera);
        scene.LoadInternal();
        scene.SetActiveCamera(camera);
        boom.SnapToSocket();
        string dump = RuntimeDiagnostics.CreateDump(scene);
        string[] required =
        {
            "ActiveCamera", "CameraOwner", "MatchesActiveCamera", "Captured", "MouseDelta", "Yaw", "Pitch",
            "ArmLength", "PivotHeight", "ShoulderOffset", "DesiredSocketPosition", "ActualSocketPosition",
            "RootPosition", "RootRotation", "Position", "Rotation", "Forward", "FOV", "Collision.Enabled",
            "Collision.Hit", "Collision.HitObject", "Collision.DesiredLength", "Collision.ActualLength",
            "ControlYaw", "ControlPitch", "DesiredBoomYaw", "DesiredBoomPitch", "SmoothedBoomYaw",
            "SmoothedBoomPitch", "CharacterRotationMode", "DesiredCharacterYaw", "ActualCharacterYaw",
            "TurnSpeed", "CameraLagEnabled", "RotationLagEnabled", "LagSubsteps",
            "DesiredArmLength", "ActualArmLength"
        };
        Assert(required.All(dump.Contains) && dump.Contains("MatchesActiveCamera: True"),
            "Camera boom diagnostics identify active camera and complete rig state");

        string? routed = null;
        RuntimeDiagnostics.OutputSink = value => routed = value;
        RuntimeDiagnostics.Dump(scene);
        RuntimeDiagnostics.OutputSink = null;
        Assert(routed?.StartsWith("[Runtime Diagnostics]", StringComparison.Ordinal) == true,
            "Runtime diagnostics use host sink with visible prefix");
    }

    private static void TestSerialization(string root, AssetDatabase database, AssetManager assets)
    {
        var serializer = new SceneSerializer(new ComponentSerializer(root, database, assets));
        Scene scene = CreateRig(out GameObject player, out GameObject cameraObject, out CameraBoom3D boom, out Camera3D camera);
        boom.ArmLength = 7f;
        boom.PivotHeight = 1.75f;
        boom.Yaw = 27f;
        boom.Pitch = 18f;
        boom.MinPitch = -20f;
        boom.MaxPitch = 60f;
        boom.MouseSensitivityX = .2f;
        boom.MouseSensitivityY = .16f;
        boom.PositionSmoothness = 9f;
        boom.RotationSmoothness = 11f;
        boom.ShoulderOffset = .6f;
        boom.EnableCameraCollision = true;
        boom.CollisionRadius = .3f;
        scene.SetActiveCamera(camera);

        Scene clone = serializer.CloneForRuntime(scene);
        GameObject clonedPlayer = clone.FindGameObject("Player")!;
        GameObject clonedCamera = clone.FindGameObject("Main Camera")!;
        CameraBoom3D clonedBoom = clonedPlayer.GetComponent<CameraBoom3D>()!;
        Assert(clonedBoom.CameraObjectId == cameraObject.Id && Near(clonedBoom.ArmLength, 7f) &&
            Near(clonedBoom.PivotHeight, 1.75f) && Near(clonedBoom.Yaw, 27f) && Near(clonedBoom.Pitch, 18f) &&
            Near(clonedBoom.MinPitch, -20f) && Near(clonedBoom.MaxPitch, 60f) &&
            Near(clonedBoom.MouseSensitivityX, .2f) && Near(clonedBoom.MouseSensitivityY, .16f) &&
            Near(clonedBoom.PositionSmoothness, 9f) && Near(clonedBoom.RotationSmoothness, 11f) &&
            Near(clonedBoom.ShoulderOffset, .6f) && clonedBoom.EnableCameraCollision &&
            Near(clonedBoom.CollisionRadius, .3f), "CameraBoom3D serializes every editable property");
        Assert(ReferenceEquals(clonedCamera.Parent, clonedPlayer) && ReferenceEquals(clone.ActiveCamera, clonedCamera.GetComponent<Camera3D>()),
            "Camera hierarchy and active ownership survive Play Mode clone");

        var sceneData = serializer.Serialize(scene);
        var blueprint = new BlueprintDefinition
        {
            Name = "BP_Player",
            Type = BlueprintType.Character,
            Root = sceneData.GameObjects.Single(item => item.Id == player.Id),
            Children = sceneData.GameObjects.Where(item => item.Id != player.Id).ToList()
        };
        string path = Path.Combine(root, "Assets", "BP_Player_CameraRig.byteblueprint");
        var blueprintSerializer = new BlueprintSerializer();
        blueprintSerializer.Save(blueprint, path);
        BlueprintDefinition loaded = blueprintSerializer.Load(path);
        Assert(loaded.Root.Components.Any(item => item.Type == "CameraBoom3D") &&
            loaded.Children.Single().ParentId == loaded.Root.Id &&
            loaded.Children.Single().Components.Any(item => item.Type == "Camera3D"),
            "Blueprint boom and child camera hierarchy survive serialization");
    }

    private static Scene CreateRig(out GameObject player, out GameObject cameraObject,
        out CameraBoom3D boom, out Camera3D camera)
    {
        var scene = new Scene("TPS Camera Rig");
        player = scene.CreateGameObject("Player");
        player.Transform.WorldPosition = Vector3.Zero;
        player.AddComponent(new CharacterController3D());
        boom = player.AddComponent(new CameraBoom3D
        {
            ArmLength = 5f,
            PivotHeight = 1.5f,
            Yaw = 0f,
            Pitch = 0f,
            PositionSmoothness = 0f,
            RotationSmoothness = 0f
        });
        cameraObject = scene.CreateGameObject("Main Camera");
        cameraObject.SetParent(player, false);
        camera = cameraObject.AddComponent(new Camera3D { ActiveGameCamera = true });
        boom.CameraObjectId = cameraObject.Id;
        scene.SetActiveCamera(camera);
        return scene;
    }

    private static bool Near(Vector3 value, Vector3 expected) => Vector3.Distance(value, expected) < .001f;
    private static bool Near(float value, float expected) => Math.Abs(value - expected) < .001f;
    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + name);
    }
}
