using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;
namespace ByteEngine.Core.Serialization;
/// <summary>Inactive authored data retained until its codec becomes available.</summary>
public sealed class MissingComponent : Component
{
    private readonly ComponentData _original;
    public bool AuthoredEnabled => _original.Enabled;
    public string MissingType => _original.Type;
    public string? Reason { get; }
    public MissingComponent(ComponentData original, string? reason = null) { _original = Copy(original); Reason = reason; Enabled = false; }
    internal ComponentData Capture() => Copy(_original);
    private static ComponentData Copy(ComponentData data) => new()
    {
        Type = data.Type, Enabled = data.Enabled,
        Properties = (System.Text.Json.Nodes.JsonObject)data.Properties.DeepClone()
    };
}
