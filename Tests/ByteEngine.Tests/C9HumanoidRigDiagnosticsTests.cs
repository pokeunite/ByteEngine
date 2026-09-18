using System.Numerics;
using System.Runtime.CompilerServices;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Tests;

internal static class C9HumanoidRigDiagnosticsTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        VerifyCleanTPose();
        VerifyBrokenArmHierarchyIsReported();
        VerifyNonTPoseIsReported();
        VerifyIkHelperMappingIsReported();
    }

    private static void VerifyCleanTPose()
    {
        Fixture fixture =
            CreateFixture(
                armDrop:
                    0.0f,
                breakLeftArmHierarchy:
                    false);

        HumanoidRigDiagnosticReport report =
            HumanoidRigDiagnostics.Analyze(
                fixture.Skeleton,
                fixture.Mapping);

        Assert(
            report.Errors.Count ==
            0,
            $"C9G: clean Humanoid hierarchy produced errors: {string.Join(" | ", report.Errors)}");

        Assert(
            report.ApproximatelyTPose,
            $"C9G: clean test T-pose was not recognized. Warnings: {string.Join(" | ", report.Warnings)}");
    }

    private static void VerifyBrokenArmHierarchyIsReported()
    {
        Fixture fixture =
            CreateFixture(
                armDrop:
                    0.0f,
                breakLeftArmHierarchy:
                    true);

        HumanoidRigDiagnosticReport report =
            HumanoidRigDiagnostics.Analyze(
                fixture.Skeleton,
                fixture.Mapping);

        Assert(
            report.Errors.Any(
                error =>
                    error.Contains(
                        "LeftLowerArm",
                        StringComparison.Ordinal)),
            "C9G: broken left-arm hierarchy was not reported.");
    }

    private static void VerifyNonTPoseIsReported()
    {
        Fixture fixture =
            CreateFixture(
                armDrop:
                    1.3f,
                breakLeftArmHierarchy:
                    false);

        HumanoidRigDiagnosticReport report =
            HumanoidRigDiagnostics.Analyze(
                fixture.Skeleton,
                fixture.Mapping);

        Assert(
            !report.ApproximatelyTPose,
            "C9G: strongly lowered arms should not be accepted as a T-pose.");

        Assert(
            report.Warnings.Any(
                warning =>
                    warning.Contains(
                        "T-pose",
                        StringComparison.OrdinalIgnoreCase)),
            "C9G: non-T-pose reference pose did not produce a warning.");
    }

    private static void VerifyIkHelperMappingIsReported()
    {
        Fixture fixture =
            CreateFixture(
                armDrop:
                    0.0f,
                breakLeftArmHierarchy:
                    false);

        fixture.Skeleton.Bones.Add(
            new Bone
            {
                Name =
                    "HandIK.L",

                ParentIndex =
                    -1,

                BindPose =
                    Matrix4x4.Identity
            });

        fixture.Mapping.SetBone(
            HumanoidBone.LeftHand,
            "HandIK.L");

        HumanoidRigDiagnosticReport report =
            HumanoidRigDiagnostics.Analyze(
                fixture.Skeleton,
                fixture.Mapping);

        Assert(
            report.Warnings.Any(
                warning =>
                    warning.Contains(
                        "IK/control/helper",
                        StringComparison.OrdinalIgnoreCase)),
            "C9G1: IK helper bone assignment was not reported.");
    }

    private static Fixture CreateFixture(
        float armDrop,
        bool breakLeftArmHierarchy)
    {
        var skeleton =
            new SkeletonAsset
            {
                Name =
                    "Diagnostics Test"
            };

        var mapping =
            new HumanoidBoneMap();

        Add(
            HumanoidBone.Hips,
            -1,
            new Vector3(
                0.0f,
                1.0f,
                0.0f));

        int hips =
            skeleton.Bones.Count -
            1;

        Add(
            HumanoidBone.Spine,
            hips,
            new Vector3(
                0.0f,
                1.55f,
                0.0f));

        int spine =
            skeleton.Bones.Count -
            1;

        Add(
            HumanoidBone.Head,
            spine,
            new Vector3(
                0.0f,
                2.45f,
                0.0f));

        Add(
            HumanoidBone.LeftUpperArm,
            spine,
            new Vector3(
                -0.45f,
                2.0f,
                0.0f));

        int leftUpper =
            skeleton.Bones.Count -
            1;

        Add(
            HumanoidBone.LeftLowerArm,
            breakLeftArmHierarchy
                ? hips
                : leftUpper,
            new Vector3(
                -0.95f,
                2.0f -
                armDrop *
                0.5f,
                0.0f));

        int leftLower =
            skeleton.Bones.Count -
            1;

        Add(
            HumanoidBone.LeftHand,
            leftLower,
            new Vector3(
                -1.35f,
                2.0f -
                armDrop,
                0.0f));

        Add(
            HumanoidBone.RightUpperArm,
            spine,
            new Vector3(
                0.45f,
                2.0f,
                0.0f));

        int rightUpper =
            skeleton.Bones.Count -
            1;

        Add(
            HumanoidBone.RightLowerArm,
            rightUpper,
            new Vector3(
                0.95f,
                2.0f -
                armDrop *
                0.5f,
                0.0f));

        int rightLower =
            skeleton.Bones.Count -
            1;

        Add(
            HumanoidBone.RightHand,
            rightLower,
            new Vector3(
                1.35f,
                2.0f -
                armDrop,
                0.0f));

        Add(
            HumanoidBone.LeftUpperLeg,
            hips,
            new Vector3(
                -0.22f,
                0.85f,
                0.0f));

        int leftUpperLeg =
            skeleton.Bones.Count -
            1;

        Add(
            HumanoidBone.LeftLowerLeg,
            leftUpperLeg,
            new Vector3(
                -0.22f,
                0.4f,
                0.0f));

        int leftLowerLeg =
            skeleton.Bones.Count -
            1;

        Add(
            HumanoidBone.LeftFoot,
            leftLowerLeg,
            new Vector3(
                -0.22f,
                0.0f,
                0.12f));

        Add(
            HumanoidBone.RightUpperLeg,
            hips,
            new Vector3(
                0.22f,
                0.85f,
                0.0f));

        int rightUpperLeg =
            skeleton.Bones.Count -
            1;

        Add(
            HumanoidBone.RightLowerLeg,
            rightUpperLeg,
            new Vector3(
                0.22f,
                0.4f,
                0.0f));

        int rightLowerLeg =
            skeleton.Bones.Count -
            1;

        Add(
            HumanoidBone.RightFoot,
            rightLowerLeg,
            new Vector3(
                0.22f,
                0.0f,
                0.12f));

        return
            new Fixture(
                skeleton,
                mapping);

        void Add(
            HumanoidBone semantic,
            int parentIndex,
            Vector3 modelPosition)
        {
            string name =
                semantic.ToString();

            Matrix4x4 model =
                Matrix4x4.CreateTranslation(
                    modelPosition);

            if (!Matrix4x4.Invert(
                    model,
                    out Matrix4x4 inverseBind))
            {
                throw new InvalidOperationException(
                    "C9G test matrix could not be inverted.");
            }

            skeleton.Bones.Add(
                new Bone
                {
                    Name =
                        name,

                    ParentIndex =
                        parentIndex,

                    BindPose =
                        inverseBind
                });

            mapping.SetBone(
                semantic,
                name);
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

    private sealed record Fixture(
        SkeletonAsset Skeleton,
        HumanoidBoneMap Mapping);
}
