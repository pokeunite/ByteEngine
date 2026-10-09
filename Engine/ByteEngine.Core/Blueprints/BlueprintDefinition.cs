using System.Numerics;
using ByteEngine.Core.Serialization.SerializationModels;

namespace ByteEngine.Core.Blueprints;

public enum BlueprintType
{
    GenericObject,
    Character
}

public sealed class BlueprintDefinition
{
    public ByteEngine.Core.Assets.AssetReference BaseBlueprint { get; set; } = ByteEngine.Core.Assets.AssetReference.Empty;
    public string BaseSnapshot { get; set; } = string.Empty;
    [System.Text.Json.Serialization.JsonIgnore]
    public List<string> InheritanceConflicts { get; } = new();
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Blueprint";
    public BlueprintType Type { get; set; }
    public GameObjectData Root { get; set; } = new();
    public List<GameObjectData> Children { get; set; } = new();
    public List<VariableData> Variables { get; set; } = new();
    public List<Guid> EventModules { get; set; } = new();
    public string? SkeletonAsset { get; set; }
}
