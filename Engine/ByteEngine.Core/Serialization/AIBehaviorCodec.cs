using ByteEngine.Core.AI;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Plugins;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;
namespace ByteEngine.Core.Serialization;
internal sealed class AIBehaviorCodec : IComponentCodec
{
    private readonly ReflectionPluginComponentCodec<AIBehavior3D> _inner = new("AIBehavior3D");
    public string TypeName => "AIBehavior3D";
    public Type ComponentType => typeof(AIBehavior3D);
    public ComponentData Serialize(Component component, ComponentSerializationContext context) => _inner.Serialize(component,context);
    public Component Deserialize(ComponentData data, ComponentSerializationContext context)
    {
        var result=(AIBehavior3D)_inner.Deserialize(data,context);
        result.Configure(reference=> {
            var record=context.AssetDatabase.Resolve(reference);
            if(record?.Type!=AssetType.AIBehavior)throw new FileNotFoundException("Choose an AI Behaviour (.byteai) asset.");
            return BehaviorTreeSerializer.Load(record.FullPath);
        });
        return result;
    }
}
