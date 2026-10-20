using System.Text.Json;
namespace ByteEngine.Core.Variables;

/// <summary>Data JSON, independent of the engine's typed scene serialization.</summary>
public static class VariableJson
{
    public static VariableValue Parse(string json)
    {using var document=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=64});return Read(document.RootElement);}
    private static VariableValue Read(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Array=>VariableValue.FromArray(value.EnumerateArray().Select(Read)),
        JsonValueKind.Object=>VariableValue.FromStructure(value.EnumerateObject().Select(p=>new KeyValuePair<string,VariableValue>(p.Name,Read(p.Value)))),
        JsonValueKind.Number=>VariableValue.FromNumber(value.GetDouble()),
        JsonValueKind.True=>VariableValue.FromBoolean(true),
        JsonValueKind.False=>VariableValue.FromBoolean(false),
        JsonValueKind.Null=>VariableValue.FromNull(),
        _=>VariableValue.FromString(value.ValueKind==JsonValueKind.String?value.GetString()??"":"")
    };
    public static string Write(VariableValue value) => JsonSerializer.Serialize(Data(value));
    private static object? Data(VariableValue v)=>v.Type switch
    {
        VariableType.Array=>v.Items.Select(Data).ToArray(),
        VariableType.Structure=>v.Children.ToDictionary(p=>p.Key,p=>Data(p.Value)),
        VariableType.Vector2=>new[]{v.Vector2.X,v.Vector2.Y},
        VariableType.Vector3=>new[]{v.Vector3.X,v.Vector3.Y,v.Vector3.Z},
        _=>v.BoxedValue
    };
    public static VariableValue FromObject(object? value)=>value switch
    {
        VariableValue v=>v.Clone(), bool b=>VariableValue.FromBoolean(b), string s=>VariableValue.FromString(s),
        System.Numerics.Vector2 v=>VariableValue.FromVector2(v), System.Numerics.Vector3 v=>VariableValue.FromVector3(v),
        null=>VariableValue.FromNull(),_=>VariableValue.FromNumber(Convert.ToDouble(value,System.Globalization.CultureInfo.InvariantCulture))
    };
}
