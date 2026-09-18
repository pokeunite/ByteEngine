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
        VerifyTargetNodeRootConversionIsPreserved();
        VerifyMeshBindFrameWinsOverAuthoredNodePose();
        VerifyMultiPartTargetUsesOneCoherentSkinFrame();
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

    private static void VerifyTargetNodeRootConversionIsPreserved()
    {
        Fixture source =
            CreateFixture(
                "Src",
                1.0f);

        Fixture targetBase =
            CreateFixture(
                "Dst",
                2.0f);

        Matrix4x4 conversion =
            Matrix4x4.CreateRotationX(
                15.0f *
                MathF.PI /
                180.0f) *
            Matrix4x4.CreateTranslation(
                new Vector3(
                    0.0f,
                    5.0f,
                    -2.0f));

        Fixture target =
            AddNodeConversionRoot(
                targetBase,
                conversion);

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
                "Converted Root Target",
                30.0f);

        string targetHips =
            target.Mapping.GetBoneName(
                HumanoidBone.Hips)
            ?? throw new InvalidOperationException();

        ImportedAnimationChannel hips =
            generated.FindChannel(
                targetHips)
            ?? throw new InvalidOperationException(
                "C9H: target Hips channel missing from converted-root retarget clip.");

        Vector3 generatedFirst =
            hips.Translation!
                .Keys[0]
                .Value;

        Vector3 expectedLocal =
            LocalTranslation(
                target,
                HumanoidBone.Hips);

        AssertNear(
            generatedFirst,
            expectedLocal,
            0.001f,
            "C9H: target ImportedNode root/conversion transform leaked into the generated Hips local track.");
    }

    private static void VerifyMeshBindFrameWinsOverAuthoredNodePose()
    {
        Fixture source =
            CreateFixture(
                "Src",
                1.0f);

        Fixture target =
            CreateFixture(
                "Dst",
                1.0f);

        string targetArmName =
            target.Mapping.GetBoneName(
                HumanoidBone.LeftUpperArm)
            ?? throw new InvalidOperationException();

        string targetHipsName =
            target.Mapping.GetBoneName(
                HumanoidBone.Hips)
            ?? throw new InvalidOperationException();

        const string meshNodeKey =
            "node:test-skinned-mesh";

        const string meshKey =
            "mesh:test-skinned-mesh";

        Matrix4x4 meshFrame =
            Matrix4x4.CreateTranslation(
                new Vector3(
                    0.0f,
                    4.0f,
                    -1.0f));

        var nodes =
            new List<ImportedNode>
            {
                new()
                {
                    Key =
                        meshNodeKey,

                    Name =
                        "SkinnedMeshRoot",

                    LocalTransform =
                        meshFrame,

                    MeshKeys =
                        new List<string>
                        {
                            meshKey
                        }
                }
            };

        foreach (ImportedNode node
                 in target.Nodes)
        {
            Matrix4x4 local =
                node.LocalTransform;

            if (string.Equals(
                    node.Name,
                    targetArmName,
                    StringComparison.Ordinal))
            {
                Matrix4x4.Decompose(
                    local,
                    out Vector3 scale,
                    out _,
                    out Vector3 translation);

                local =
                    Matrix4x4.CreateScale(
                        scale) *
                    Matrix4x4.CreateFromQuaternion(
                        Quaternion.CreateFromAxisAngle(
                            Vector3.UnitZ,
                            35.0f *
                            MathF.PI /
                            180.0f)) *
                    Matrix4x4.CreateTranslation(
                        translation);
            }

            nodes.Add(
                new ImportedNode
                {
                    Key =
                        node.Key,

                    Name =
                        node.Name,

                    ParentKey =
                        string.Equals(
                            node.Name,
                            targetHipsName,
                            StringComparison.Ordinal)
                            ? meshNodeKey
                            : node.ParentKey,

                    LocalTransform =
                        local,

                    MeshKeys =
                        node.MeshKeys.ToList()
                });
        }

        int boneCount =
            target.Skeleton.Bones.Count;

        var vertices =
            new float[
                boneCount *
                8];

        var jointIndices =
            new Vector4[
                boneCount];

        var jointWeights =
            new Vector4[
                boneCount];

        for (int boneIndex = 0;
             boneIndex <
                boneCount;
             boneIndex++)
        {
            jointIndices[boneIndex] =
                new Vector4(
                    boneIndex,
                    0.0f,
                    0.0f,
                    0.0f);

            jointWeights[boneIndex] =
                new Vector4(
                    1.0f,
                    0.0f,
                    0.0f,
                    0.0f);
        }

        ImportedMesh[] meshes =
        {
            new()
            {
                Key =
                    meshKey,

                Name =
                    "Test Skin",

                Vertices =
                    vertices,

                JointIndices =
                    jointIndices,

                JointWeights =
                    jointWeights
            }
        };

        ImportedAnimation generated =
            HumanoidRetargetClipBuilder.Build(
                source.Skeleton,
                source.Mapping,
                source.ReferencePose,
                CreateSourceClip(
                    source),
                target.Skeleton,
                target.Mapping,
                target.ReferencePose,
                nodes,
                meshes,
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Bind Frame Test",
                30.0f);

        ImportedAnimationChannel arm =
            generated.FindChannel(
                targetArmName)
            ?? throw new InvalidOperationException(
                "C9I: target LeftUpperArm channel missing from bind-frame test.");

        Quaternion firstRotation =
            arm.Rotation!
                .Keys[0]
                .Value;

        float dot =
            MathF.Abs(
                Quaternion.Dot(
                    Quaternion.Normalize(
                        firstRotation),
                    Quaternion.Identity));

        Assert(
            dot >
                0.999f,
            "C9I: authored/static node pose leaked into the retargeted bind pose instead of using the skin bind frame.");
    }

    private static void VerifyMultiPartTargetUsesOneCoherentSkinFrame()
    {
        Fixture source =
            CreateFixture(
                "Src",
                1.0f);

        Fixture target =
            CreateFixture(
                "Dst",
                1.0f);

        const string primaryMeshKey =
            "mesh:primary";

        const string secondaryMeshKey =
            "mesh:secondary";

        const string primaryNodeKey =
            "node:primary";

        const string secondaryNodeKey =
            "node:secondary";

        Matrix4x4 primaryFrame =
            Matrix4x4.CreateTranslation(
                new Vector3(
                    0.0f,
                    3.0f,
                    0.0f));

        Matrix4x4 secondaryFrame =
            Matrix4x4.CreateTranslation(
                new Vector3(
                    25.0f,
                    -10.0f,
                    7.0f));

        var nodes =
            new List<ImportedNode>
            {
                new()
                {
                    Key =
                        primaryNodeKey,

                    Name =
                        "PrimarySkin",

                    LocalTransform =
                        primaryFrame,

                    MeshKeys =
                        new List<string>
                        {
                            primaryMeshKey
                        }
                },

                new()
                {
                    Key =
                        secondaryNodeKey,

                    Name =
                        "SecondarySkin",

                    LocalTransform =
                        secondaryFrame,

                    MeshKeys =
                        new List<string>
                        {
                            secondaryMeshKey
                        }
                }
            };

        string hipsName =
            target.Mapping.GetBoneName(
                HumanoidBone.Hips)!;

        foreach (ImportedNode node
                 in target.Nodes)
        {
            nodes.Add(
                new ImportedNode
                {
                    Key =
                        node.Key,

                    Name =
                        node.Name,

                    ParentKey =
                        string.Equals(
                            node.Name,
                            hipsName,
                            StringComparison.Ordinal)
                            ? primaryNodeKey
                            : node.ParentKey,

                    LocalTransform =
                        node.LocalTransform,

                    MeshKeys =
                        node.MeshKeys.ToList()
                });
        }

        int boneCount =
            target.Skeleton.Bones.Count;

        ImportedMesh primary =
            CreateSkinMesh(
                primaryMeshKey,
                Enumerable.Range(
                    0,
                    boneCount /
                    2)
                    .ToArray());

        ImportedMesh secondary =
            CreateSkinMesh(
                secondaryMeshKey,
                Enumerable.Range(
                    boneCount /
                    2,
                    boneCount -
                    boneCount /
                    2)
                    .ToArray());

        ImportedAnimation generated =
            HumanoidRetargetClipBuilder.Build(
                source.Skeleton,
                source.Mapping,
                source.ReferencePose,
                CreateSourceClip(
                    source),
                target.Skeleton,
                target.Mapping,
                target.ReferencePose,
                nodes,
                new[]
                {
                    primary,
                    secondary
                },
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Multi Part Frame Test",
                30.0f);

        string leftHandName =
            target.Mapping.GetBoneName(
                HumanoidBone.LeftHand)!;

        string rightFootName =
            target.Mapping.GetBoneName(
                HumanoidBone.RightFoot)!;

        ImportedAnimationChannel leftHand =
            generated.FindChannel(
                leftHandName)
            ?? throw new InvalidOperationException(
                "C9J: left hand channel missing.");

        ImportedAnimationChannel rightFoot =
            generated.FindChannel(
                rightFootName)
            ?? throw new InvalidOperationException(
                "C9J: right foot channel missing.");

        /*
         * If secondaryFrame leaks into only part of the skeleton the local
         * tracks become enormous. One coherent frame keeps both chains local
         * and close to their authored offsets.
         */
        Vector3 leftHandTranslation =
            leftHand.Translation!
                .Keys[0]
                .Value;

        Vector3 rightFootTranslation =
            rightFoot.Translation!
                .Keys[0]
                .Value;

        Assert(
            leftHandTranslation.Length() <
                5.0f &&
            rightFootTranslation.Length() <
                5.0f,
            "C9J: multi-part target mixed incompatible mesh-node frames into one Humanoid pose.");

        ImportedMesh CreateSkinMesh(
            string key,
            int[] influencedBones)
        {
            int vertexCount =
                influencedBones.Length;

            var vertices =
                new float[
                    vertexCount *
                    8];

            var joints =
                new Vector4[
                    vertexCount];

            var weights =
                new Vector4[
                    vertexCount];

            for (int index = 0;
                 index <
                    influencedBones.Length;
                 index++)
            {
                joints[index] =
                    new Vector4(
                        influencedBones[index],
                        0.0f,
                        0.0f,
                        0.0f);

                weights[index] =
                    new Vector4(
                        1.0f,
                        0.0f,
                        0.0f,
                        0.0f);
            }

            return
                new ImportedMesh
                {
                    Key =
                        key,

                    Name =
                        key,

                    Vertices =
                        vertices,

                    JointIndices =
                        joints,

                    JointWeights =
                        weights
                };
        }
    }

    private static Fixture AddNodeConversionRoot(
        Fixture fixture,
        Matrix4x4 conversion)
    {
        string hipsName =
            fixture.Mapping.GetBoneName(
                HumanoidBone.Hips)
            ?? throw new InvalidOperationException();

        const string rootKey =
            "node:byteengine-test-conversion-root";

        var nodes =
            new List<ImportedNode>
            {
                new()
                {
                    Key =
                        rootKey,

                    Name =
                        "ArmatureConversion",

                    LocalTransform =
                        conversion
                }
            };

        foreach (ImportedNode node
                 in fixture.Nodes)
        {
            nodes.Add(
                new ImportedNode
                {
                    Key =
                        node.Key,

                    Name =
                        node.Name,

                    ParentKey =
                        string.Equals(
                            node.Name,
                            hipsName,
                            StringComparison.Ordinal)
                            ? rootKey
                            : node.ParentKey,

                    LocalTransform =
                        node.LocalTransform,

                    MeshKeys =
                        node.MeshKeys.ToList()
                });
        }

        return
            new Fixture(
                fixture.Skeleton,
                fixture.Mapping,
                fixture.ReferencePose,
                nodes);
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
