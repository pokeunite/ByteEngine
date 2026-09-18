using System.Numerics;
using System.Runtime.CompilerServices;

using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Tests;

/// <summary>
/// TPS-D regressions for Unreal-style spring-arm collision behavior.
/// </summary>
internal static class TpsCameraCollisionRegressionTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        VerifyCollisionDefaults();
        VerifySphereRadiusIsNotSubtractedTwice();
        VerifyCameraProbeIgnoresTriggers();
    }

    private static void VerifyCollisionDefaults()
    {
        var boom =
            new CameraBoom3D();

        Assert(
            boom.EnableCameraCollision,
            "TPS-D: new CameraBoom3D rigs must default camera collision on.");

        Assert(
            boom.CollisionRadius > 0f,
            "TPS-D: camera collision probe radius must be positive.");

        Assert(
            boom.CollisionSafetyMargin >= 0f,
            "TPS-D: collision safety margin cannot be negative.");
    }

    private static void VerifySphereRadiusIsNotSubtractedTwice()
    {
        const float desired =
            5.0f;

        const float hitDistance =
            2.2f;

        const float safety =
            0.05f;

        float allowed =
            CameraBoom3D.CalculateCollisionLength(
                desired,
                hitDistance,
                safety);

        AssertNear(
            allowed,
            2.15f,
            .0001f,
            "TPS-D: sphere-cast radius appears to be subtracted twice.");
    }

    private static void VerifyCameraProbeIgnoresTriggers()
    {
        var scene =
            new Scene(
                "TPS-D Trigger Probe");

        GameObject player =
            scene.CreateGameObject(
                "Player");

        var boom =
            player.AddComponent(
                new CameraBoom3D
                {
                    ArmLength =
                        5f,

                    PivotHeight =
                        1.5f,

                    Yaw =
                        0f,

                    Pitch =
                        0f,

                    ShoulderOffset =
                        0f,

                    CollisionRadius =
                        .2f,

                    CollisionSafetyMargin =
                        .05f,

                    EnableCameraCollision =
                        true,

                    CameraLagEnabled =
                        false,

                    RotationLagEnabled =
                        false
                });

        GameObject cameraObject =
            scene.CreateGameObject(
                "Camera");

        cameraObject.SetParent(
            player,
            false);

        Camera3D camera =
            cameraObject.AddComponent(
                new Camera3D
                {
                    ActiveGameCamera =
                        true
                });

        boom.CameraObjectId =
            cameraObject.Id;

        scene.SetActiveCamera(
            camera);

        GameObject triggerWall =
            scene.CreateGameObject(
                "Trigger Wall");

        triggerWall.Transform.WorldPosition =
            new Vector3(
                0f,
                1.5f,
                2.5f);

        triggerWall.AddComponent(
            new BoxCollider3D
            {
                Size =
                    new Vector3(
                        4f,
                        4f,
                        .25f),

                IsTrigger =
                    true
            });

        scene.LoadInternal();

        /*
         * Trigger volumes should not collapse the camera boom.
         */
        AssertNear(
            boom.ActualArmLength,
            boom.ArmLength,
            .001f,
            "TPS-D: camera collision incorrectly treated a trigger as a blocking wall.");
    }

    private static void AssertNear(
        float actual,
        float expected,
        float epsilon,
        string message)
    {
        if (MathF.Abs(
                actual -
                expected) >
            epsilon)
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
