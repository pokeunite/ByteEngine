using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.HumanoidGeometry.Tests;

/// <summary>
/// Read-only, reproducible import, pose, and native channel diagnostics. Never refreshes the asset database.
/// </summary>
internal static class AnimationDiagnosticReport
{
    private sealed record Input(string Label, string Path, ImportedModel Raw, ModelAsset Effective,
        ModelImporterSettings SavedSettings);

    public static void Run(string[] args)
    {
        Dictionary<string, string> options = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i += 2)
        {
            if (i + 1 >= args.Length || !args[i].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("Usage: --animation-report --unity <fbx> --blender <fbx> --target <fbx> [--out <txt>]");
            options[args[i][2..]] = args[i + 1];
        }
        foreach (string required in new[] { "unity", "blender", "target" })
            if (!options.ContainsKey(required))
                throw new ArgumentException($"Missing --{required}. Usage: --animation-report --unity <fbx> --blender <fbx> --target <fbx> [--out <txt>]");

        string output = Path.GetFullPath(options.GetValueOrDefault("out") ?? Path.Combine(
            "Tests", "ByteEngine.HumanoidGeometry.Tests", "Reports",
            $"animation-diagnostics-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt"));
        string? parent = Path.GetDirectoryName(output);
        if (parent is not null) Directory.CreateDirectory(parent);
        var log = new StringBuilder();
        void Line(string value = "") => log.AppendLine(value);
        Line("BYTEENGINE ANIMATION DIAGNOSTIC REPORT");
        Line($"UTC: {DateTime.UtcNow:O}");
        Line($"Core assembly: {typeof(ModelAsset).Assembly.Location}");
        Line("All source FBX and .meta files are read-only. This report does not run editor rendering, playback root-motion correction, or GPU skinning.");

        Input?[] inputs = new Input?[3];
        string[] labels = { "UNITY", "BLENDER", "TARGET" };
        for (int i = 0; i < labels.Length; i++)
        {
            string path = Path.GetFullPath(options[labels[i].ToLowerInvariant()]);
            try
            {
                inputs[i] = Load(labels[i], path, Line);
                Describe(inputs[i]!, Line);
            }
            catch (Exception ex)
            {
                Line($"\n[{labels[i]}] IMPORT FAILED: {ex}");
            }
        }

        if (inputs[2] is { } target)
            for (int i = 0; i < 2; i++)
                if (inputs[i] is { } source)
                {
                    try { CompareNativeRig(source, target, Line); }
                    catch (Exception ex) { Line($"\n[{source.Label} -> TARGET] COMPARISON FAILED: {ex}"); }
                }

        File.WriteAllText(output, log.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"Animation diagnostic report: {output}");
        Console.WriteLine($"Bytes: {new FileInfo(output).Length}");
    }

