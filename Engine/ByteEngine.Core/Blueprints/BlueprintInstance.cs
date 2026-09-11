using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Blueprints;

public sealed class BlueprintInstance : Component
{
    public AssetReference Blueprint { get; set; } = AssetReference.Empty;
    public Guid InstanceId { get; set; } = Guid.NewGuid();
}
