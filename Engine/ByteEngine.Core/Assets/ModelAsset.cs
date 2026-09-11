using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Core.Assets;

public sealed class ModelAsset
{
    public Guid Guid { get; }
    public Guid SourceAssetGuid { get; }
    public string Name { get; }
    public IReadOnlyList<ImportedNode> Nodes { get; }
    public IReadOnlyList<ImportedMesh> Meshes { get; }
    public IReadOnlyList<ImportedMaterial> Materials { get; }
    public SkeletonAsset? Skeleton { get; }
    public IReadOnlyList<ImportedAnimation> Animations { get; }

    internal ModelAsset(ImportedModel imported)
    {
        Guid = imported.Guid;
        SourceAssetGuid = imported.SourceAssetGuid;
        Name = imported.Name;
        Nodes = imported.Nodes;
        Meshes = imported.Meshes;
        Materials = imported.Materials;
        Skeleton = imported.Skeleton;
        Animations = imported.Animations;
    }
}
