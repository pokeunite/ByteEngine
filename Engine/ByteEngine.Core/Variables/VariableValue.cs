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
        VariableType.String => FromString(),
        VariableType.Boolean => FromBoolean(),
        VariableType.Vector2 => FromVector2(default),
        VariableType.Vector3 => FromVector3(default),
        _ => FromNumber()
    };

    [JsonIgnore]
    public object BoxedValue => Type switch
    {
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
        Vector3 = Vector3
    };
}
