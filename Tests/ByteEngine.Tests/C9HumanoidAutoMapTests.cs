using System.Runtime.CompilerServices;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Tests;

internal static class C9HumanoidAutoMapTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        VerifyMixamoAutoMap();
        VerifyUnrealStyleAutoMap();
        VerifyBlenderShortNameAutoMap();
        VerifyHierarchyBeatsDetachedNameMatch();
        VerifyHelperBonesAreSkipped();
        VerifyValidationRejectsMissingRequiredBone();
    }

    private static void VerifyMixamoAutoMap()
    {
        SkeletonAsset skeleton =
            CreateMixamoSkeleton();

        HumanoidBoneMap mapping =
            HumanoidRigMapper.AutoMap(
                skeleton);

        HumanoidRigValidationResult validation =
            HumanoidRigMapper.Validate(
                skeleton,
                mapping);

        Assert(
            validation.IsReady,
            $"C9H: Mixamo skeleton did not validate. Missing: {string.Join(", ", validation.MissingRequiredBones)}");

        Assert(
            mapping.GetBoneName(
                HumanoidBone.Hips) ==
            "mixamorig:Hips",
            "C9H: Mixamo Hips was not auto-mapped.");

        Assert(
            mapping.GetBoneName(
                HumanoidBone.LeftLowerArm) ==
            "mixamorig:LeftForeArm",
            "C9H: Mixamo LeftForeArm was not mapped to LeftLowerArm.");

        Assert(
            mapping.GetBoneName(
                HumanoidBone.LeftThumbProximal) ==
            "mixamorig:LeftHandThumb1",
            "C9H: Mixamo thumb mapping failed.");
    }

    private static void VerifyUnrealStyleAutoMap()
    {
        SkeletonAsset skeleton =
            CreateUnrealSkeleton();

        HumanoidBoneMap mapping =
            HumanoidRigMapper.AutoMap(
                skeleton);

        HumanoidRigValidationResult validation =
            HumanoidRigMapper.Validate(
                skeleton,
                mapping);

        Assert(
            validation.IsReady,
            $"C9H: Unreal-style skeleton did not validate. Missing: {string.Join(", ", validation.MissingRequiredBones)}");

        Assert(
            mapping.GetBoneName(
                HumanoidBone.Hips) ==
            "pelvis",
            "C9H: Unreal pelvis was not mapped to Hips.");

        Assert(
            mapping.GetBoneName(
                HumanoidBone.LeftUpperArm) ==
            "upperarm_l",
            "C9H: Unreal left upper arm was not mapped.");

        Assert(
            mapping.GetBoneName(
                HumanoidBone.RightLowerLeg) ==
            "calf_r",
            "C9H: Unreal right calf was not mapped to RightLowerLeg.");
    }

    private static void VerifyBlenderShortNameAutoMap()
    {
        var builder =
            new SkeletonBuilder(
                "Blender Short Names");

        builder.Add(
            "Pelvis");
        builder.Add(
            "Spine",
            "Pelvis");
        builder.Add(
            "Head",
            "Spine");

        builder.Add(
            "Arm.L",
            "Spine");
        builder.Add(
            "Forarm.L",
            "Arm.L");
        builder.Add(
            "Hand.L",
            "Forarm.L");

        builder.Add(
            "Arm.R",
            "Spine");
        builder.Add(
            "Forarm.R",
            "Arm.R");
        builder.Add(
            "Hand.R",
            "Forarm.R");

        builder.Add(
            "Hip.L",
            "Pelvis");
        builder.Add(
            "Calf.L",
            "Hip.L");
        builder.Add(
            "Foot.L",
            "Calf.L");

        builder.Add(
            "Hip.R",
            "Pelvis");
        builder.Add(
            "Calf.R",
            "Hip.R");
        builder.Add(
            "Foot.R",
            "Calf.R");

        SkeletonAsset skeleton =
            builder.Build();

        HumanoidBoneMap mapping =
            HumanoidRigMapper.AutoMap(
                skeleton);

        HumanoidRigValidationResult validation =
            HumanoidRigMapper.Validate(
                skeleton,
                mapping);

        Assert(
            validation.IsReady,
            $"C9I: Blender short-name skeleton did not validate. Missing: {string.Join(", ", validation.MissingRequiredBones)}");

        Assert(
            mapping.GetBoneName(
                HumanoidBone.LeftUpperArm) ==
            "Arm.L",
            "C9I: Arm.L was not mapped to LeftUpperArm.");

        Assert(
            mapping.GetBoneName(
                HumanoidBone.LeftLowerArm) ==
            "Forarm.L",
            "C9I: Forarm.L was not mapped to LeftLowerArm.");

        Assert(
            mapping.GetBoneName(
                HumanoidBone.LeftUpperLeg) ==
            "Hip.L",
            "C9I: Hip.L was not mapped to LeftUpperLeg.");
    }

    private static void VerifyHierarchyBeatsDetachedNameMatch()
    {
        var builder =
            new SkeletonBuilder(
                "Hierarchy Preference");

        builder.Add(
            "Pelvis");

        /*
         * This is the kind of rig that defeated the original name-only mapper:
         * a bone named exactly "Spine" exists, but on another hierarchy branch.
         */
        builder.Add(
            "Spine");

        builder.Add(
            "DEF-spine.001",
            "Pelvis");

        builder.Add(
            "Head",
            "DEF-spine.001");

        AddRequiredLimbs(
            builder,
            "Pelvis",
            "DEF-spine.001");

        SkeletonAsset skeleton =
            builder.Build();

        HumanoidBoneMap mapping =
            HumanoidRigMapper.AutoMap(
                skeleton);

        Assert(
            mapping.GetBoneName(
                HumanoidBone.Spine) ==
            "DEF-spine.001",
            "C9H: Auto Map preferred a detached exact-name Spine instead of the deform bone below Hips.");
    }

    private static void VerifyHelperBonesAreSkipped()
    {
        var builder =
            new SkeletonBuilder(
                "Helper Rejection");

        builder.Add(
            "Pelvis");

        builder.Add(
            "DEF-spine",
            "Pelvis");

        builder.Add(
            "Head",
            "DEF-spine");

        builder.Add(
            "upper_arm.L",
            "DEF-spine");

        builder.Add(
            "forearm.L",
            "upper_arm.L");

        builder.Add(
            "HandIK.L",
            "forearm.L");

        builder.Add(
            "DEF-hand.L",
            "forearm.L");

        builder.Add(
            "upper_arm.R",
            "DEF-spine");

        builder.Add(
            "forearm.R",
            "upper_arm.R");

        builder.Add(
            "HandIK.R",
            "forearm.R");

        builder.Add(
            "DEF-hand.R",
            "forearm.R");

        AddLegs(
            builder,
            "Pelvis");

        SkeletonAsset skeleton =
            builder.Build();

        HumanoidBoneMap mapping =
            HumanoidRigMapper.AutoMap(
                skeleton);

        Assert(
            mapping.GetBoneName(
                HumanoidBone.LeftHand) ==
            "DEF-hand.L",
            "C9H: Auto Map selected a left-hand IK/helper bone instead of the deform hand.");

        Assert(
            mapping.GetBoneName(
                HumanoidBone.RightHand) ==
            "DEF-hand.R",
            "C9H: Auto Map selected a right-hand IK/helper bone instead of the deform hand.");
    }

    private static void VerifyValidationRejectsMissingRequiredBone()
    {
        var builder =
            new SkeletonBuilder(
                "Missing Right Foot");

        builder.Add(
            "Hips");

        builder.Add(
            "Spine",
            "Hips");

        builder.Add(
            "Head",
            "Spine");

        AddArms(
            builder,
            "Spine");

        builder.Add(
            "LeftUpLeg",
            "Hips");

        builder.Add(
            "LeftLeg",
            "LeftUpLeg");

        builder.Add(
            "LeftFoot",
            "LeftLeg");

        builder.Add(
            "RightUpLeg",
            "Hips");

        builder.Add(
            "RightLeg",
            "RightUpLeg");

        SkeletonAsset skeleton =
            builder.Build();

        HumanoidBoneMap mapping =
            HumanoidRigMapper.AutoMap(
                skeleton);

        HumanoidRigValidationResult validation =
            HumanoidRigMapper.Validate(
                skeleton,
                mapping);

        Assert(
            !validation.IsReady,
            "C9H: incomplete Humanoid skeleton should not validate.");

        Assert(
            validation.MissingRequiredBones.Contains(
                HumanoidBone.RightFoot),
            "C9H: validation did not report the missing Right Foot.");
    }

    private static SkeletonAsset CreateMixamoSkeleton()
    {
        var builder =
            new SkeletonBuilder(
                "Mixamo Test");

        builder.Add(
            "mixamorig:Hips");
        builder.Add(
            "mixamorig:Spine",
            "mixamorig:Hips");
        builder.Add(
            "mixamorig:Spine1",
            "mixamorig:Spine");
        builder.Add(
            "mixamorig:Spine2",
            "mixamorig:Spine1");
        builder.Add(
            "mixamorig:Neck",
            "mixamorig:Spine2");
        builder.Add(
            "mixamorig:Head",
            "mixamorig:Neck");

        builder.Add(
            "mixamorig:LeftShoulder",
            "mixamorig:Spine2");
        builder.Add(
            "mixamorig:LeftArm",
            "mixamorig:LeftShoulder");
        builder.Add(
            "mixamorig:LeftForeArm",
            "mixamorig:LeftArm");
        builder.Add(
            "mixamorig:LeftHand",
            "mixamorig:LeftForeArm");
        builder.Add(
            "mixamorig:LeftHandThumb1",
            "mixamorig:LeftHand");
        builder.Add(
            "mixamorig:LeftHandThumb2",
            "mixamorig:LeftHandThumb1");
        builder.Add(
            "mixamorig:LeftHandThumb3",
            "mixamorig:LeftHandThumb2");

        builder.Add(
            "mixamorig:RightShoulder",
            "mixamorig:Spine2");
        builder.Add(
            "mixamorig:RightArm",
            "mixamorig:RightShoulder");
        builder.Add(
            "mixamorig:RightForeArm",
            "mixamorig:RightArm");
        builder.Add(
            "mixamorig:RightHand",
            "mixamorig:RightForeArm");

        builder.Add(
            "mixamorig:LeftUpLeg",
            "mixamorig:Hips");
        builder.Add(
            "mixamorig:LeftLeg",
            "mixamorig:LeftUpLeg");
        builder.Add(
            "mixamorig:LeftFoot",
            "mixamorig:LeftLeg");
        builder.Add(
            "mixamorig:LeftToeBase",
            "mixamorig:LeftFoot");

        builder.Add(
            "mixamorig:RightUpLeg",
            "mixamorig:Hips");
        builder.Add(
            "mixamorig:RightLeg",
            "mixamorig:RightUpLeg");
        builder.Add(
            "mixamorig:RightFoot",
            "mixamorig:RightLeg");
        builder.Add(
            "mixamorig:RightToeBase",
            "mixamorig:RightFoot");

        return builder.Build();
    }

    private static SkeletonAsset CreateUnrealSkeleton()
    {
        var builder =
            new SkeletonBuilder(
                "Unreal Test");

        builder.Add(
            "root");
        builder.Add(
            "pelvis",
            "root");
        builder.Add(
            "spine_01",
            "pelvis");
        builder.Add(
            "spine_02",
            "spine_01");
        builder.Add(
            "spine_03",
            "spine_02");
        builder.Add(
            "neck_01",
            "spine_03");
        builder.Add(
            "head",
            "neck_01");

        builder.Add(
            "clavicle_l",
            "spine_03");
        builder.Add(
            "upperarm_l",
            "clavicle_l");
        builder.Add(
            "lowerarm_l",
            "upperarm_l");
        builder.Add(
            "hand_l",
            "lowerarm_l");

        builder.Add(
            "clavicle_r",
            "spine_03");
        builder.Add(
            "upperarm_r",
            "clavicle_r");
        builder.Add(
            "lowerarm_r",
            "upperarm_r");
        builder.Add(
            "hand_r",
            "lowerarm_r");

        builder.Add(
            "thigh_l",
            "pelvis");
        builder.Add(
            "calf_l",
            "thigh_l");
        builder.Add(
            "foot_l",
            "calf_l");
        builder.Add(
            "ball_l",
            "foot_l");

        builder.Add(
            "thigh_r",
            "pelvis");
        builder.Add(
            "calf_r",
            "thigh_r");
        builder.Add(
            "foot_r",
            "calf_r");
        builder.Add(
            "ball_r",
            "foot_r");

        return builder.Build();
    }

    private static void AddRequiredLimbs(
        SkeletonBuilder builder,
        string hips,
        string chestOrSpine)
    {
        AddArms(
            builder,
            chestOrSpine);

        AddLegs(
            builder,
            hips);
    }

    private static void AddArms(
        SkeletonBuilder builder,
        string parent)
    {
        builder.Add(
            "LeftArm",
            parent);
        builder.Add(
            "LeftForeArm",
            "LeftArm");
        builder.Add(
            "LeftHand",
            "LeftForeArm");

        builder.Add(
            "RightArm",
            parent);
        builder.Add(
            "RightForeArm",
            "RightArm");
        builder.Add(
            "RightHand",
            "RightForeArm");
    }

    private static void AddLegs(
        SkeletonBuilder builder,
        string hips)
    {
        builder.Add(
            "LeftUpLeg",
            hips);
        builder.Add(
            "LeftLeg",
            "LeftUpLeg");
        builder.Add(
            "LeftFoot",
            "LeftLeg");

        builder.Add(
            "RightUpLeg",
            hips);
        builder.Add(
            "RightLeg",
            "RightUpLeg");
        builder.Add(
            "RightFoot",
            "RightLeg");
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

    private sealed class SkeletonBuilder
    {
        private readonly SkeletonAsset _skeleton;

        private readonly Dictionary<string, int> _indices =
            new(
                StringComparer.OrdinalIgnoreCase);

        public SkeletonBuilder(
            string name)
        {
            _skeleton =
                new SkeletonAsset
                {
                    Name =
                        name
                };
        }

        public void Add(
            string name,
            string? parentName = null)
        {
            int parent =
                parentName !=
                        null &&
                    _indices.TryGetValue(
                        parentName,
                        out int parentIndex)
                    ? parentIndex
                    : -1;

            int index =
                _skeleton.Bones.Count;

            _skeleton.Bones.Add(
                new Bone
                {
                    Name =
                        name,

                    ParentIndex =
                        parent
                });

            _indices[name] =
                index;
        }

        public SkeletonAsset Build() =>
            _skeleton;
    }
}
