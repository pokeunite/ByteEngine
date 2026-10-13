using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Plugins;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Classification;
using ByteEngine.Core.Serialization.SerializationModels;
namespace ByteEngine.Core.Serialization;
internal sealed class HitscanEmitter3DCodec:IComponentCodec
{
    readonly ReflectionPluginComponentCodec<HitscanEmitter3D> _inner=new("HitscanEmitter3D");
    public string TypeName=>"HitscanEmitter3D";
    public Type ComponentType=>typeof(HitscanEmitter3D);
    public ComponentData Serialize(Component component,ComponentSerializationContext context){var data=_inner.Serialize(component,context);data.Properties["hitMaskBits"]=(long)((HitscanEmitter3D)component).HitMask.Bits;return data;}
    public Component Deserialize(ComponentData data,ComponentSerializationContext context){var c=(HitscanEmitter3D)_inner.Deserialize(data,context);if(data.Properties["hitMaskBits"] is {} bits)c.HitMask=LayerMask.FromBits(unchecked((uint)bits.GetValue<long>()));return c;}
}
