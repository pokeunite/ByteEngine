using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Editor;
using ByteEngine.Editor.Panels;

namespace ByteEngine.Tests;

internal static class V081PolishTests
{
    public static void Run(string root, AssetDatabase database, AssetManager assets)
    {
        TestControlRotationAndCharacterModes();
        TestCameraControlRotationAndLagMath();
        TestCameraSerialization(root, database, assets);
        TestComponentMetadata();
        TestEventDisplayNames(root);
        TestResponsiveToolbar();
    }

    private static void TestControlRotationAndCharacterModes()
    {
        var scene = new Scene("Character Rotation");
        GameObject player = scene.CreateGameObject("Player");
        player.AddComponent(new ByteEngine.Core.Characters.CharacterController3D());
        PlayerController3D input = player.AddComponent(new PlayerController3D { TurnSpeed = 540f });

        input.SetControlRotation(90f, 200f, -40f, 65f);
        Assert(Near(input.ControlYaw, 90f) && Near(input.ControlPitch, 65f),
            "Control rotation stores yaw and clamps pitch");

        input.CharacterRotation = CharacterRotationMode.FaceMovement;
        player.Transform.EulerAngles = Vector3.Zero;
        input.UpdateCharacterRotation(Vector3.UnitX, 1f);
        Assert(MathF.Abs(PlayerController3D.DeltaAngle(player.Transform.EulerAngles.Y, -90f)) < .01f,
            "Face Movement rotates toward movement direction");

        input.CharacterRotation = CharacterRotationMode.FaceCamera;
        input.ControlYaw = -170f;
        player.Transform.EulerAngles = new Vector3(0f, 170f, 0f);
        input.TurnSpeed = 10f;
        input.UpdateCharacterRotation(-Vector3.UnitZ, 1f);
        Assert(MathF.Abs(PlayerController3D.DeltaAngle(170f, player.Transform.EulerAngles.Y)) <= 10.01f,
            "Face Camera uses shortest-yaw interpolation");

        input.CharacterRotation = CharacterRotationMode.Independent;
        Quaternion before = player.Transform.LocalRotation;
        input.ControlYaw = 45f;
        input.UpdateCharacterRotation(Vector3.UnitX, 1f);
        Assert(MathF.Abs(Quaternion.Dot(before, player.Transform.LocalRotation)) > .9999f,
            "Independent leaves character rotation unchanged");
    }

    private static void TestCameraControlRotationAndLagMath()
    {
        var scene = new Scene("Control Rotation Camera");
        GameObject player = scene.CreateGameObject("Player");
        PlayerController3D input = player.AddComponent(new PlayerController3D());
        CameraBoom3D boom = player.AddComponent(new CameraBoom3D
        {
            ArmLength = 5f,
            PivotHeight = 1.5f,
            PositionSmoothness = 0f,
            RotationSmoothness = 0f
        });
        GameObject cameraObject = scene.CreateGameObject("Camera");
        cameraObject.SetParent(player, false);
        cameraObject.AddComponent(new Camera3D());
        boom.CameraObjectId = cameraObject.Id;
        scene.LoadInternal();

        input.SetControlRotation(90f, 30f);
        boom.SnapToSocket();
        Vector3 pivot = player.Transform.WorldPosition + Vector3.UnitY * boom.PivotHeight;
        Assert(Near(cameraObject.Transform.WorldPosition,
                pivot + CameraBoom3D.CalculateOrbitVector(90f, 30f, 5f)),
            "Control Rotation changes boom target rotation");

        float oneStep = CameraBoom3D.SmoothAngle(0f, 90f, 12f, .1f);
        float manySteps = 0f;
        for (int index = 0; index < 10; index++)
            manySteps = CameraBoom3D.SmoothAngle(manySteps, 90f, 12f, .01f);
        Assert(Near(oneStep, manySteps, .001f) &&
            CameraBoom3D.CalculateLagSteps(.1f, true, 1f / 60f) > 1,
            "Camera lag is delta-time stable and substeps frame spikes");
    }

