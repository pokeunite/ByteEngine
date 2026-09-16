using System.Numerics;

namespace ByteEngine.Core.Assets.Importers;

public sealed class ImportedModel
{
    public Guid Guid { get; init; }
    public Guid SourceAssetGuid { get; init; }
    public string Name { get; init; } = "Model";
    public List<ImportedNode> Nodes { get; init; } = new();
    public List<ImportedMesh> Meshes { get; init; } = new();
    public List<ImportedMaterial> Materials { get; init; } = new();
    public SkeletonAsset? Skeleton { get; init; }
    public List<ImportedAnimation> Animations { get; init; } = new();
}

public sealed class ImportedNode
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = "Node";
    public string? ParentKey { get; init; }
    public Matrix4x4 LocalTransform { get; init; } = Matrix4x4.Identity;
    public List<string> MeshKeys { get; init; } = new();
}

public sealed class ImportedMesh
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = "Mesh";
    public float[] Vertices { get; init; } = Array.Empty<float>();
    public uint[] Indices { get; init; } = Array.Empty<uint>();
    public string? MaterialKey { get; init; }
    public Vector4[] JointIndices { get; init; } = Array.Empty<Vector4>();
    public Vector4[] JointWeights { get; init; } = Array.Empty<Vector4>();
}

public sealed class ImportedMaterial
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = "Material";
    public Vector4 BaseColor { get; init; } = Vector4.One;
    public float Metallic { get; init; }
    public float Roughness { get; init; } = 1f;
    public ImportedTexture? BaseColorTexture { get; init; }
    public ImportedTexture? NormalTexture { get; init; }
}

public sealed class ImportedTexture
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = "Texture";
    public byte[] EncodedData { get; init; } = Array.Empty<byte>();
    public string? SourcePath { get; init; }
}

public sealed class SkeletonAsset
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = "Skeleton";
    public List<Bone> Bones { get; init; } = new();
}

public sealed class Bone
{
    public string Name { get; init; } = "Bone";
    public int ParentIndex { get; init; } = -1;

    /// <summary>
    /// Inverse bind matrix supplied by the source asset.
    /// </summary>
    public Matrix4x4 BindPose { get; init; } = Matrix4x4.Identity;
}

public enum ImportedAnimationInterpolation
{
    Step,
    Linear,
    CubicSpline
}

public sealed class ImportedVectorTrack
{
    public ImportedAnimationInterpolation Interpolation { get; init; } =
        ImportedAnimationInterpolation.Linear;

    public List<ImportedVectorKey> Keys { get; init; } = new();
}

public sealed class ImportedQuaternionTrack
{
    public ImportedAnimationInterpolation Interpolation { get; init; } =
        ImportedAnimationInterpolation.Linear;

    public List<ImportedQuaternionKey> Keys { get; init; } = new();
}

public readonly record struct ImportedVectorKey(
    float Time,
    Vector3 Value,
    Vector3 InTangent,
    Vector3 OutTangent);

public readonly record struct ImportedQuaternionKey(
    float Time,
    Quaternion Value,
    Quaternion InTangent,
    Quaternion OutTangent);

public sealed class ImportedAnimationChannel
{
    public string NodeName { get; init; } = string.Empty;
    public ImportedVectorTrack? Translation { get; init; }
    public ImportedQuaternionTrack? Rotation { get; init; }
    public ImportedVectorTrack? Scale { get; init; }

    public bool HasKeys =>
        Translation?.Keys.Count > 0 ||
        Rotation?.Keys.Count > 0 ||
        Scale?.Keys.Count > 0;
}

public sealed class ImportedAnimation
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = "Animation";
    public float Duration { get; init; }
    public List<ImportedAnimationChannel> Channels { get; init; } = new();

    public ImportedAnimationChannel? FindChannel(string nodeName)
    {
        if (string.IsNullOrWhiteSpace(nodeName))
        {
            return null;
        }

        return Channels.FirstOrDefault(
            channel =>
                string.Equals(
                    channel.NodeName,
                    nodeName,
                    StringComparison.Ordinal))
            ?? Channels.FirstOrDefault(
                channel =>
                    string.Equals(
                        channel.NodeName,
                        nodeName,
                        StringComparison.OrdinalIgnoreCase));
    }
}
