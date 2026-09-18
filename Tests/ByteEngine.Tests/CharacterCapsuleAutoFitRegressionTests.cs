using System.Numerics;
using System.Runtime.CompilerServices;

using ByteEngine.Core.Characters;
using ByteEngine.Core.Scene;
using ByteEngine.Editor;

namespace ByteEngine.Tests;

internal static class CharacterCapsuleAutoFitRegressionTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        VerifyDefaultCapsuleUsesFeetOrigin();
        VerifyTPoseArmSpanDoesNotControlRadius();
        VerifyAutoFitBottomMatchesVisualFeet();
    }

    private static void VerifyDefaultCapsuleUsesFeetOrigin()
    {
        var capsule =
            new CapsuleCollider3D();

        AssertNear(
            capsule.Center.Y,
            1f,
            "Character capsule: default 2m capsule must be centered one metre above a feet-origin root.");

        AssertNear(
            capsule.Center.Y -
            capsule.Height * .5f,
            0f,
            "Character capsule: default capsule bottom must sit at the character root/feet.");
    }

    private static void VerifyTPoseArmSpanDoesNotControlRadius()
    {
        var root =
            new GameObject(
                "T-Pose Character");

        CharacterCapsuleFitResult fit =
            CharacterCapsuleAutoFit.FitBounds(
                root,
                new Vector3(
                    -1.0f,
                    0f,
                    -.22f),
                new Vector3(
                    1.0f,
                    1.8f,
                    .22f),
                "Synthetic T-pose");

        /*
         * Old max-horizontal fitting would produce a radius over one metre from
         * the two metre arm span. Body fitting should stay around torso depth.
         */
        Assert(
            fit.CapsuleRadius <
            .35f,
            $"Character capsule: T-pose arm span inflated radius to {fit.CapsuleRadius:0.###}m.");

        Assert(
            fit.CapsuleRadius >
            .15f,
            $"Character capsule: body radius became unrealistically small ({fit.CapsuleRadius:0.###}m).");
    }

    private static void VerifyAutoFitBottomMatchesVisualFeet()
    {
        var root =
            new GameObject(
                "Offset Character");

        Vector3 minimum =
            new(
                -.3f,
                -.12f,
                -.24f);

        Vector3 maximum =
            new(
                .3f,
                1.68f,
                .24f);

        CharacterCapsuleFitResult fit =
            CharacterCapsuleAutoFit.FitBounds(
                root,
                minimum,
                maximum,
                "Synthetic offset character");

        float bottom =
            fit.CapsuleCenter.Y -
            fit.CapsuleHeight *
            .5f;

        AssertNear(
            bottom,
            minimum.Y,
            "Character capsule: auto-fit must anchor the physical capsule bottom to the visual foot minimum.");
    }

    private static void AssertNear(
        float actual,
        float expected,
        string message)
    {
        if (MathF.Abs(
                actual -
                expected) >
            .0001f)
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
