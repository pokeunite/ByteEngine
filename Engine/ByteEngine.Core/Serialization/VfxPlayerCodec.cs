using System.Text.Json.Nodes;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Vfx;
using ByteEngine.Core.Serialization.SerializationModels;

namespace ByteEngine.Core.Serialization;

internal sealed class VfxPlayerCodec : IComponentCodec
{
    public string TypeName => nameof(VfxPlayer);
    public Type ComponentType => typeof(VfxPlayer);
    public ComponentData Serialize(Component component,ComponentSerializationContext context)
    {
        var v=(VfxPlayer)component;
        return new() { Type=TypeName, Properties=new JsonObject {
            ["effectGuid"]=v.Effect.Guid.ToString(), ["effectPath"]=v.Effect.CachedProjectPath,
            ["preset"]=v.Preset.ToString(), ["playOnStart"]=v.PlayOnStart, ["seed"]=v.Seed,
            ["size"]=v.Size, ["intensity"]=v.Intensity, ["speed"]=v.PlaybackSpeed, ["distance"]=v.ViewDistance, ["destroyWhenFinished"]=v.DestroyWhenFinished
        }};
    }
    public Component Deserialize(ComponentData data,ComponentSerializationContext context)
    {
        var p=data.Properties;
        return new VfxPlayer {
            Effect=new AssetReference(Guid.TryParse(p["effectGuid"]?.GetValue<string>(),out var id)?id:Guid.Empty,p["effectPath"]?.GetValue<string>()??""),
            Preset=Enum.TryParse<VfxPreset>(p["preset"]?.GetValue<string>(),out var preset)?preset:VfxPreset.Sparks,
            PlayOnStart=p["playOnStart"]?.GetValue<bool>()??true, Seed=p["seed"]?.GetValue<int>()??1,
            Size=p["size"]?.GetValue<float>()??1, Intensity=p["intensity"]?.GetValue<float>()??1,
            PlaybackSpeed=p["speed"]?.GetValue<float>()??1, ViewDistance=p["distance"]?.GetValue<float>()??150,
            DestroyWhenFinished=p["destroyWhenFinished"]?.GetValue<bool>()??false
        };
    }
}
