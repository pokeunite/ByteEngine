using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Graphics.ThreeD;

public sealed class SkeletalMeshRenderer : Component
{
    public AssetReference Model { get; set; } = AssetReference.Empty;
    public string? SkeletonKey { get; set; }
    public List<string> MaterialKeys { get; set; } = new();
    public bool Visible { get; set; } = true;
}