    private static Input Load(string label, string path, Action<string> line)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("FBX not found", path);
        line($"\n[{label}] {path}");
        line($"FBX bytes={new FileInfo(path).Length} sha256={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))}");
        string metaPath = path + ".meta";
        AssetMetadata metadata = new();
        if (File.Exists(metaPath))
        {
            line($"META bytes={new FileInfo(metaPath).Length} sha256={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(metaPath)))}");
            var json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip };
            json.Converters.Add(new JsonStringEnumConverter());
            metadata = JsonSerializer.Deserialize<AssetMetadata>(File.ReadAllText(metaPath), json) ?? new();
        }
        else line("META missing: importer defaults used in memory");
        ModelImporterSettings settings = metadata.ModelImporter ?? new();
        settings.Normalize();
        ModelImporterSettings saved = settings.Clone();
        var record = new AssetRecord(metadata.Guid == Guid.Empty ? Guid.NewGuid() : metadata.Guid,
            AssetType.Model3D, Path.GetFileName(path), path, metaPath, metadata);
        ImportedModel raw = ModelImporter.ForPath(path).Import(record, settings);
        ModelAsset effective = new(raw, settings);
        return new Input(label, path, raw, effective, saved);
    }

    private static void Describe(Input input, Action<string> line)
    {
        ImportedModel model = input.Raw;
        ModelAsset effective = input.Effective;
        SkeletonAsset? skeleton = model.Skeleton;
        line($"Saved rig={input.SavedSettings.RigType} auto={input.SavedSettings.AutoDetectHumanoidRig} importScale={F(input.SavedSettings.ImportScale)} importAnimations={input.SavedSettings.ImportAnimations}");
        line($"Effective rig={effective.RigType} autoDetected={effective.HumanoidRigWasAutoDetected}");
        line($"Counts: nodes={model.Nodes.Count} meshes={model.Meshes.Count} bones={skeleton?.Bones.Count ?? 0} clips={model.Animations.Count}");
        if (skeleton is not null)
        {
            WriteMap("SAVED MAP", input.SavedSettings.HumanoidMapping, line);
            WriteMap("AUTO MAP", HumanoidRigMapper.AutoMap(skeleton), line);
            WriteMap("EFFECTIVE MAP", effective.HumanoidMapping, line);
            var validation = HumanoidRigMapper.Validate(skeleton, effective.HumanoidMapping);
            var diagnostics = HumanoidRigDiagnostics.Analyze(skeleton, effective.HumanoidMapping);
            line($"Validation ready={validation.IsReady} mapped={validation.MappedBoneCount} required={validation.RequiredMappedCount} missing={string.Join(",", validation.MissingRequiredBones)} invalid={string.Join(",", validation.InvalidMappedBones)}");
            foreach (string error in diagnostics.Errors) line($"  ERROR {error}");
            foreach (string warning in diagnostics.Warnings) line($"  WARNING {warning}");
            line("BONES: index | parent | name | bind position | inverse bind matrix");
            for (int i = 0; i < skeleton.Bones.Count; i++)
            {
                Bone bone = skeleton.Bones[i];
                Matrix4x4.Invert(bone.BindPose, out Matrix4x4 bind);
                line($"  {i,3} | {bone.ParentIndex,3} | {bone.Name} | {V(bind.Translation)} | {M(bone.BindPose)}");
            }
        }
        line("NODES: key | parent key | name | local TRS | mesh keys");
        foreach (ImportedNode node in model.Nodes)
        {
            Matrix4x4.Decompose(node.LocalTransform, out Vector3 scale, out Quaternion rotation, out Vector3 position);
            line($"  {node.Key} | {node.ParentKey} | {node.Name} | T={V(position)} R={Q(rotation)} S={V(scale)} | {string.Join(",", node.MeshKeys)}");
        }
        line("MESHES: name | vertex count | triangles | weighted vertices | bad joint references | weight range");
        foreach (ImportedMesh mesh in model.Meshes)
        {
            int count = mesh.Vertices.Length / 8, weighted = 0, bad = 0;
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < Math.Min(count, Math.Min(mesh.JointIndices.Length, mesh.JointWeights.Length)); i++)
            {
                Vector4 w = mesh.JointWeights[i], j = mesh.JointIndices[i];
                float sum = w.X + w.Y + w.Z + w.W;
                if (sum > 0.000001f) weighted++;
                min = Math.Min(min, sum); max = Math.Max(max, sum);
                for (int k = 0; k < 4; k++)
                    if (w[k] > 0 && (j[k] < 0 || j[k] >= (skeleton?.Bones.Count ?? 0))) bad++;
            }
            line($"  {mesh.Name} key={mesh.Key} vertices={count} triangles={mesh.Indices.Length / 3} weighted={weighted} badJoints={bad} weightRange={F(min == float.MaxValue ? 0 : min)}..{F(max == float.MinValue ? 0 : max)}");
        }
        foreach (ImportedAnimation clip in model.Animations)
        {
            var names = model.Nodes.Select(n => n.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            int matched = clip.Channels.Count(c => names.Contains(c.NodeName));
            int fingers = clip.Channels.Count(c => IsFinger(c.NodeName));
            line($"CLIP {clip.Name} key={clip.Key} duration={F(clip.Duration)} channels={clip.Channels.Count} nodeMatched={matched} fingerChannels={fingers}");
            foreach (ImportedAnimationChannel channel in clip.Channels)
            {
                line($"  TRACK {channel.NodeName} nodeMatch={names.Contains(channel.NodeName)} T={Track(channel.Translation)} R={Track(channel.Rotation)} S={Track(channel.Scale)}");
                if ((channel.NodeName.Contains("Spine", StringComparison.OrdinalIgnoreCase) ||
                    channel.NodeName.Contains("Shoulder", StringComparison.OrdinalIgnoreCase)) &&
                    channel.Rotation is { } rotations)
                    foreach (ImportedQuaternionKey key in rotations.Keys)
                        line($"    ROTATION KEY {channel.NodeName} t={F(key.Time)} q={Q(key.Value)}");
            }
            foreach (float fraction in new[] { 0f, .25f, .5f, .75f, 1f })
            {
                float time = clip.Duration * fraction;
                try { SamplePose(model, clip, time, line); }
                catch (Exception ex) { line($"  POSE at {F(time)} FAILED: {ex}"); }
            }
        }
    }

    private static void CompareNativeRig(Input source, Input target, Action<string> line)
    {
        line($"\n[{source.Label} -> TARGET]");
        SkeletonAsset? a = source.Raw.Skeleton, b = target.Raw.Skeleton;
        if (a is null || b is null) { line("Comparison unavailable: missing skeleton"); return; }
        var targetBones = b.Bones.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        float largest = 0; string largestBone = "";
        int common = 0, parentMismatch = 0;
        foreach (Bone bone in a.Bones)
            if (targetBones.TryGetValue(bone.Name, out Bone? other))
            {
                common++;
                float difference = MaxMatrixDifference(bone.BindPose, other.BindPose);
                if (difference > largest) { largest = difference; largestBone = bone.Name; }
                string? pa = bone.ParentIndex >= 0 && bone.ParentIndex < a.Bones.Count ? a.Bones[bone.ParentIndex].Name : null;
                string? pb = other.ParentIndex >= 0 && other.ParentIndex < b.Bones.Count ? b.Bones[other.ParentIndex].Name : null;
                if (!string.Equals(pa, pb, StringComparison.OrdinalIgnoreCase)) parentMismatch++;
            }
        line($"Common bones={common}/{a.Bones.Count}; parent-name mismatches={parentMismatch}; max inverse-bind element delta={F(largest)} at {largestBone}");
        foreach (HumanoidBone role in Enum.GetValues<HumanoidBone>())
        {
            string? sa = source.Effective.HumanoidMapping.GetBoneName(role), tb = target.Effective.HumanoidMapping.GetBoneName(role);
            if (sa is not null || tb is not null) line($"  ROLE {role}: source={sa ?? "<none>"} target={tb ?? "<none>"}");
        }
        ImportedAnimation? clip = source.Raw.Animations.FirstOrDefault();
        if (clip is null) { line("No source clip"); return; }
        var targetNames = target.Raw.Nodes.Select(n => n.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        line($"Direct target node matches={clip.Channels.Count(c => targetNames.Contains(c.NodeName))}/{clip.Channels.Count}; direct finger matches={clip.Channels.Count(c => IsFinger(c.NodeName) && targetNames.Contains(c.NodeName))}/{clip.Channels.Count(c => IsFinger(c.NodeName))}");
        foreach (ImportedAnimationChannel channel in clip.Channels.Where(c => !targetNames.Contains(c.NodeName)))
            line($"  UNMATCHED target node: {channel.NodeName}");
    }

    private static void SamplePose(ImportedModel model, ImportedAnimation clip, float time, Action<string> line)
    {
        var nodes = model.Nodes;
        var byKey = nodes.Select((node, index) => (node.Key, index)).GroupBy(x => x.Key)
            .ToDictionary(x => x.Key, x => x.First().index, StringComparer.Ordinal);
        Matrix4x4[] local = new Matrix4x4[nodes.Count], global = new Matrix4x4[nodes.Count];
        int[] state = new int[nodes.Count];
        for (int i = 0; i < nodes.Count; i++)
        {
            ImportedNode node = nodes[i];
            Matrix4x4.Decompose(node.LocalTransform, out Vector3 s, out Quaternion r, out Vector3 p);
            ImportedAnimationChannel? channel = clip.FindChannel(node.Name);
            local[i] = Matrix4x4.CreateScale(AnimationPoseSampler.Sample(channel?.Scale, time, s)) *
                Matrix4x4.CreateFromQuaternion(AnimationPoseSampler.Sample(channel?.Rotation, time, r)) *
                Matrix4x4.CreateTranslation(AnimationPoseSampler.Sample(channel?.Translation, time, p));
        }
        Matrix4x4 Resolve(int i)
        {
            if (state[i] == 2) return global[i];
            if (state[i] == 1) return global[i] = local[i];
            state[i] = 1;
            global[i] = nodes[i].ParentKey is { } key && byKey.TryGetValue(key, out int parent)
                ? local[i] * Resolve(parent) : local[i];
            state[i] = 2;
            return global[i];
        }
        for (int i = 0; i < nodes.Count; i++) Resolve(i);
        line($"  POSE t={F(time)} (native node sampling, model space; before root-motion correction)");
        foreach (int i in Enumerable.Range(0, nodes.Count))
            if ((nodes[i].Name.Contains("Spine", StringComparison.OrdinalIgnoreCase) ||
                nodes[i].Name.Contains("Shoulder", StringComparison.OrdinalIgnoreCase) ||
                nodes[i].Name.Contains("Arm", StringComparison.OrdinalIgnoreCase) ||
                nodes[i].Name.Contains("Hand", StringComparison.OrdinalIgnoreCase)) &&
                !IsFinger(nodes[i].Name))
            {
                Matrix4x4.Decompose(local[i], out Vector3 scale, out Quaternion rotation, out Vector3 position);
                line($"    LOCAL {nodes[i].Name} T={V(position)} R={Q(rotation)} S={V(scale)}");
            }
        foreach (Bone bone in model.Skeleton?.Bones ?? new List<Bone>())
        {
            int i = Array.FindIndex(nodes.ToArray(), n => string.Equals(n.Name, bone.Name, StringComparison.OrdinalIgnoreCase));
            if (i >= 0 && (bone.Name.Contains("Hips", StringComparison.OrdinalIgnoreCase) ||
                bone.Name.Contains("Spine", StringComparison.OrdinalIgnoreCase) ||
                bone.Name.Contains("Shoulder", StringComparison.OrdinalIgnoreCase) ||
                bone.Name.Contains("Arm", StringComparison.OrdinalIgnoreCase) ||
                bone.Name.Contains("Hand", StringComparison.OrdinalIgnoreCase) ||
                bone.Name.Contains("Thumb", StringComparison.OrdinalIgnoreCase) ||
                bone.Name.Contains("Index", StringComparison.OrdinalIgnoreCase)))
                line($"    JOINT {bone.Name} node={i} position={V(global[i].Translation)}");
        }
        foreach (ImportedMesh mesh in model.Meshes)
        {
            int meshNode = Array.FindIndex(nodes.ToArray(), n => n.MeshKeys.Contains(mesh.Key));
            if (meshNode < 0 || model.Skeleton is null || mesh.JointWeights.Length == 0) continue;
            Matrix4x4.Invert(global[meshNode], out Matrix4x4 inverseMesh);
            var boneGlobals = model.Skeleton.Bones.Select(b =>
            {
                int i = Array.FindIndex(nodes.ToArray(), n => string.Equals(n.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                return i < 0 ? Matrix4x4.Identity : b.BindPose * global[i] * inverseMesh;
            }).ToArray();
            Vector3 min = new(float.PositiveInfinity), max = new(float.NegativeInfinity);
            int count = Math.Min(mesh.Vertices.Length / 8, Math.Min(mesh.JointWeights.Length, mesh.JointIndices.Length));
            int sampled = 0, nonFinite = 0;
            for (int vertex = 0; vertex < count; vertex += Math.Max(1, count / 256))
            {
                int offset = vertex * 8;
                Vector3 source = new(mesh.Vertices[offset], mesh.Vertices[offset + 1], mesh.Vertices[offset + 2]);
                Vector4 weights = mesh.JointWeights[vertex], indices = mesh.JointIndices[vertex];
                Vector3 posed = Vector3.Zero; float total = 0;
                for (int k = 0; k < 4; k++)
                    if (weights[k] > 0 && indices[k] >= 0 && indices[k] < boneGlobals.Length)
                    { posed += Vector3.Transform(source, boneGlobals[(int)indices[k]]) * weights[k]; total += weights[k]; }
                if (total > 0.000001f) posed /= total; else posed = source;
                posed = Vector3.Transform(posed, global[meshNode]);
                if (!float.IsFinite(posed.X) || !float.IsFinite(posed.Y) || !float.IsFinite(posed.Z)) { nonFinite++; continue; }
                min = Vector3.Min(min, posed); max = Vector3.Max(max, posed); sampled++;
            }
            line($"    SKIN {mesh.Name} sampledVertices={sampled} nonFinite={nonFinite} boundsMin={V(min)} boundsMax={V(max)}");
        }
    }

    private static void WriteMap(string name, HumanoidBoneMap map, Action<string> line)
    {
        line($"{name} ({map.MappedCount} roles):");
        foreach (var pair in map.Bones.OrderBy(x => x.Key.ToString())) line($"  {pair.Key} -> {pair.Value}");
    }
    private static string Track(ImportedVectorTrack? t) => t is null ? "-" :
        $"{t.Keys.Count} [{F(t.Keys.FirstOrDefault().Time)}..{F(t.Keys.LastOrDefault().Time)}] first={V(t.Keys.FirstOrDefault().Value)} last={V(t.Keys.LastOrDefault().Value)}";
    private static string Track(ImportedQuaternionTrack? t) => t is null ? "-" :
        $"{t.Keys.Count} [{F(t.Keys.FirstOrDefault().Time)}..{F(t.Keys.LastOrDefault().Time)}] first={Q(t.Keys.FirstOrDefault().Value)} last={Q(t.Keys.LastOrDefault().Value)}";
    private static bool IsFinger(string s) => new[] { "Thumb", "Index", "Middle", "Ring", "Pinky", "Little" }.Any(x => s.Contains(x, StringComparison.OrdinalIgnoreCase));
    private static string F(float value) => value.ToString("G9", CultureInfo.InvariantCulture);
    private static string V(Vector3 value) => $"({F(value.X)},{F(value.Y)},{F(value.Z)})";
    private static string Q(Quaternion value) => $"({F(value.X)},{F(value.Y)},{F(value.Z)},{F(value.W)})";
    private static string M(Matrix4x4 m) => string.Join(",", new[] { m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44 }.Select(F));
    private static float MaxMatrixDifference(Matrix4x4 a, Matrix4x4 b)
    {
        float[] x = { a.M11,a.M12,a.M13,a.M14,a.M21,a.M22,a.M23,a.M24,a.M31,a.M32,a.M33,a.M34,a.M41,a.M42,a.M43,a.M44 };
        float[] y = { b.M11,b.M12,b.M13,b.M14,b.M21,b.M22,b.M23,b.M24,b.M31,b.M32,b.M33,b.M34,b.M41,b.M42,b.M43,b.M44 };
        return x.Zip(y, (left, right) => MathF.Abs(left - right)).Max();
    }
}
