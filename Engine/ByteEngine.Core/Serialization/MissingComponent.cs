using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;
namespace ByteEngine.Core.Serialization;
/// <summary>Inactive authored data retained until its codec becomes available.</summary>
public sealed class MissingComponent : Component
{
    private readonly ComponentData _original;
    public string MissingType => _original.Type;
    public MissingComponent(ComponentData original) { _original = Copy(original); Enabled = false; }
    internal ComponentData Capture() => Copy(_original);
    private static ComponentData Copy(ComponentData data) => new()
    {
        Type = data.Type, Enabled = data.Enabled,
        Properties = (System.Text.Json.Nodes.JsonObject)data.Properties.DeepClone()
    };
}
