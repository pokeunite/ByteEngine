using System.Numerics;
using System.Text.Json.Serialization;

namespace ByteEngine.Core.Variables;

public sealed class VariableValue
{
    public VariableType Type { get; set; }
    public double Number { get; set; }
    public string String { get; set; } = string.Empty;
    public bool Boolean { get; set; }
    public Vector2 Vector2 { get; set; }
    public Vector3 Vector3 { get; set; }
    public List<VariableValue> Items { get; set; } = new();
    public Dictionary<string,VariableValue> Children { get; set; } = new(StringComparer.Ordinal);
    public static VariableValue FromNull()=>new(){Type=VariableType.Null};
    public static VariableValue FromArray(IEnumerable<VariableValue>? values=null) => new(){Type=VariableType.Array,Items=values?.Select(v=>v.Clone()).ToList()??new()};
    public static VariableValue FromStructure(IEnumerable<KeyValuePair<string,VariableValue>>? values=null) => new(){Type=VariableType.Structure,Children=values?.ToDictionary(p=>p.Key,p=>p.Value.Clone(),StringComparer.Ordinal)??new(StringComparer.Ordinal)};

    public static VariableValue FromNumber(double value = 0d) =>
        new() { Type = VariableType.Number, Number = value };

    public static VariableValue FromString(string value = "") =>
        new() { Type = VariableType.String, String = value };

    public static VariableValue FromBoolean(bool value = false) =>
        new() { Type = VariableType.Boolean, Boolean = value };

    public static VariableValue FromVector2(Vector2 value) =>
        new() { Type = VariableType.Vector2, Vector2 = value };

    public static VariableValue FromVector3(Vector3 value) =>
        new() { Type = VariableType.Vector3, Vector3 = value };

    public static VariableValue Default(VariableType type) => type switch
    {
        VariableType.Array => FromArray(),
        VariableType.Structure => FromStructure(),
        VariableType.Null => FromNull(),
        VariableType.String => FromString(),
        VariableType.Boolean => FromBoolean(),
        VariableType.Vector2 => FromVector2(default),
        VariableType.Vector3 => FromVector3(default),
        _ => FromNumber()
    };

    [JsonIgnore]
    public object? BoxedValue => Type switch
    {
        VariableType.Null => null,
        VariableType.Array or VariableType.Structure => this,
        VariableType.Number => Number,
        VariableType.String => String,
        VariableType.Boolean => Boolean,
        VariableType.Vector2 => Vector2,
        VariableType.Vector3 => Vector3,
        _ => Number
    };

    public VariableValue Clone() => new()
    {
        Type = Type,
        Number = Number,
        String = String,
        Boolean = Boolean,
        Vector2 = Vector2,
        Vector3 = Vector3,
        Items = Items.Select(v=>v.Clone()).ToList(),
        Children = Children.ToDictionary(p=>p.Key,p=>p.Value.Clone(),StringComparer.Ordinal)
    };
}
