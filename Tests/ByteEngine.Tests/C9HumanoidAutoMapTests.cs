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
        VerifyValidationRejectsMissingRequiredBone();
    }

    private static void VerifyMixamoAutoMap()
    {
        SkeletonAsset skeleton =
            CreateSkeleton(
                "mixamorig:Hips",
                "mixamorig:Spine",
                "mixamorig:Spine1",
                "mixamorig:Spine2",
                "mixamorig:Neck",
                "mixamorig:Head",
                "mixamorig:LeftShoulder",
                "mixamorig:LeftArm",
                "mixamorig:LeftForeArm",
                "mixamorig:LeftHand",
                "mixamorig:RightShoulder",
                "mixamorig:RightArm",
                "mixamorig:RightForeArm",
                "mixamorig:RightHand",
                "mixamorig:LeftUpLeg",
                "mixamorig:LeftLeg",
                "mixamorig:LeftFoot",
                "mixamorig:LeftToeBase",
                "mixamorig:RightUpLeg",
                "mixamorig:RightLeg",
                "mixamorig:RightFoot",
                "mixamorig:RightToeBase",
                "mixamorig:LeftHandThumb1",
                "mixamorig:LeftHandThumb2",
                "mixamorig:LeftHandThumb3");

        HumanoidBoneMap mapping =
            HumanoidRigMapper.AutoMap(
                skeleton);

        HumanoidRigValidationResult validation =
            HumanoidRigMapper.Validate(
                skeleton,
                mapping);

        Assert(
            validation.IsReady,
            $"C9B: Mixamo skeleton did not validate. Missing: {string.Join(", ", validation.MissingRequiredBones)}");

        Assert(
            mapping.GetBoneName(
                HumanoidBone.Hips) ==
            "mixamorig:Hips",
            "C9B: Mixamo Hips was not auto-mapped.");

        Assert(
            mapping.GetBoneName(
                HumanoidBone.LeftLowerArm) ==
            "mixamorig:LeftForeArm",
            "C9B: Mixamo LeftForeArm was not mapped to LeftLowerArm.");

        Assert(
            mapping.GetBoneName(
                HumanoidBone.LeftThumbProximal) ==
            "mixamorig:LeftHandThumb1",
            "C9B: Mixamo thumb mapping failed.");
    }

    private static void VerifyUnrealStyleAutoMap()
    {
        SkeletonAsset skeleton =
            CreateSkeleton(
                "root",
                "pelvis",
                "spine_01",
                "spine_02",
                "spine_03",
                "neck_01",
                "head",
                "clavicle_l",
                "upperarm_l",
                "lowerarm_l",
                "hand_l",
                "clavicle_r",
                "upperarm_r",
                "lowerarm_r",
                "hand_r",
                "thigh_l",
                "calf_l",
                "foot_l",
                "ball_l",
                "thigh_r",
                "calf_r",
                "foot_r",
                "ball_r");

        HumanoidBoneMap mapping =
            HumanoidRigMapper.AutoMap(
                skeleton);

        HumanoidRigValidationResult validation =
            HumanoidRigMapper.Validate(
                skeleton,
                mapping);

        Assert(
            validation.IsReady,
            $"C9B: Unreal-style skeleton did not validate. Missing: {string.Join(", ", validation.MissingRequiredBones)}");

        Assert(
            mapping.GetBoneName(
                HumanoidBone.Hips) ==
            "pelvis",
            "C9B: Unreal pelvis was not mapped to Hips.");

        Assert(
            mapping.GetBoneName(
                HumanoidBone.LeftUpperArm) ==
            "upperarm_l",
            "C9B: Unreal left upper arm was not mapped.");

        Assert(
            mapping.GetBoneName(
                HumanoidBone.RightLowerLeg) ==
            "calf_r",
            "C9B: Unreal right calf was not mapped to RightLowerLeg.");
    }

    private static void VerifyValidationRejectsMissingRequiredBone()
    {
        SkeletonAsset skeleton =
            CreateSkeleton(
                "Hips",
                "Spine",
                "Head",
                "LeftArm",
                "LeftForeArm",
                "LeftHand",
                "RightArm",
                "RightForeArm",
                "RightHand",
                "LeftUpLeg",
                "LeftLeg",
                "LeftFoot",
                "RightUpLeg",
                "RightLeg");

        HumanoidBoneMap mapping =
            HumanoidRigMapper.AutoMap(
                skeleton);

        HumanoidRigValidationResult validation =
            HumanoidRigMapper.Validate(
                skeleton,
                mapping);

        Assert(
            !validation.IsReady,
            "C9B: incomplete Humanoid skeleton should not validate.");

        Assert(
            validation.MissingRequiredBones.Contains(
                HumanoidBone.RightFoot),
            "C9B: validation did not report the missing Right Foot.");
    }

    private static SkeletonAsset CreateSkeleton(
        params string[] names)
    {
        return
            new SkeletonAsset
            {
                Name =
                    "Test Skeleton",

                Bones =
                    names
                        .Select(
                            name =>
                                new Bone
                                {
                                    Name =
                                        name
                                })
                        .ToList()
            };
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