    private static void TestCameraSerialization(string root, AssetDatabase database, AssetManager assets)
    {
        var serializer = new SceneSerializer(new ComponentSerializer(root, database, assets));
        var scene = new Scene("Camera Settings");
        GameObject player = scene.CreateGameObject("Player");
        player.AddComponent(new PlayerController3D
        {
            CharacterRotation = CharacterRotationMode.FaceMovement,
            TurnSpeed = 720f,
            ControlYaw = 42f,
            ControlPitch = 18f
        });
        player.AddComponent(new CameraBoom3D
        {
            CameraLagEnabled = false,
            RotationLagEnabled = true,
            LagSubstepping = false,
            MaximumLagDistance = 2.25f,
            MaxLagTimeStep = .02f,
            CollisionReturnSpeed = 6f
        });

        Scene clone = serializer.CloneForRuntime(scene);
        PlayerController3D input = clone.FindGameObject("Player")!.GetComponent<PlayerController3D>()!;
        CameraBoom3D boom = clone.FindGameObject("Player")!.GetComponent<CameraBoom3D>()!;
        Assert(input.CharacterRotation == CharacterRotationMode.FaceMovement && Near(input.TurnSpeed, 720f) &&
            Near(input.ControlYaw, 42f) && Near(input.ControlPitch, 18f),
            "Player control rotation settings survive Play Mode cloning");
        Assert(!boom.CameraLagEnabled && boom.RotationLagEnabled && !boom.LagSubstepping &&
            Near(boom.MaximumLagDistance, 2.25f) && Near(boom.MaxLagTimeStep, .02f) &&
            Near(boom.CollisionReturnSpeed, 6f),
            "Camera lag settings survive Play Mode cloning");
    }

    private static void TestComponentMetadata()
    {
        Type[] visibleComponents = typeof(Component).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(Component).IsAssignableFrom(type))
            .ToArray();
        Assert(visibleComponents.All(type =>
                ComponentMetadataRegistry.RegisteredTypes.Contains(type) &&
                !string.IsNullOrWhiteSpace(ComponentMetadataRegistry.Get(type).Category)),
            "Every existing concrete component has category metadata");
        Assert(ComponentMetadataRegistry.Matches(typeof(CameraBoom3D), "third person") &&
            ComponentMetadataRegistry.Matches(typeof(PlayerController3D), "control yaw"),
            "Component search matches friendly names and keywords");
        Assert(ComponentMetadataRegistry.Get(typeof(CameraBoom3D)).Category == "Camera",
            "Metadata category resolves independently of Inspector code");
    }

    private static void TestEventDisplayNames(string root)
    {
        string path = Path.Combine(root, "Assets", "Named.byteevents");
        Guid id = Guid.NewGuid();
        var serializer = new EventModuleSerializer();
        var module = new EventModuleDefinition
        {
            Rules = { new EventRuleDefinition { Id = id, DisplayName = "Player Death" } }
        };
        serializer.Save(module, path);
        EventModuleDefinition loaded = serializer.Load(path);
        Assert(loaded.Rules[0].DisplayName == "Player Death" && loaded.Rules[0].Id == id,
            "Event DisplayName survives save/load without changing identity");

        loaded.Rules[0].DisplayName = "Restart Level";
        serializer.Save(loaded, path);
        EventModuleDefinition renamed = serializer.Load(path);
        Assert(renamed.Rules[0].DisplayName == "Restart Level" && renamed.Rules[0].Id == id,
            "Renaming an event preserves its internal ID");

        string legacyPath = Path.Combine(root, "Assets", "Legacy.byteevents");
        File.WriteAllText(legacyPath,
            $$"""{"id":"{{Guid.NewGuid()}}","version":1,"name":"Legacy","rules":[{"id":"{{id}}","enabled":true,"conditions":[],"actions":[],"subEvents":[]}]}""");
        EventModuleDefinition legacy = serializer.Load(legacyPath);
        Assert(legacy.Rules[0].DisplayName == "Event 1" && legacy.Rules[0].Id == id,
            "Legacy event without DisplayName receives a stable readable fallback");
    }

    private static void TestResponsiveToolbar()
    {
        ByteGraphToolbarLayout narrow = ByteGraphToolbarLayout.ForWidth(700f);
        ByteGraphToolbarLayout wide = ByteGraphToolbarLayout.ForWidth(1600f);
        Assert(narrow.CollapseSecondary && narrow.TraceOnSecondRow,
            "Constrained ByteGraph toolbar collapses secondary actions and preserves trace row");
        Assert(!wide.CollapseSecondary && !wide.TraceOnSecondRow,
            "Wide ByteGraph toolbar keeps primary graph controls and trace together");
    }

    private static bool Near(float value, float expected, float tolerance = .001f) =>
        MathF.Abs(value - expected) <= tolerance;

    private static bool Near(Vector3 value, Vector3 expected, float tolerance = .001f) =>
        Vector3.Distance(value, expected) <= tolerance;

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + name);
    }
}
