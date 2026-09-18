using System.Numerics;
using System.Runtime.CompilerServices;

using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Tests;

/// <summary>
/// TPS-A regressions for the scene-wide LateUpdate phase and third-person
/// camera timing. These tests are asset-independent and run automatically with
/// the ByteEngine.Tests executable.
/// </summary>
internal static class TpsLateUpdateRegressionTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        VerifySceneWideLateUpdateOrder();
        VerifyCameraBoomRunsAfterCharacterTransformChanges();
    }

    private static void VerifySceneWideLateUpdateOrder()
    {
        var scene = new Scene("TPS-A LateUpdate Order");
        var events = new List<string>();

        scene.CreateGameObject("A")
            .AddComponent(new PhaseProbe("A", events));
        scene.CreateGameObject("B")
            .AddComponent(new PhaseProbe("B", events));

        scene.LoadInternal();
        scene.UpdateInternal();

        string actual = string.Join(",", events);
        const string expected = "A.Update,B.Update,A.LateUpdate,B.LateUpdate";

        Assert(
            actual == expected,
            $"TPS-A: expected scene-wide Update then LateUpdate passes. Expected '{expected}', got '{actual}'.");

        scene.UnloadInternal();
    }

    private static void VerifyCameraBoomRunsAfterCharacterTransformChanges()
    {
        var scene = new Scene("TPS-A Camera Timing");
        GameObject player = scene.CreateGameObject("Player");

        CameraBoom3D boom = player.AddComponent(new CameraBoom3D
        {
            UseControlRotation = false,
            ArmLength = 5f,
            PivotHeight = 1.5f,
            Yaw = 0f,
            Pitch = 0f,
            CameraLagEnabled = false,
            RotationLagEnabled = false,
            EnableCameraCollision = false
        });

        player.AddComponent(new RotateDuringUpdate(90f));

        GameObject cameraObject = scene.CreateGameObject("Main Camera");
        cameraObject.SetParent(player, false);
        Camera3D camera = cameraObject.AddComponent(new Camera3D
        {
            ActiveGameCamera = true
        });
        boom.CameraObjectId = cameraObject.Id;
        scene.SetActiveCamera(camera);

        scene.LoadInternal();
        scene.UpdateInternal();

        Vector3 expected =
            player.Transform.WorldPosition +
            Vector3.UnitY * boom.PivotHeight +
            CameraBoom3D.CalculateOrbitVector(0f, 0f, boom.ArmLength);

        Assert(
            Near(cameraObject.Transform.WorldPosition, expected),
            "TPS-A: CameraBoom3D must resolve its final socket after character transform changes in Update.");

        Assert(
            MathF.Abs(player.Transform.EulerAngles.Y - 90f) < .001f,
            "TPS-A regression setup did not rotate the player during Update.");

        scene.UnloadInternal();
    }

    private sealed class PhaseProbe : Component
    {
        private readonly string _name;
        private readonly List<string> _events;

        public PhaseProbe(string name, List<string> events)
        {
            _name = name;
            _events = events;
        }

        protected override void OnUpdate() =>
            _events.Add(_name + ".Update");

        protected override void OnLateUpdate() =>
            _events.Add(_name + ".LateUpdate");
    }

    private sealed class RotateDuringUpdate : Component
    {
        private readonly float _yaw;

        public RotateDuringUpdate(float yaw)
        {
            _yaw = yaw;
        }

        protected override void OnUpdate()
        {
            Vector3 euler = Transform.EulerAngles;
            euler.Y = _yaw;
            Transform.EulerAngles = euler;
        }
    }

    private static bool Near(Vector3 value, Vector3 expected) =>
        Vector3.Distance(value, expected) < .001f;

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
