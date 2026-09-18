using System.Numerics;
using System.Runtime.CompilerServices;

using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;

namespace ByteEngine.Tests;

/// <summary>
/// TPS-B regressions for separating movement direction from character facing.
/// </summary>
internal static class TpsFacingModeRegressionTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        VerifyStandardTpsDefaults();
        VerifyFaceMovementUsesMovementDirection();
        VerifyFreeCameraOrbitDoesNotRotateIdleCharacter();
        VerifyFaceCameraActsAsStrafeAimMode();
        VerifyIndependentModeDoesNotRotateCharacter();
    }

    private static void VerifyStandardTpsDefaults()
    {
        var controller =
            new PlayerController3D();

        Assert(
            controller.CharacterRotation ==
                CharacterRotationMode.FaceMovement,
            "TPS-B: standard PlayerController3D must default to FaceMovement.");

        Assert(
            !controller.UseLocalOrientation,
            "TPS-B: standard PlayerController3D movement must default to camera/control-relative.");
    }

    private static void VerifyFaceMovementUsesMovementDirection()
    {
        GameObject player =
            CreatePlayer(
                out PlayerController3D controller);

        controller.CharacterRotation =
            CharacterRotationMode.FaceMovement;

        controller.TurnSpeed =
            1000f;

        controller.UpdateCharacterRotation(
            Vector3.UnitX,
            1f);

        AssertNearAngle(
            player.Transform.EulerAngles.Y,
            90f,
            "TPS-B: FaceMovement did not rotate toward movement direction.");
    }

    private static void VerifyFreeCameraOrbitDoesNotRotateIdleCharacter()
    {
        GameObject player =
            CreatePlayer(
                out PlayerController3D controller);

        player.Transform.EulerAngles =
            new Vector3(
                0f,
                25f,
                0f);

        controller.CharacterRotation =
            CharacterRotationMode.FaceMovement;

        controller.ControlYaw =
            120f;

        controller.UpdateCharacterRotation(
            Vector3.Zero,
            1f);

        AssertNearAngle(
            player.Transform.EulerAngles.Y,
            25f,
            "TPS-B: free camera orbit rotated an idle FaceMovement character.");
    }

    private static void VerifyFaceCameraActsAsStrafeAimMode()
    {
        GameObject player =
            CreatePlayer(
                out PlayerController3D controller);

        controller.CharacterRotation =
            CharacterRotationMode.FaceCamera;

        controller.ControlYaw =
            70f;

        controller.TurnSpeed =
            1000f;

        /*
         * Aim/strafe mode must follow control yaw even with no movement input.
         */
        controller.UpdateCharacterRotation(
            Vector3.Zero,
            1f);

        AssertNearAngle(
            player.Transform.EulerAngles.Y,
            70f,
            "TPS-B: FaceCamera did not follow control yaw while idle.");
    }

    private static void VerifyIndependentModeDoesNotRotateCharacter()
    {
        GameObject player =
            CreatePlayer(
                out PlayerController3D controller);

        player.Transform.EulerAngles =
            new Vector3(
                0f,
                -35f,
                0f);

        controller.CharacterRotation =
            CharacterRotationMode.Independent;

        controller.ControlYaw =
            100f;

        controller.UpdateCharacterRotation(
            Vector3.UnitX,
            1f);

        AssertNearAngle(
            player.Transform.EulerAngles.Y,
            -35f,
            "TPS-B: Independent mode unexpectedly rotated the character.");
    }

    private static GameObject CreatePlayer(
        out PlayerController3D controller)
    {
        var player =
            new GameObject(
                "TPS-B Player");

        controller =
            player.AddComponent(
                new PlayerController3D());

        return player;
    }

    private static void AssertNearAngle(
        float actual,
        float expected,
        string message)
    {
        float delta =
            MathF.Abs(
                PlayerController3D.DeltaAngle(
                    actual,
                    expected));

        if (delta > 0.01f)
        {
            throw new InvalidOperationException(
                $"{message} Actual={actual}, Expected={expected}");
        }
    }

    private static void Assert(
        bool condition,
        string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                message);
        }
    }
}
