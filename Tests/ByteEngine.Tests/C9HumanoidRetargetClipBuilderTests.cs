using System.Numerics;
using System.Runtime.CompilerServices;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Tests;

internal static class C9HumanoidRetargetClipBuilderTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        VerifyRetargetedClipUsesTargetBoneNames();
        VerifyTargetBoneLengthsRemainTargetAuthored();
    }

    private static void VerifyRetargetedClipUsesTargetBoneNames()
    {
        Fixture source =
            CreateFixture(
                "Src",
                1.0f);

        Fixture target =
            CreateFixture(
                "Dst",
                1.5f);

        ImportedAnimation sourceClip =
            CreateSourceClip(
                source);

        ImportedAnimation generated =
            HumanoidRetargetClipBuilder.Build(
                source.Skeleton,
                source.Mapping,
                source.ReferencePose,
                sourceClip,
                target.Skeleton,
                target.Mapping,
                target.ReferencePose,
                target.Nodes,
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Retargeted Walk",
                30.0f);

        string targetLeftArm =
            target.Mapping.GetBoneName(
                HumanoidBone.LeftUpperArm)
            ?? throw new InvalidOperationException();

        ImportedAnimationChannel? arm =
            generated.FindChannel(
                targetLeftArm);

        Assert(
            arm !=
            null,
            "C9E: generated target clip did not contain target LeftUpperArm channel.");

        Assert(
            generated.FindChannel(
                source.Mapping.GetBoneName(
                    HumanoidBone.LeftUpperArm)!) ==
            null,
            "C9E: generated target clip should not keep source-only bone names.");

        Assert(
            generated.Duration ==
            sourceClip.Duration,
            "C9E: generated target clip duration changed.");
    }

    private static void VerifyTargetBoneLengthsRemainTargetAuthored()
    {
        Fixture source =
            CreateFixture(
                "Src",
                1.0f);

        Fixture target =
            CreateFixture(
                "Dst",
                2.0f);

        ImportedAnimation sourceClip =
            CreateSourceClip(
                source);

        ImportedAnimation generated =
            HumanoidRetargetClipBuilder.Build(
                source.Skeleton,
                source.Mapping,
                source.ReferencePose,
                sourceClip,
                target.Skeleton,
                target.Mapping,
                target.ReferencePose,
                target.Nodes,
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Retargeted",
                10.0f);

        string targetLowerArm =
            target.Mapping.GetBoneName(
                HumanoidBone.LeftLowerArm)
            ?? throw new InvalidOperationException();

        ImportedAnimationChannel lowerArm =
            generated.FindChannel(
                targetLowerArm)
            ?? throw new InvalidOperationException(
                "C9E: target lower arm channel missing.");

        Vector3 first =
            lowerArm.Translation!
                .Keys[0]
                .Value;

        Vector3 last =
            lowerArm.Translation
                .Keys[^1]
                .Value;

        AssertNear(
            first,
            last,
            0.0001f,
            "C9E: retargeted arm animation changed target-authored bone length/local offset.");
    }

    private static ImportedAnimation CreateSourceClip(
        Fixture source)
    {
        string hips =
            source.Mapping.GetBoneName(
                HumanoidBone.Hips)!;

        string arm =
            source.Mapping.GetBoneName(
                HumanoidBone.LeftUpperArm)!;

        Vector3 hipsLocal =
            LocalTranslation(
                source,
                HumanoidBone.Hips);

        return
            new ImportedAnimation
            {
                Key =
                    "source:walk",

                Name =
                    "Walk",

                Duration =
                    1.0f,

                Channels =
                    new List<ImportedAnimationChannel>
                    {
                        new()
                        {
                            NodeName =
                                hips,

                            Translation =
                                new ImportedVectorTrack
                                {
                                    Keys =
                                        new List<ImportedVectorKey>
                                        {
                                            new(
                                                0.0f,
                                                hipsLocal,
                                                Vector3.Zero,
                                                Vector3.Zero),

                                            new(
                                                1.0f,
                                                hipsLocal +
                                                new Vector3(
                                                    1.0f,
                                                    0.0f,
                                                    0.0f),
                                                Vector3.Zero,
                                                Vector3.Zero)
                                        }
                                }
                        },

                        new()
                        {
                            NodeName =
                                arm,

                            Rotation =
                                new ImportedQuaternionTrack
                                {
                                    Keys =
                                        new List<ImportedQuaternionKey>
                                        {
                                            new(
                                                0.0f,
                                                Quaternion.Identity,
                                                default,
                                                default),

                                            new(
                                                1.0f,
                                                Quaternion.CreateFromAxisAngle(
                                                    Vector3.UnitZ,
                                                    MathF.PI /
                                                    2.0f),
                                                default,
                                                default)
                                        }
                                }
                        }
                    }
            };
    }

    private static Fixture CreateFixture(
        string prefix,
        float size)
    {
        var skeleton =
            new SkeletonAsset
            {
                Name =
                    $"{prefix} Skeleton"
            };

        var mapping =
            new HumanoidBoneMap();

        var nodes =
            new List<ImportedNode>();

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

        int lua =
            skeleton.Bones.Count -
            1;

        Add(
            HumanoidBone.LeftLowerArm,
            lua,
            new Vector3(
                -1.0f,
                2.0f,
                0.0f));

        int lla =
            skeleton.Bones.Count -
            1;

        Add(
            HumanoidBone.LeftHand,
            lla,
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

        int rua =
            skeleton.Bones.Count -
            1;

        Add(
            HumanoidBone.RightLowerArm,
            rua,
            new Vector3(
                1.0f,
                2.0f,
                0.0f));

        int rla =
            skeleton.Bones.Count -
            1;

        Add(
            HumanoidBone.RightHand,
            rla,
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

        int lul =
            skeleton.Bones.Count -
            1;

        Add(
            HumanoidBone.LeftLowerLeg,
            lul,
            new Vector3(
                -0.25f,
                0.35f,
                0.0f));

        int lll =
            skeleton.Bones.Count -
            1;

        Add(
            HumanoidBone.LeftFoot,
            lll,
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

        int rul =
            skeleton.Bones.Count -
            1;

        Add(
            HumanoidBone.RightLowerLeg,
            rul,
            new Vector3(
                0.25f,
                0.35f,
                0.0f));

        int rll =
            skeleton.Bones.Count -
            1;

        Add(
            HumanoidBone.RightFoot,
            rll,
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
            "C9E: test Humanoid reference pose was not ready.");

        return
            new Fixture(
                skeleton,
                mapping,
                reference,
                nodes);

        void Add(
            HumanoidBone semantic,
            int parentBoneIndex,
            Vector3 unscaledModelPosition)
        {
            string boneName =
                $"{prefix}_{semantic}";

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
                "C9E: test bind transform could not be inverted.");

            int boneIndex =
                skeleton.Bones.Count;

            skeleton.Bones.Add(
                new Bone
                {
                    Name =
                        boneName,

                    ParentIndex =
                        parentBoneIndex,

                    BindPose =
                        inverseBind
                });

            mapping.SetBone(
                semantic,
                boneName);

            Matrix4x4 local =
                model;

            string? parentKey =
                null;

            if (parentBoneIndex >=
                0)
            {
                Matrix4x4.Invert(
                    skeleton.Bones[
                        parentBoneIndex].BindPose,
                    out Matrix4x4 parentModel);

                Matrix4x4.Invert(
                    parentModel,
                    out Matrix4x4 inverseParent);

                local =
                    model *
                    inverseParent;

                parentKey =
                    nodes[
                        parentBoneIndex].Key;
            }

            nodes.Add(
                new ImportedNode
                {
                    Key =
                        $"node:{boneIndex}",

                    Name =
                        boneName,

                    ParentKey =
                        parentKey,

                    LocalTransform =
                        local
                });
        }
    }

    private static Vector3 LocalTranslation(
        Fixture fixture,
        HumanoidBone bone)
    {
        string name =
            fixture.Mapping.GetBoneName(
                bone)!;

        ImportedNode node =
            fixture.Nodes.First(
                node =>
                    node.Name ==
                    name);

        Matrix4x4.Decompose(
            node.LocalTransform,
            out _,
            out _,
            out Vector3 translation);

        return translation;
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

    private sealed record Fixture(
        SkeletonAsset Skeleton,
        HumanoidBoneMap Mapping,
        HumanoidReferencePose ReferencePose,
        IReadOnlyList<ImportedNode> Nodes);
}
