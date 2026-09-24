using System.Numerics;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.HumanoidGeometry.Tests;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "--animation-report",
                StringComparison.OrdinalIgnoreCase))
        {
            AnimationDiagnosticReport.Run(args[1..]);
            return;
        }

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
        foreach (ImportedAnimation clip in model.Animations)
            Console.WriteLine($"clip={clip.Name} channels={clip.Channels.Count} duration={clip.Duration:F2}");
    }
}
