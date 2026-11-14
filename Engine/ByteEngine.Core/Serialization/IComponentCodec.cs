using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;

namespace ByteEngine.Core.Serialization;

public sealed class ComponentSerializationContext
{
    public string ProjectRoot { get; }

    public AssetManager Assets { get; }

    public AssetDatabase AssetDatabase { get; }

    public Action<string>? WarningSink { get; }

    /// <summary>False for headless authoring: preserve references without creating GPU resources.</summary>
    public bool LoadGraphicsAssets { get; }

    public ComponentSerializationContext(
        string projectRoot,
        AssetDatabase assetDatabase,
        AssetManager assets,
        Action<string>? warningSink,
        bool loadGraphicsAssets = true)
    {
        ProjectRoot =
            Path.GetFullPath(
                projectRoot
            );

        Assets = assets;
        AssetDatabase = assetDatabase;
        WarningSink = warningSink;
        LoadGraphicsAssets = loadGraphicsAssets;
    }
}

public interface IComponentCodec
{
    string TypeName { get; }

    Type ComponentType { get; }

    ComponentData Serialize(
        Component component,
        ComponentSerializationContext context);

    Component Deserialize(
        ComponentData data,
        ComponentSerializationContext context);
}
