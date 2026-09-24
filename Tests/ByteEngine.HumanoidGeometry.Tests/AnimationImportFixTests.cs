using System.Numerics;
using System.Runtime.CompilerServices;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.HumanoidGeometry.Tests;

internal static class AnimationImportFixTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        VerifyFbxInterpolationSpikeFilter();
        VerifyArmAndFingerMapping();
        Console.WriteLine("Animation import spike filter and full hand mapping regressions passed.");
    }

    private static void VerifyFbxInterpolationSpikeFilter()
    {
        ImportedQuaternionTrack sampled = new();
        for (int frame = 0; frame <= 25; frame++)
            sampled.Keys.Add(new ImportedQuaternionKey(frame / 30f,
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, frame * .01f), default, default));
        sampled.Keys.Insert(7, new ImportedQuaternionKey(.21707776f,
            Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI), default, default));
        sampled.Keys.Insert(8, new ImportedQuaternionKey(.25f,
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, .075f), default, default));
        Check(FbxModelImporter.RemoveOffGridRotationKeys(sampled, 30f) == 1,
            "spurious between-frame FBX rotation was not removed");
        Check(sampled.Keys.Count == 27 && sampled.Keys.Any(key =>
            MathF.Abs(key.Time - .25f) < .0001f),
            "legitimate between-frame key changed");

        ImportedQuaternionTrack freeTimed = new();
        for (int i = 0; i < 14; i++)
            freeTimed.Keys.Add(new ImportedQuaternionKey(i * .043f,
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, i * .03f), default, default));
        Check(FbxModelImporter.RemoveOffGridRotationKeys(freeTimed, 30f) == 0 &&
            freeTimed.Keys.Count == 14, "irregular authored key times were removed");
    }

    private static void VerifyArmAndFingerMapping()
    {
        SkeletonAsset skeleton = new();
        List<ImportedNode> nodes = new();
        Dictionary<string, (int Index, Vector3 Position)> bones = new();
        void Add(string leaf, string? parent, Vector3 position)
        {
            string name = "mixamorig1:" + leaf;
            string? parentName = parent is null ? null : "mixamorig1:" + parent;
            int parentIndex = parentName is null ? -1 : bones[parentName].Index;
            Vector3 relative = parentName is null ? position : position - bones[parentName].Position;
            Matrix4x4.Invert(Matrix4x4.CreateTranslation(position), out Matrix4x4 inverse);
            skeleton.Bones.Add(new Bone { Name = name, ParentIndex = parentIndex, BindPose = inverse });
            nodes.Add(new ImportedNode { Key = name, Name = name, ParentKey = parentName,
                LocalTransform = Matrix4x4.CreateTranslation(relative) });
            bones[name] = (skeleton.Bones.Count - 1, position);
        }
        Add("Root", null, Vector3.Zero);
        Add("Hips", "Root", new(0, 100, 0));
        Add("Spine", "Hips", new(0, 110, 0));
        Add("Spine1", "Spine", new(0, 120, 0));
        Add("Spine2", "Spine1", new(0, 132, 0));
        Add("Neck", "Spine2", new(0, 145, 0));
        Add("Head", "Neck", new(0, 160, 0));
        foreach (string side in new[] { "Left", "Right" })
        {
            float sign = side == "Left" ? 1 : -1;
            Add(side + "UpLeg", "Hips", new(sign * 8, 94, 0));
            Add(side + "Leg", side + "UpLeg", new(sign * 8, 50, 0));
            Add(side + "Foot", side + "Leg", new(sign * 8, 6, 10));
            Add(side + "ToeBase", side + "Foot", new(sign * 8, 3, 25));
            Add(side + "Shoulder", "Spine2", new(sign * 6, 145, 0));
            Add(side + "Arm", side + "Shoulder", new(sign * 19, 143, 0));
            Add(side + "ForeArm", side + "Arm", new(sign * 44, 143, 0));
            Add(side + "Hand", side + "ForeArm", new(sign * 68, 143, 0));
            foreach (string finger in new[] { "Thumb", "Index", "Middle", "Ring", "Pinky" })
            {
                string parent = side + "Hand";
                for (int segment = 1; segment <= 3; segment++)
                {
                    string leaf = side + "Hand" + finger + segment;
                    Add(leaf, parent, new(sign * (68 + segment * 4), 143 + segment, 0));
                    parent = leaf;
                }
            }
        }

        HumanoidBoneMap map = HumanoidRigMapper.AutoMap(skeleton);
        Check(map.GetBoneName(HumanoidBone.LeftShoulder) == "mixamorig1:LeftShoulder" &&
            map.GetBoneName(HumanoidBone.LeftUpperArm) == "mixamorig1:LeftArm" &&
            map.GetBoneName(HumanoidBone.LeftLowerArm) == "mixamorig1:LeftForeArm" &&
            map.GetBoneName(HumanoidBone.LeftHand) == "mixamorig1:LeftHand", "left arm chain shifted");
        Check(map.GetBoneName(HumanoidBone.RightShoulder) == "mixamorig1:RightShoulder" &&
            map.GetBoneName(HumanoidBone.RightUpperArm) == "mixamorig1:RightArm" &&
            map.GetBoneName(HumanoidBone.RightLowerArm) == "mixamorig1:RightForeArm" &&
            map.GetBoneName(HumanoidBone.RightHand) == "mixamorig1:RightHand", "right arm chain shifted");
        Check(map.GetBoneName(HumanoidBone.LeftThumbProximal) == "mixamorig1:LeftHandThumb1" &&
            map.GetBoneName(HumanoidBone.RightLittleDistal) == "mixamorig1:RightHandPinky3",
            "finger roles absent");
        Check(HumanoidRigMapper.Validate(skeleton, map).IsReady, "full map did not validate");

    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Animation fix regression: " + message);
    }
}
