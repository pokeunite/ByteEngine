using System.Text.Json.Nodes;
using ByteEngine.Core.Variables;

namespace ByteEngine.Core.Serialization.SerializationModels;

public sealed class SceneData
{
    public const int CurrentSchemaVersion = 1;
    // Zero denotes an unversioned legacy scene.
    public int SchemaVersion { get; set; }
    public bool FixedSimulation { get; set; }
    public double FixedStepSeconds { get; set; } = 1.0 / 60;
    public int MaximumCatchUpSteps { get; set; } = 8;
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
    public GameObjectData DeepClone()=>new() {
        Id=Id,Name=Name,Active=Active,Tags=Tags.ToList(),Layer=Layer,ParentId=ParentId,ParentSocket=ParentSocket,
        AttachmentLocationRule=AttachmentLocationRule,AttachmentRotationRule=AttachmentRotationRule,AttachmentScaleRule=AttachmentScaleRule,
        AttachmentOffset=CopyTransform(AttachmentOffset),Transform=CopyTransform(Transform)!,
        Components=Components.Select(c=>new ComponentData {Type=c.Type,Enabled=c.Enabled,Properties=(JsonObject)c.Properties.DeepClone()}).ToList(),
        Variables=Variables.Select(v=>new VariableData {Name=v.Name,Value=v.Value.Clone()}).ToList()
    };
    private static TransformData? CopyTransform(TransformData? t)=>t==null?null:new() {
        LocalPosition=t.LocalPosition is {} p?new() {X=p.X,Y=p.Y,Z=p.Z}:null,
        LocalRotation=t.LocalRotation is {} r?new() {X=r.X,Y=r.Y,Z=r.Z,W=r.W}:null,
        LocalScale=t.LocalScale is {} s?new() {X=s.X,Y=s.Y,Z=s.Z}:null,
        Position=t.Position is {} p2?new() {X=p2.X,Y=p2.Y}:null,Rotation=t.Rotation,
        Size=t.Size is {} s2?new() {X=s2.X,Y=s2.Y}:null
    };
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
