using System.Numerics;
using System.Runtime.CompilerServices;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Tests;

internal static class C9HumanoidRetargeterTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        VerifyRotationTransfersToDifferentTargetProportions();
        VerifyHipsTranslationScalesWithCharacterSize();
        VerifyLoopTimeWraps();
    }

    private static void VerifyRotationTransfersToDifferentTargetProportions()
    {
        RigFixture source =
            CreateHumanoidFixture(
                1.0f);

        RigFixture target =
            CreateHumanoidFixture(
                2.0f);

        ImportedAnimation clip =
            CreateClip(
                source,
                hipsEndTranslation:
                    Vector3.Zero,
                leftArmEndRotation:
                    Quaternion.CreateFromAxisAngle(
                        Vector3.UnitZ,
                        MathF.PI /
                        2.0f));

        HumanoidRetargetPose pose =
            HumanoidRetargeter.Retarget(
                source.Skeleton,
                source.Mapping,
                source.ReferencePose,
                clip,
                1.0f,
                target.Skeleton,
                target.Mapping,
                target.ReferencePose,
                loop:
                    false);

        int targetArmIndex =
            target.IndexOf(
                HumanoidBone.LeftUpperArm);

        Assert(
            Matrix4x4.Decompose(
                pose.LocalMatrices[targetArmIndex],
                out Vector3 targetScale,
                out Quaternion targetRotation,
                out Vector3 targetTranslation),
            "C9D: retargeted left-arm matrix could not be decomposed.");

        Vector3 rotatedRight =
            Vector3.Transform(
                Vector3.UnitX,
                targetRotation);

        AssertNear(
            rotatedRight,
            Vector3.UnitY,
            0.01f,
            "C9D: 90-degree source arm rotation was not transferred to target.");

        Vector3 targetReferenceTranslation =
            GetLocalTranslation(
                target,
                targetArmIndex);

        AssertNear(
            targetTranslation,
            targetReferenceTranslation,
            0.0001f,
            "C9D: target arm bone length/local translation should remain target-authored.");

        Vector3 targetReferenceScale =
            GetLocalScale(
                target,
                targetArmIndex);

        AssertNear(
            targetScale,
            targetReferenceScale,
            0.0001f,
            "C9D: target arm scale should remain target-authored.");
    }

    private static void VerifyHipsTranslationScalesWithCharacterSize()
    {
        RigFixture source =
            CreateHumanoidFixture(
                1.0f);

        RigFixture target =
            CreateHumanoidFixture(
                2.0f);

        ImportedAnimation clip =
            CreateClip(
                source,
                hipsEndTranslation:
                    new Vector3(
                        1.0f,
                        0.0f,
                        0.0f),
                leftArmEndRotation:
                    Quaternion.Identity);

        HumanoidRetargetPose pose =
            HumanoidRetargeter.Retarget(
                source.Skeleton,
                source.Mapping,
                source.ReferencePose,
                clip,
                1.0f,
                target.Skeleton,
                target.Mapping,
                target.ReferencePose,
                loop:
                    false);

        int targetHipsIndex =
            target.IndexOf(
                HumanoidBone.Hips);

        Assert(
            Matrix4x4.Decompose(
                pose.LocalMatrices[targetHipsIndex],
                out _,
                out _,
                out Vector3 hipsTranslation),
            "C9D: retargeted hips matrix could not be decomposed.");

        Vector3 targetReferenceHips =
            GetLocalTranslation(
                target,
                targetHipsIndex);

        /*
         * The fixture's target body is exactly twice the size of the source.
         * A +1 source Hips displacement should therefore become +2.
         */
        AssertNear(
            hipsTranslation,
            targetReferenceHips +
            new Vector3(
                2.0f,
                0.0f,
                0.0f),
            0.02f,
            "C9D: Hips translation did not scale to target Humanoid proportions.");

        AssertNear(
            pose.TranslationScale,
            2.0f,
            0.02f,
            "C9D: target/source Humanoid body scale ratio was incorrect.");
    }

    private static void VerifyLoopTimeWraps()
    {
        RigFixture source =
            CreateHumanoidFixture(
                1.0f);

        RigFixture target =
            CreateHumanoidFixture(
                1.0f);

        ImportedAnimation clip =
            CreateClip(
                source,
                hipsEndTranslation:
                    new Vector3(
                        1.0f,
                        0.0f,
                        0.0f),
                leftArmEndRotation:
                    Quaternion.Identity);

        HumanoidRetargetPose pose =
            HumanoidRetargeter.Retarget(
                source.Skeleton,
                source.Mapping,
                source.ReferencePose,
                clip,
                2.25f,
                target.Skeleton,
                target.Mapping,
                target.ReferencePose,
                loop:
                    true);

        AssertNear(
            pose.SourceTime,
            0.25f,
            0.0001f,
            "C9D: looping source animation time did not wrap correctly.");
    }

    private static ImportedAnimation CreateClip(
        RigFixture source,
        Vector3 hipsEndTranslation,
        Quaternion leftArmEndRotation)
    {
        string hipsName =
            source.Mapping.GetBoneName(
                HumanoidBone.Hips)
            ?? throw new InvalidOperationException(
                "Test Hips mapping missing.");

        string leftArmName =
            source.Mapping.GetBoneName(
                HumanoidBone.LeftUpperArm)
            ?? throw new InvalidOperationException(
                "Test LeftUpperArm mapping missing.");

        int hipsIndex =
            source.IndexOf(
                HumanoidBone.Hips);

        int armIndex =
            source.IndexOf(
                HumanoidBone.LeftUpperArm);

        Vector3 hipsReference =
            GetLocalTranslation(
                source,
                hipsIndex);

        Quaternion armReference =
            GetLocalRotation(
                source,
                armIndex);

        return
            new ImportedAnimation
            {
                Name =
                    "Retarget Test",

                Duration =
                    1.0f,

                Channels =
                    new List<ImportedAnimationChannel>
                    {
                        new()
                        {
                            NodeName =
                                hipsName,

                            Translation =
                                VectorTrack(
                                    hipsReference,
                                    hipsReference +
                                    hipsEndTranslation)
                        },

                        new()
                        {
                            NodeName =
                                leftArmName,

                            Rotation =
                                QuaternionTrack(
                                    armReference,
                                    Normalize(
                                        armReference *
                                        leftArmEndRotation))
                        }
                    }
            };
    }

    private static ImportedVectorTrack VectorTrack(
        Vector3 start,
        Vector3 end)
    {
        return
            new ImportedVectorTrack
            {
                Interpolation =
                    ImportedAnimationInterpolation.Linear,

                Keys =
                    new List<ImportedVectorKey>
                    {
                        new(
                            0.0f,
                            start,
                            Vector3.Zero,
                            Vector3.Zero),

                        new(
                            1.0f,
                            end,
                            Vector3.Zero,
                            Vector3.Zero)
                    }
            };
    }

    private static ImportedQuaternionTrack QuaternionTrack(
        Quaternion start,
        Quaternion end)
    {
        return
            new ImportedQuaternionTrack
            {
                Interpolation =
                    ImportedAnimationInterpolation.Linear,

                Keys =
                    new List<ImportedQuaternionKey>
                    {
                        new(
                            0.0f,
                            Normalize(
                                start),
                            default,
                            default),

                        new(
                            1.0f,
                            Normalize(
                                end),
                            default,
                            default)
                    }
            };
    }

    private static RigFixture CreateHumanoidFixture(
        float size)
    {
        var skeleton =
            new SkeletonAsset
            {
                Name =
                    $"Humanoid {size:0.00}"
            };

        var mapping =
            new HumanoidBoneMap();

        /*
         * Build a deterministic hierarchy containing all required C9 bones.
         * Positions are model-space bind positions. The target fixture uses the
         * same pose at 2x size so translation scaling has an exact expectation.
         */
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
                1.5f,
                0.0f));

        int spine =
            skeleton.Bones.Count -
            1;

        Add(
            HumanoidBone.Head,
            spine,
            new Vector3(
                0.0f,
                2.5f,
                0.0f));

        Add(
            HumanoidBone.LeftUpperArm,
            spine,
            new Vector3(
                -0.5f,
                2.0f,
                0.0f));

        int leftUpperArm =
            skeleton.Bones.Count -
            1;

        Add(
            HumanoidBone.LeftLowerArm,
            leftUpperArm,
            new Vector3(
                -1.0f,
                2.0f,
                0.0f));

        int leftLowerArm =
            skeleton.Bones.Count -
            1;

        Add(
            HumanoidBone.LeftHand,
            leftLowerArm,
            new Vector3(
                -1.4f,
                2.0f,
                0.0f));

        Add(
            HumanoidBone.RightUpperArm,
            spine,
            new Vector3(
                0.5f,
                2.0f,
                0.0f));

        int rightUpperArm =
            skeleton.Bones.Count -
            1;

        Add(
            HumanoidBone.RightLowerArm,
            rightUpperArm,
            new Vector3(
                1.0f,
                2.0f,
                0.0f));

        int rightLowerArm =
            skeleton.Bones.Count -
            1;

        Add(
            HumanoidBone.RightHand,
            rightLowerArm,
            new Vector3(
                1.4f,
                2.0f,
                0.0f));

        Add(
            HumanoidBone.LeftUpperLeg,
            hips,
            new Vector3(
                -0.25f,
                0.8f,
                0.0f));

        int leftUpperLeg =
            skeleton.Bones.Count -
            1;

        Add(
            HumanoidBone.LeftLowerLeg,
            leftUpperLeg,
            new Vector3(
                -0.25f,
                0.35f,
                0.0f));

        int leftLowerLeg =
            skeleton.Bones.Count -
            1;

        Add(
            HumanoidBone.LeftFoot,
            leftLowerLeg,
            new Vector3(
                -0.25f,
                0.0f,
                0.15f));

        Add(
            HumanoidBone.RightUpperLeg,
            hips,
            new Vector3(
                0.25f,
                0.8f,
                0.0f));

        int rightUpperLeg =
            skeleton.Bones.Count -
            1;

        Add(
            HumanoidBone.RightLowerLeg,
            rightUpperLeg,
            new Vector3(
                0.25f,
                0.35f,
                0.0f));

        int rightLowerLeg =
            skeleton.Bones.Count -
            1;

        Add(
            HumanoidBone.RightFoot,
            rightLowerLeg,
            new Vector3(
                0.25f,
                0.0f,
                0.15f));

        HumanoidReferencePose reference =
            HumanoidReferencePose.Capture(
                skeleton,
                mapping);

        Assert(
            reference.IsReady,
            "C9D: test Humanoid reference pose was not ready.");

        return
            new RigFixture(
                skeleton,
                mapping,
                reference);

        void Add(
            HumanoidBone semantic,
            int parentIndex,
            Vector3 unscaledModelPosition)
        {
            string name =
                semantic.ToString();

            Vector3 modelPosition =
                unscaledModelPosition *
                size;

            Matrix4x4 model =
                Matrix4x4.CreateTranslation(
                    modelPosition);

            Assert(
                Matrix4x4.Invert(
                    model,
                    out Matrix4x4 inverseBind),
                "C9D: test reference matrix could not be inverted.");

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

    private static Vector3 GetLocalTranslation(
        RigFixture fixture,
        int boneIndex)
    {
        Matrix4x4 local =
            GetReferenceLocal(
                fixture,
                boneIndex);

        Assert(
            Matrix4x4.Decompose(
                local,
                out _,
                out _,
                out Vector3 translation),
            "C9D: reference local transform could not be decomposed.");

        return translation;
    }

    private static Quaternion GetLocalRotation(
        RigFixture fixture,
        int boneIndex)
    {
        Matrix4x4 local =
            GetReferenceLocal(
                fixture,
                boneIndex);

        Assert(
            Matrix4x4.Decompose(
                local,
                out _,
                out Quaternion rotation,
                out _),
            "C9D: reference local transform could not be decomposed.");

        return Normalize(
            rotation);
    }

    private static Vector3 GetLocalScale(
        RigFixture fixture,
        int boneIndex)
    {
        Matrix4x4 local =
            GetReferenceLocal(
                fixture,
                boneIndex);

        Assert(
            Matrix4x4.Decompose(
                local,
                out Vector3 scale,
                out _,
                out _),
            "C9D: reference local transform could not be decomposed.");

        return scale;
    }

    private static Matrix4x4 GetReferenceLocal(
        RigFixture fixture,
        int boneIndex)
    {
        Bone bone =
            fixture.Skeleton.Bones[
                boneIndex];

        Assert(
            Matrix4x4.Invert(
                bone.BindPose,
                out Matrix4x4 model),
            "C9D: reference bind transform could not be inverted.");

        if (bone.ParentIndex <
            0)
        {
            return model;
        }

        Bone parent =
            fixture.Skeleton.Bones[
                bone.ParentIndex];

        Assert(
            Matrix4x4.Invert(
                parent.BindPose,
                out Matrix4x4 parentModel),
            "C9D: parent reference bind transform could not be inverted.");

        Assert(
            Matrix4x4.Invert(
                parentModel,
                out Matrix4x4 inverseParent),
            "C9D: parent reference model transform could not be inverted.");

        return
            model *
            inverseParent;
    }

    private static Quaternion Normalize(
        Quaternion value) =>
        value.LengthSquared() >
            0.000001f
            ? Quaternion.Normalize(
                value)
            : Quaternion.Identity;

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

    private static void AssertNear(
        float actual,
        float expected,
        float tolerance,
        string message)
    {
        if (MathF.Abs(
                actual -
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

    private sealed class RigFixture
    {
        public SkeletonAsset Skeleton { get; }

        public HumanoidBoneMap Mapping { get; }

        public HumanoidReferencePose ReferencePose { get; }

        public RigFixture(
            SkeletonAsset skeleton,
            HumanoidBoneMap mapping,
            HumanoidReferencePose referencePose)
        {
            Skeleton =
                skeleton;

            Mapping =
                mapping;

            ReferencePose =
                referencePose;
        }

        public int IndexOf(
            HumanoidBone semantic)
        {
            string sourceName =
                Mapping.GetBoneName(
                    semantic)
                ?? throw new InvalidOperationException(
                    $"Test mapping missing {semantic}.");

            for (int index = 0;
                 index < Skeleton.Bones.Count;
                 index++)
            {
                if (string.Equals(
                        Skeleton.Bones[index].Name,
                        sourceName,
                        StringComparison.Ordinal))
                {
                    return index;
                }
            }

            throw new InvalidOperationException(
                $"Test source bone '{sourceName}' was not found.");
        }
    }
}
