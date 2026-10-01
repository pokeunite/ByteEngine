using System.Text.Json.Nodes;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;

namespace ByteEngine.Core.Construction;

internal sealed class VehicleBuilder3DCodec : IComponentCodec
{
    public string TypeName => "VehicleBuilder3D";
    public Type ComponentType => typeof(VehicleBuilder3D);
    public ComponentData Serialize(Component component, ComponentSerializationContext context)
    {
        var builder=(VehicleBuilder3D)component;
        return new ComponentData { Type=TypeName, Properties=new JsonObject
        { ["partsDirectory"]=builder.PartsDirectory, ["maximumSpeed"]=builder.MaximumSpeed, ["roadGrip"]=builder.RoadGrip, ["driftGrip"]=builder.DriftGrip, ["freeBuilding"]=builder.FreeBuilding } };
    }
    public Component Deserialize(ComponentData data, ComponentSerializationContext context) => new VehicleBuilder3D
    {
        FreeBuilding=data.Properties["freeBuilding"]?.GetValue<bool>() ?? true,
        Assets=context.Assets, ProjectRoot=context.ProjectRoot,
        PartsDirectory=data.Properties["partsDirectory"]?.GetValue<string>() ?? "Assets/refinded parts",
        MaximumSpeed=data.Properties["maximumSpeed"]?.GetValue<float>() ?? 12,
        RoadGrip=data.Properties["roadGrip"]?.GetValue<float>() ?? 12,
        DriftGrip=data.Properties["driftGrip"]?.GetValue<float>() ?? .9f
    };
}
