using ByteEngine.Core.Plugins;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;
namespace Karlson.Plugin;
public sealed class KarlsonDirectorCodec:IComponentCodec
{
    readonly ReflectionPluginComponentCodec<KarlsonDirector3D> _inner=new("bytebard.karlson.KarlsonDirector3D");
    public string TypeName=>"bytebard.karlson.KarlsonDirector3D";
    public Type ComponentType=>typeof(KarlsonDirector3D);
    public ComponentData Serialize(Component component,ComponentSerializationContext context)=>_inner.Serialize(component,context);
    public Component Deserialize(ComponentData data,ComponentSerializationContext context){var component=(KarlsonDirector3D)_inner.Deserialize(data,context);component.ProjectRoot=context.ProjectRoot;return component;}
}
