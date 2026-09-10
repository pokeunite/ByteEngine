using System.Text.Json.Nodes;

namespace ByteEngine.Core.Serialization.SerializationModels;

public sealed class SceneData
{
    public string Name { get; set; } =
        "Untitled Scene";

    public Guid SceneId { get; set; } =
        Guid.NewGuid();

    public List<GameObjectData> GameObjects { get; set; } =
        new();
}

public sealed class GameObjectData
{
    public Guid Id { get; set; }

    public string Name { get; set; } =
        "GameObject";

    public bool Active { get; set; } =
        true;

    public TransformData Transform { get; set; } =
        new();

    public List<ComponentData> Components { get; set; } =
        new();
}

public sealed class TransformData
{
    public Vector2Data Position { get; set; } =
        new();

    public float Rotation { get; set; }

    public Vector2Data Size { get; set; } =
        new()
        {
            X = 64.0f,
            Y = 64.0f
        };
}

public sealed class Vector2Data
{
    public float X { get; set; }

    public float Y { get; set; }
}

public sealed class ComponentData
{
    public string Type { get; set; } =
        string.Empty;

    public bool Enabled { get; set; } =
        true;

    public JsonObject Properties { get; set; } =
        new();
}
