using System.Text.Json.Nodes;
using ByteEngine.Core.Variables;

namespace ByteEngine.Core.Serialization.SerializationModels;

public sealed class SceneData
{
    public string Name { get; set; } =
        "Untitled Scene";

    public Guid SceneId { get; set; } =
        Guid.NewGuid();

    public List<GameObjectData> GameObjects { get; set; } =
        new();

    public List<VariableData> Variables { get; set; } = new();
}

public sealed class GameObjectData
{
    public Guid Id { get; set; }

    public string Name { get; set; } =
        "GameObject";

    public bool Active { get; set; } =
        true;

    public List<Guid> Tags { get; set; } = new();

    public int Layer { get; set; }

    public Guid? ParentId { get; set; }

    public string ParentSocket { get; set; } = string.Empty;
    public ByteEngine.Core.Scene.AttachmentTransformRule AttachmentLocationRule { get; set; } = ByteEngine.Core.Scene.AttachmentTransformRule.KeepRelative;
    public ByteEngine.Core.Scene.AttachmentTransformRule AttachmentRotationRule { get; set; } = ByteEngine.Core.Scene.AttachmentTransformRule.KeepRelative;
    public ByteEngine.Core.Scene.AttachmentTransformRule AttachmentScaleRule { get; set; } = ByteEngine.Core.Scene.AttachmentTransformRule.KeepRelative;
    public TransformData? AttachmentOffset { get; set; }

    public TransformData Transform { get; set; } =
        new();

    public List<ComponentData> Components { get; set; } =
        new();

    public List<VariableData> Variables { get; set; } = new();
}

public sealed class VariableData
{
    public string Name { get; set; } = "Variable";
    public VariableValue Value { get; set; } = VariableValue.FromNumber();
}

public sealed class TransformData
{
    public Vector3Data? LocalPosition { get; set; }

    public QuaternionData? LocalRotation { get; set; }

    public Vector3Data? LocalScale { get; set; }

    public Vector2Data? Position { get; set; }

    public float? Rotation { get; set; }

    public Vector2Data? Size { get; set; }
}

public sealed class Vector3Data
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
}

public sealed class QuaternionData
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float W { get; set; } = 1.0f;
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
