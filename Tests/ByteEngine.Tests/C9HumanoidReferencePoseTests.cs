using System.Numerics;
using System.Runtime.CompilerServices;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Tests;

internal static class C9HumanoidReferencePoseTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        VerifyReferencePoseCapture();
        VerifyInvalidInverseBindIsRejected();
    }

    private static void VerifyReferencePoseCapture()
    {
        var skeleton =
            new SkeletonAsset
            {
                Name =
                    "Reference Pose Test"
            };

        var mapping =
            new HumanoidBoneMap();

        int index =
            0;

        foreach (HumanoidBone semantic
                 in HumanoidBoneCatalog.Required)
        {
            string sourceName =
                $"Source_{semantic}";

            Vector3 expectedPosition =
                new(
                    index * 0.25f,
                    index * 0.5f,
                    -index * 0.125f);

            Matrix4x4 modelSpace =
                Matrix4x4.CreateTranslation(
                    expectedPosition);

            Assert(
                Matrix4x4.Invert(
                    modelSpace,
                    out Matrix4x4 inverseBind),
                "C9C: test bind matrix could not be inverted.");

            skeleton.Bones.Add(
                new Bone
                {
                    Name =
                        sourceName,

                    ParentIndex =
                        index - 1,

                    BindPose =
                        inverseBind
                });

            mapping.SetBone(
                semantic,
                sourceName);

            index++;
        }

        HumanoidReferencePose pose =
            HumanoidReferencePose.Capture(
                skeleton,
                mapping);

        Assert(
            pose.IsReady,
            $"C9C: complete Humanoid reference pose did not validate. Missing: {string.Join(", ", pose.MissingRequiredBones)}");

        HumanoidReferenceBonePose? hips =
            pose.GetBone(
                HumanoidBone.Hips);

        Assert(
            hips !=
            null,
            "C9C: Hips reference pose was not captured.");

        int hipsIndex =
            HumanoidBoneCatalog.Required
                .ToList()
                .IndexOf(
                    HumanoidBone.Hips);

        Vector3 expectedHips =
            new(
                hipsIndex * 0.25f,
                hipsIndex * 0.5f,
                -hipsIndex * 0.125f);

        AssertNear(
            hips!.Position,
            expectedHips,
            0.0001f,
            "C9C: Hips model-space reference position was not reconstructed from inverse bind pose.");

        AssertNear(
            hips.Scale,
            Vector3.One,
            0.0001f,
            "C9C: reference-pose scale should remain one for the translation-only test bind pose.");
    }

    private static void VerifyInvalidInverseBindIsRejected()
    {
        var skeleton =
            new SkeletonAsset
            {
                Name =
                    "Invalid Bind Pose"
            };

        var mapping =
            new HumanoidBoneMap();

        int index =
            0;

        foreach (HumanoidBone semantic
                 in HumanoidBoneCatalog.Required)
        {
            string sourceName =
                semantic.ToString();

            skeleton.Bones.Add(
                new Bone
                {
                    Name =
                        sourceName,

                    ParentIndex =
                        index - 1,

                    BindPose =
                        semantic ==
                            HumanoidBone.Head
                            ? new Matrix4x4()
                            : Matrix4x4.Identity
                });

            mapping.SetBone(
                semantic,
                sourceName);

            index++;
        }

        HumanoidReferencePose pose =
            HumanoidReferencePose.Capture(
                skeleton,
                mapping);

        Assert(
            !pose.IsReady,
            "C9C: singular inverse bind matrix should make the Humanoid reference pose invalid.");

        Assert(
            pose.InvalidMappedBones.Contains(
                HumanoidBone.Head),
            "C9C: invalid Head inverse bind matrix was not reported.");

        Assert(
            pose.MissingRequiredBones.Contains(
                HumanoidBone.Head),
            "C9C: invalid required Head transform should also count as a missing captured reference transform.");
    }

    private static void AssertNear(
        Vector3 actual,
        Vector3 expected,
        float tolerance,
        string message)
    {
        if (Vector3.Distance(
                actual,
                expected) >
            tolerance)
        {
            throw new InvalidOperationException(
                $"{message} Expected {expected}, got {actual}.");
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
