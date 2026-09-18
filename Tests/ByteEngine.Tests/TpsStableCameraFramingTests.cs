using System.Numerics;
using System.Runtime.CompilerServices;

using ByteEngine.Core.Gameplay;

namespace ByteEngine.Tests;

/// <summary>
/// TPS-C regressions for stable third-person camera framing.
/// </summary>
internal static class TpsStableCameraFramingTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        VerifyStableTpsDefaults();
        VerifyViewForwardMatchesInverseOrbit();
        VerifyCameraRightMatchesYawConvention();
        VerifyShoulderOffsetDoesNotChangeViewDirection();
    }

    private static void VerifyStableTpsDefaults()
    {
        var boom =
            new CameraBoom3D();

        Assert(
            !boom.CameraLagEnabled,
            "TPS-C: standard TPS position lag must default off.");

        Assert(
            !boom.RotationLagEnabled,
            "TPS-C: standard TPS rotation lag must default off.");
    }

    private static void VerifyViewForwardMatchesInverseOrbit()
    {
        const float yaw =
            37f;

        const float pitch =
            21f;

        Vector3 orbit =
            CameraBoom3D.CalculateOrbitVector(
                yaw,
                pitch,
                1f);

        Vector3 forward =
            CameraBoom3D.CalculateViewForward(
                yaw,
                pitch);

        AssertNear(
            forward,
            -Vector3.Normalize(orbit),
            .0001f,
            "TPS-C: camera forward must be the inverse of the boom orbit direction.");
    }

    private static void VerifyCameraRightMatchesYawConvention()
    {
        const float yaw =
            45f;

        Vector3 forward =
            CameraBoom3D.CalculateViewForward(
                yaw,
                0f);

        Vector3 right =
            CameraBoom3D.CalculateCameraRight(
                yaw);

        Vector3 expected =
            Vector3.Normalize(
                Vector3.Cross(
                    forward,
                    Vector3.UnitY));

        AssertNear(
            right,
            expected,
            .0001f,
            "TPS-C: camera right does not match the boom yaw convention.");

        Assert(
            MathF.Abs(
                Vector3.Dot(
                    forward,
                    right)) <
            .0001f,
            "TPS-C: shoulder-right vector must remain perpendicular to camera forward.");
    }

    private static void VerifyShoulderOffsetDoesNotChangeViewDirection()
    {
        /*
         * This regression protects the architectural rule: ShoulderOffset is
         * composition/socket placement. View direction comes from yaw/pitch.
         */
        Vector3 before =
            CameraBoom3D.CalculateViewForward(
                65f,
                -12f);

        var boom =
            new CameraBoom3D
            {
                ShoulderOffset =
                    1.25f
            };

        Vector3 after =
            CameraBoom3D.CalculateViewForward(
                65f,
                -12f);

        Assert(
            MathF.Abs(boom.ShoulderOffset - 1.25f) < .0001f,
            "TPS-C: shoulder-offset test setup failed.");

        AssertNear(
            before,
            after,
            .0001f,
            "TPS-C: shoulder framing changed camera view direction.");
    }

    private static void AssertNear(
        Vector3 actual,
        Vector3 expected,
        float epsilon,
        string message)
    {
        if (Vector3.Distance(
                actual,
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
