using System.Numerics;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.HumanoidGeometry.Tests;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (args.Length == 0) return;
        string path = Path.GetFullPath(args[0]);
        AssetRecord record = new(Guid.NewGuid(), AssetType.Model3D,
            Path.GetFileName(path), path, path + ".meta", new AssetMetadata());
        ImportedModel model = ModelImporter.ForPath(path).Import(record, new ModelImporterSettings());
        Console.WriteLine($"meshes={model.Meshes.Count} nodes={model.Nodes.Count} clips={model.Animations.Count} bones={model.Skeleton?.Bones.Count ?? 0}");
        if (model.Skeleton is not { } skeleton) return;
        HumanoidMappingResult analysis = HumanoidSkeletonAnalyzer.Analyze(skeleton);
        HumanoidBoneMap integrated = HumanoidRigMapper.AutoMap(skeleton);
        Console.WriteLine($"geometry-confidence={analysis.OverallConfidence:F2} ready={analysis.IsHumanoid}; integrated-missing={string.Join(",", integrated.MissingRequiredBones())}");
        foreach (var pair in analysis.Mapping.Bones) Console.WriteLine($"geometry {pair.Key}={pair.Value}");
        foreach (var pair in integrated.Bones) Console.WriteLine($"integrated {pair.Key}={pair.Value}");
        if (!HumanoidRigMapper.Validate(skeleton, integrated).IsReady ||
            HumanoidRigDiagnostics.Analyze(skeleton, integrated).Errors.Count > 0)
            throw new InvalidOperationException("Imported source is not Humanoid-ready.");
        if (args.Contains("--bones", StringComparer.OrdinalIgnoreCase))
        for (int i = 0; i < skeleton.Bones.Count; i++)
        {
            Bone bone = skeleton.Bones[i];
            Matrix4x4.Invert(bone.BindPose, out Matrix4x4 bind);
            Console.WriteLine($"{i,3} parent={bone.ParentIndex,3} pos={bind.Translation} {bone.Name}");
        }
        Console.WriteLine($"validation={HumanoidRigMapper.Validate(skeleton, integrated).IsReady} diagnostic-errors={HumanoidRigDiagnostics.Analyze(skeleton, integrated).Errors.Count} reference-ready={HumanoidReferencePose.Capture(skeleton, integrated).IsReady}");
        if (args.Length > 1)
        {
            string targetPath = Path.GetFullPath(args[1]);
            AssetRecord targetRecord = new(Guid.NewGuid(), AssetType.Model3D,
                Path.GetFileName(targetPath), targetPath, targetPath + ".meta", new AssetMetadata());
            ImportedModel target = ModelImporter.ForPath(targetPath).Import(targetRecord, new ModelImporterSettings());
            SkeletonAsset targetSkeleton = target.Skeleton ?? throw new Exception("No target skeleton");
            HumanoidBoneMap targetMap = HumanoidRigMapper.AutoMap(targetSkeleton);
            Console.WriteLine($"target-ready={HumanoidRigMapper.Validate(targetSkeleton, targetMap).IsReady} target-errors={HumanoidRigDiagnostics.Analyze(targetSkeleton, targetMap).Errors.Count}");
            if (!HumanoidRigMapper.Validate(targetSkeleton, targetMap).IsReady ||
                HumanoidRigDiagnostics.Analyze(targetSkeleton, targetMap).Errors.Count > 0)
                throw new InvalidOperationException("Imported target is not Humanoid-ready.");
            ImportedAnimation clip = model.Animations.First();
            HumanoidRetargetPose pose = HumanoidRetargeter.Retarget(skeleton, integrated,
                HumanoidReferencePose.Capture(skeleton, integrated), clip, model.Nodes, model.Meshes,
                clip.Duration * .5f, targetSkeleton, targetMap,
                HumanoidReferencePose.Capture(targetSkeleton, targetMap), false);
            Console.WriteLine($"retarget-pose-matrices={pose.ModelMatrices.Count} finite={pose.ModelMatrices.All(m => float.IsFinite(m.Translation.X) && float.IsFinite(m.Translation.Y) && float.IsFinite(m.Translation.Z))}");
            ImportedAnimation baked = HumanoidRetargetClipBuilder.Build(skeleton, integrated,
                HumanoidReferencePose.Capture(skeleton, integrated), clip, model.Nodes, model.Meshes,
                targetSkeleton, targetMap, HumanoidReferencePose.Capture(targetSkeleton, targetMap),
                target.Nodes, target.Meshes, record.Guid, targetRecord.Guid, "Run_F_Retargeted", 30f);
            Console.WriteLine($"baked-channels={baked.Channels.Count} baked-duration={baked.Duration:F2}");
            if (baked.Channels.Count == 0 || baked.Duration <= 0)
                throw new InvalidOperationException("Retarget bake emitted no usable tracks.");
        }        foreach (ImportedAnimation clip in model.Animations)
            Console.WriteLine($"clip={clip.Name} channels={clip.Channels.Count} duration={clip.Duration:F2}");
    }
}