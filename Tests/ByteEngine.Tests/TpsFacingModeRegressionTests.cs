using System.Numerics;
using System.Runtime.CompilerServices;

using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;

namespace ByteEngine.Tests;

/// <summary>
/// TPS-B/E regressions for movement-facing, control/transform yaw conversion,
/// and idle turn-in-place behavior.
/// </summary>
internal static class TpsFacingModeRegressionTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        VerifyStandardTpsDefaults();
        VerifyFaceMovementUsesWorldMovementDirection();
        VerifyIdleFreeOrbitWindow();
        VerifyIdleTurnInPlaceCatchesCamera();
        VerifyFaceCameraActsAsStrafeAimMode();
        VerifyIndependentModeDoesNotRotateCharacter();
    }

    private static void VerifyStandardTpsDefaults()
    {
        var controller = new PlayerController3D();

        Assert(
            controller.CharacterRotation == CharacterRotationMode.FaceMovement,
            "TPS-E: standard PlayerController3D must default to FaceMovement.");

        Assert(
            !controller.UseLocalOrientation,
            "TPS-E: standard movement must default to camera/control-relative.");

        AssertNear(controller.IdleTurnStartAngle, 60f,
            "TPS-E: idle turn start angle default changed.");
        AssertNear(controller.IdleTurnFinishAngle, 5f,
            "TPS-E: idle turn finish angle default changed.");
        AssertNear(controller.IdleTurnSpeed, 300f,
            "TPS-E: idle turn speed default changed.");
    }

    private static void VerifyFaceMovementUsesWorldMovementDirection()
    {
        GameObject player = CreatePlayer(out PlayerController3D controller);
        controller.CharacterRotation = CharacterRotationMode.FaceMovement;
        controller.TurnSpeed = 1000f;

        controller.UpdateCharacterRotation(Vector3.UnitX, 1f);

        AssertNearAngle(
            player.Transform.EulerAngles.Y,
            -90f,
            "TPS-E: world +X requires Transform yaw -90 with ByteEngine's yaw convention.");

        AssertNearDirection(
            player.Transform.Forward,
            Vector3.UnitX,
            "TPS-E: FaceMovement Euler yaw did not produce the requested world-facing direction.");
    }

    private static void VerifyIdleFreeOrbitWindow()
    {
        GameObject player = CreatePlayer(out PlayerController3D controller);
        player.Transform.EulerAngles = new Vector3(0f, 25f, 0f);
        controller.CharacterRotation = CharacterRotationMode.FaceMovement;
        controller.ControlYaw = -50f; // camera-facing Transform yaw = +50; delta = 25

        controller.UpdateCharacterRotation(Vector3.Zero, 1f);

        AssertNearAngle(
            player.Transform.EulerAngles.Y,
            25f,
            "TPS-E: idle character rotated while camera remained inside the free-orbit window.");
    }

    private static void VerifyIdleTurnInPlaceCatchesCamera()
    {
        GameObject player = CreatePlayer(out PlayerController3D controller);
        controller.CharacterRotation = CharacterRotationMode.FaceMovement;
        controller.ControlYaw = 90f;
        controller.IdleTurnSpeed = 300f;

        controller.UpdateCharacterRotation(Vector3.Zero, 1f);

        AssertNearAngle(
            player.Transform.EulerAngles.Y,
            -90f,
            "TPS-E: idle turn-in-place did not convert control yaw to Transform yaw.");

        AssertNearDirection(
            player.Transform.Forward,
            PlayerController3D.ForwardFromYaw(90f),
            "TPS-E: idle turn-in-place did not end facing the camera/control heading.");
    }

    private static void VerifyFaceCameraActsAsStrafeAimMode()
    {
        GameObject player = CreatePlayer(out PlayerController3D controller);
        controller.CharacterRotation = CharacterRotationMode.FaceCamera;
        controller.ControlYaw = 70f;
        controller.TurnSpeed = 1000f;

        controller.UpdateCharacterRotation(Vector3.Zero, 1f);

        AssertNearAngle(
            player.Transform.EulerAngles.Y,
            -70f,
            "TPS-E: FaceCamera did not convert control yaw to Transform yaw.");

        AssertNearDirection(
            player.Transform.Forward,
            PlayerController3D.ForwardFromYaw(70f),
            "TPS-E: FaceCamera body forward does not match control heading.");
    }

    private static void VerifyIndependentModeDoesNotRotateCharacter()
    {
        GameObject player = CreatePlayer(out PlayerController3D controller);
        player.Transform.EulerAngles = new Vector3(0f, -35f, 0f);
        controller.CharacterRotation = CharacterRotationMode.Independent;
        controller.ControlYaw = 100f;

        controller.UpdateCharacterRotation(Vector3.UnitX, 1f);

        AssertNearAngle(
            player.Transform.EulerAngles.Y,
            -35f,
            "TPS-E: Independent mode unexpectedly rotated the character.");
    }

    private static GameObject CreatePlayer(out PlayerController3D controller)
    {
        var player = new GameObject("TPS-E Player");
        controller = player.AddComponent(new PlayerController3D());
        return player;
    }

    private static void AssertNearAngle(float actual, float expected, string message)
    {
        float delta = MathF.Abs(PlayerController3D.DeltaAngle(actual, expected));
        if (delta > 0.01f)
            throw new InvalidOperationException($"{message} Actual={actual}, Expected={expected}");
    }

    private static void AssertNearDirection(Vector3 actual, Vector3 expected, string message)
    {
        Vector3 a = Vector3.Normalize(new Vector3(actual.X, 0f, actual.Z));
        Vector3 e = Vector3.Normalize(new Vector3(expected.X, 0f, expected.Z));
        if (Vector3.Dot(a, e) < 0.9999f)
            throw new InvalidOperationException($"{message} Actual={actual}, Expected={expected}");
    }

    private static void AssertNear(float actual, float expected, string message)
    {
        if (MathF.Abs(actual - expected) > 0.001f)
            throw new InvalidOperationException($"{message} Actual={actual}, Expected={expected}");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
