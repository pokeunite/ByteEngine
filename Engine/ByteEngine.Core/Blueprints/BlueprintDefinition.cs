using System.Numerics;
using ByteEngine.Core.Serialization.SerializationModels;

namespace ByteEngine.Core.Blueprints;

public sealed class BlueprintDefinition
{
    public string Name { get; set; } = "Blueprint";
    public GameObjectData Root { get; set; } = new();
    public List<GameObjectData> Children { get; set; } = new();
    public List<Guid> EventModules { get; set; } = new();
    public string? SkeletonAsset { get; set; }
    public List<SocketDefinition> Sockets { get; set; } = new();
}

public sealed class SocketDefinition
{
    public string Name { get; set; } = "Socket";
    public string Bone { get; set; } = string.Empty;
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; } = Quaternion.Identity;
    public Vector3 Scale { get; set; } = Vector3.One;
    public string? PreviewAsset { get; set; }
}
