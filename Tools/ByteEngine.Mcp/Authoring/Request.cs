using System.Text.Json;

namespace ByteEngine.Mcp.Authoring;

internal sealed class McpFault(string code, string? hint = null) : Exception(code)
{
    public string Code { get; } = code;
    public string? Hint { get; } = hint;
}

internal static class Request
{
    public static JsonElement Get(JsonElement? value, string name)
    {
        if (value is { ValueKind: JsonValueKind.Object } obj &&
            obj.TryGetProperty(name, out JsonElement result)) return result;
        return default;
    }
    public static string? String(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    public static string? String(JsonElement? value, string name) => String(Get(value, name));
    public static string Required(JsonElement? value, string name) =>
        String(value, name) is { Length: > 0 } text ? text :
        throw new McpFault("INVALID_REQUEST", $"Provide {name}.");
    public static bool Bool(JsonElement? value, string name, bool fallback = false) =>
        Get(value, name) is { ValueKind: JsonValueKind.True } ? true :
        Get(value, name) is { ValueKind: JsonValueKind.False } ? false : fallback;
    public static int Int(JsonElement? value, string name, int fallback = 0) =>
        Get(value, name) is { ValueKind: JsonValueKind.Number } number &&
        number.TryGetInt32(out int parsed) ? parsed : fallback;
    public static float Float(JsonElement? value, string name, float fallback = 0) =>
        Get(value, name) is { ValueKind: JsonValueKind.Number } number &&
        number.TryGetSingle(out float parsed) && float.IsFinite(parsed) ? parsed : fallback;
    public static IEnumerable<JsonElement> Items(JsonElement value) =>
        value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : [];
    public static Dictionary<string, JsonElement> Properties(JsonElement value) =>
        value.ValueKind == JsonValueKind.Object
            ? value.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase)
            : new(StringComparer.OrdinalIgnoreCase);
}
