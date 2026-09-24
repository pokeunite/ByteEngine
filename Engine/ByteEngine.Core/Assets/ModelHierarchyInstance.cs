using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Assets;

/// <summary>
/// Marks the root of one imported model hierarchy.
///
/// MeshRenderer references identify model sub-assets, but they cannot
/// distinguish two instances of the same model beneath one parent. This
/// marker keeps hierarchy-level operations scoped to the correct instance.
/// </summary>
public sealed class ModelHierarchyInstance : Component
{
    public AssetReference Model { get; set; } = AssetReference.Empty;

    public float AppliedImportScale { get; set; } = 1.0f;
    public bool AutoGrounded { get; set; }
}
