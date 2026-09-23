using System.Numerics;
using System.Runtime.CompilerServices;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Tests;

internal static class HumanoidGeometryMappingTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var source = Check(Matrix4x4.Identity, "bone_");
        var target = Check(Matrix4x4.CreateFromYawPitchRoll(.75f, -.35f, .42f) *
            Matrix4x4.CreateScale(180f), "joint_");
        Check(Matrix4x4.CreateFromYawPitchRoll(-.6f, .2f, -.4f), "x", extraSpine: 5, fingers: 5);
        Check(Matrix4x4.Identity, "jointA", fingers: 3);
        Check(Matrix4x4.Identity, "wrapped_", fingers: 3, clavicles: true, wrappers: true);
        string sourceHips = source.Map.GetBoneName(HumanoidBone.Hips)!;
        ImportedAnimation clip = new()
        {
            Name = "Meshless Walk", Duration = 1,
            Channels = new()
            {
                new ImportedAnimationChannel
                {
                    NodeName = sourceHips,
                    Translation = new ImportedVectorTrack
                    {
                        Keys = new()
                        {
                            new ImportedVectorKey(0, new Vector3(0, 1, 0), default, default),
                            new ImportedVectorKey(1, new Vector3(0, 1, -.3f), default, default)
                        }
                    }
                }
            }
        };
        HumanoidRetargetPose pose = HumanoidRetargeter.Retarget(
            source.Skeleton, source.Map, HumanoidReferencePose.Capture(source.Skeleton, source.Map),
            clip, 1, target.Skeleton, target.Map,
            HumanoidReferencePose.Capture(target.Skeleton, target.Map), loop: false);
        Assert(pose.ModelMatrices.Count == target.Skeleton.Bones.Count &&
            pose.ModelMatrices.All(matrix => float.IsFinite(matrix.Translation.X) &&
                float.IsFinite(matrix.Translation.Y) && float.IsFinite(matrix.Translation.Z)),
            "meshless cross-rig retarget produces finite target pose");
        int targetHips = target.Skeleton.Bones.FindIndex(bone =>
            bone.Name == target.Map.GetBoneName(HumanoidBone.Hips));
        Matrix4x4 targetBind = default;
        Assert(targetHips >= 0 && Matrix4x4.Invert(
            target.Skeleton.Bones[targetHips].BindPose, out targetBind),
            "target bind pose is invertible");
        Assert(Vector3.Distance(pose.ModelMatrices[targetHips].Translation,
            targetBind.Translation) > 1f,
            "meshless source trajectory transfers across proportions and facing direction");
        Console.WriteLine("Humanoid geometry mapping and meshless retarget regressions passed.");
    }

    private static (SkeletonAsset Skeleton, HumanoidBoneMap Map) Check(Matrix4x4 root, string prefix,
        int extraSpine = 0, int fingers = 0, bool clavicles = false, bool wrappers = false)
    {
        SkeletonAsset skeleton = new();
        Dictionary<string, int> names = new();
        void Add(string id, string? parent, Vector3 position)
        {
            Matrix4x4 model = Matrix4x4.CreateTranslation(position) * root;
            if (!Matrix4x4.Invert(model, out Matrix4x4 inverse)) throw new Exception("invalid fixture");
            int parentIndex = parent == null ? -1 : names[parent];
            if (wrappers && parent != null)
            {
                skeleton.Bones.Add(new Bone
                {
                    Name = prefix + id + "_$AssimpFbx$_Translation",
                    ParentIndex = parentIndex,
                    BindPose = inverse
                });
                parentIndex = skeleton.Bones.Count - 1;
            }
            names[id] = skeleton.Bones.Count;
            skeleton.Bones.Add(new Bone { Name = prefix + skeleton.Bones.Count.ToString("000"),
                ParentIndex = parentIndex, BindPose = inverse });
        }
        Add("root", null, new(0, 0, 0));
        Add("hips", "root", new(0, 1, 0));
        Add("spine", "hips", new(0, 1.25f, 0));
        string chestParent = "spine";
        for (int i = 0; i < extraSpine; i++)
        {
            string next = "spineExtra" + i;
            Add(next, chestParent, new(0, 1.25f + .04f * (i + 1), 0));
            chestParent = next;
        }
        Add("chest", chestParent, new(0, 1.5f, 0));
        Add("neck", "chest", new(0, 1.7f, 0));
        Add("head", "neck", new(0, 1.9f, 0));
        Add("leftUpperLeg", "hips", new(-.2f, .9f, 0));
        Add("leftLowerLeg", "leftUpperLeg", new(-.2f, .5f, 0));
        Add("leftFoot", "leftLowerLeg", new(-.2f, .1f, -.15f));
        Add("rightUpperLeg", "hips", new(.2f, .9f, 0));
        Add("rightLowerLeg", "rightUpperLeg", new(.2f, .5f, 0));
        Add("rightFoot", "rightLowerLeg", new(.2f, .1f, -.15f));
        if (clavicles) Add("leftShoulder", "chest", new(-.12f, 1.52f, 0));
        Add("leftUpperArm", clavicles ? "leftShoulder" : "chest", new(-.4f, 1.5f, 0));
        Add("leftLowerArm", "leftUpperArm", new(-.75f, 1.5f, 0));
        Add("leftHand", "leftLowerArm", new(-1f, 1.5f, 0));
        Add("leftTwist", "leftUpperArm", new(-.42f, 1.52f, 0));
        if (clavicles) Add("rightShoulder", "chest", new(.12f, 1.52f, 0));
        Add("rightUpperArm", clavicles ? "rightShoulder" : "chest", new(.4f, 1.5f, 0));
        Add("rightLowerArm", "rightUpperArm", new(.75f, 1.5f, 0));
        Add("rightHand", "rightLowerArm", new(1f, 1.5f, 0));
        Add("rightTwist", "rightUpperArm", new(.42f, 1.52f, 0));
        for (int i = 0; i < fingers; i++)
        {
            Add("leftFinger" + i + "A", "leftHand", new(-1.05f, 1.47f + .015f * i, -.04f));
            Add("leftFinger" + i + "B", "leftFinger" + i + "A", new(-1.12f, 1.47f + .015f * i, -.08f));
            Add("rightFinger" + i + "A", "rightHand", new(1.05f, 1.47f + .015f * i, -.04f));
            Add("rightFinger" + i + "B", "rightFinger" + i + "A", new(1.12f, 1.47f + .015f * i, -.08f));
        }
        HumanoidMappingResult analysis = HumanoidSkeletonAnalyzer.Analyze(skeleton);
        Assert(analysis.IsHumanoid, "geometry rig should be humanoid");
        Assert(analysis.OverallConfidence > .8f, "geometry confidence");
        foreach (HumanoidBone role in HumanoidBoneCatalog.Required)
        {
            string expected = prefix + names[Enum.GetName(role)![..1].ToLowerInvariant() +
                Enum.GetName(role)![1..]].ToString("000");
            Assert(analysis.Mapping.GetBoneName(role) == expected, "mapping " + role);
        }
        HumanoidBoneMap integrated = HumanoidRigMapper.AutoMap(skeleton);
        Assert(HumanoidRigMapper.Validate(skeleton, integrated).IsReady, "AutoMap integration");
        Assert(HumanoidRigDiagnostics.Analyze(skeleton, integrated).Errors.Count == 0, "AutoMap hierarchy diagnostics");
        Assert(HumanoidReferencePose.Capture(skeleton, integrated).IsReady, "geometry reference pose");
        return (skeleton, integrated);
    }
    private static void Assert(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("Humanoid geometry: " + label);
    }
}

