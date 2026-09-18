using System.Numerics;
using System.Runtime.CompilerServices;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Tests;

/// <summary>
/// Regression for valid Humanoid rigs whose model-space facing axes differ.
///
/// A 180-degree Y-rotated source avatar has anatomical forward/right expressed
/// on the opposite model axes. Retargeting must normalize through Humanoid space
/// before applying rotation or root-motion deltas to the target.
/// </summary>
internal static class C9HumanoidAvatarFrameTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        VerifyOppositeFacingAvatarRotationAxisIsNormalized();
        VerifyOppositeFacingAvatarTranslationIsNormalized();
    }

    private static void VerifyOppositeFacingAvatarRotationAxisIsNormalized()
    {
        HumanoidReferencePose source =
            CreateReferencePose(
                rotate180Y:
                    true);

        HumanoidReferencePose target =
            CreateReferencePose(
                rotate180Y:
                    false);

        /*
         * Source anatomical +Z forward is model-space -Z after the 180-degree
         * avatar rotation. The same anatomical rotation must become +Z on target.
         */
        Quaternion sourceDelta =
            Quaternion.CreateFromAxisAngle(
                -Vector3.UnitZ,
                MathF.PI /
                2.0f);

        Quaternion targetDelta =
            HumanoidRetargeter.RemapRotationDeltaForAvatarFrames(
                source,
                target,
                sourceDelta);

        Vector3 rotatedRight =
            Vector3.Transform(
                Vector3.UnitX,
                targetDelta);

        AssertNear(
            rotatedRight,
            Vector3.UnitY,
            0.01f,
            "C9N: opposite-facing source rotation axis was not normalized into target Humanoid space.");
    }

    private static void VerifyOppositeFacingAvatarTranslationIsNormalized()
    {
        HumanoidReferencePose source =
            CreateReferencePose(
                rotate180Y:
                    true);

        HumanoidReferencePose target =
            CreateReferencePose(
                rotate180Y:
                    false);

        Vector3 remapped =
            HumanoidRetargeter.RemapVectorForAvatarFrames(
                source,
                target,
                -Vector3.UnitZ);

        AssertNear(
            remapped,
            Vector3.UnitZ,
            0.001f,
            "C9N: opposite-facing source forward vector was not normalized into target Humanoid space.");
    }

    private static HumanoidReferencePose CreateReferencePose(
        bool rotate180Y)
    {
        var skeleton =
            new SkeletonAsset
            {
                Name =
                    rotate180Y
                        ? "Source Reversed"
                        : "Target Forward"
            };

        var mapping =
            new HumanoidBoneMap();

        var positions =
            new Dictionary<HumanoidBone, Vector3>
            {
                [HumanoidBone.Hips] =
                    new(0.0f, 1.0f, 0.0f),

                [HumanoidBone.Spine] =
                    new(0.0f, 1.5f, 0.0f),

                [HumanoidBone.Head] =
                    new(0.0f, 2.5f, 0.0f),

                [HumanoidBone.LeftUpperArm] =
                    new(-0.6f, 2.0f, 0.0f),

                [HumanoidBone.LeftLowerArm] =
                    new(-1.1f, 2.0f, 0.0f),

                [HumanoidBone.LeftHand] =
                    new(-1.5f, 2.0f, 0.0f),

                [HumanoidBone.RightUpperArm] =
                    new(0.6f, 2.0f, 0.0f),

                [HumanoidBone.RightLowerArm] =
                    new(1.1f, 2.0f, 0.0f),

                [HumanoidBone.RightHand] =
                    new(1.5f, 2.0f, 0.0f),

                [HumanoidBone.LeftUpperLeg] =
                    new(-0.25f, 0.8f, 0.0f),

                [HumanoidBone.LeftLowerLeg] =
                    new(-0.25f, 0.35f, 0.0f),

                [HumanoidBone.LeftFoot] =
                    new(-0.25f, 0.0f, 0.2f),

                [HumanoidBone.RightUpperLeg] =
                    new(0.25f, 0.8f, 0.0f),

                [HumanoidBone.RightLowerLeg] =
                    new(0.25f, 0.35f, 0.0f),

                [HumanoidBone.RightFoot] =
                    new(0.25f, 0.0f, 0.2f)
            };

        Quaternion avatarRotation =
            rotate180Y
                ? Quaternion.CreateFromAxisAngle(
                    Vector3.UnitY,
                    MathF.PI)
                : Quaternion.Identity;

        foreach ((HumanoidBone semantic, Vector3 basePosition)
                 in positions)
        {
            Vector3 position =
                Vector3.Transform(
                    basePosition,
                    avatarRotation);

            Matrix4x4 model =
                Matrix4x4.CreateTranslation(
                    position);

            if (!Matrix4x4.Invert(
                    model,
                    out Matrix4x4 inverseBind))
            {
                throw new InvalidOperationException(
                    "C9N: test bind matrix could not be inverted.");
            }

            skeleton.Bones.Add(
                new Bone
                {
                    Name =
                        semantic.ToString(),

                    ParentIndex =
                        -1,

                    BindPose =
                        inverseBind
                });

            mapping.SetBone(
                semantic,
                semantic.ToString());
        }

        HumanoidReferencePose pose =
            HumanoidReferencePose.Capture(
                skeleton,
                mapping);

        if (!pose.IsReady)
        {
            throw new InvalidOperationException(
                "C9N: test Humanoid reference pose was not ready.");
        }

        return pose;
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
}
