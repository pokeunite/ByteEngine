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
    public Matrix4x4 BindPose { get; init; } = Matrix4x4.Identity;
}

public sealed class ImportedAnimation
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = "Animation";
    public float Duration { get; init; }
}
