namespace ByteEngine.Mcp.Authoring;

internal sealed class HandleTable
{
    private readonly Dictionary<(char Kind, string Identity), string> _forward = new();
    private readonly Dictionary<string, (char Kind, string Identity)> _reverse = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<char, int> _next = new();

    public string Get(char kind, string identity)
    {
        var key = (kind, identity.ToUpperInvariant());
        if (_forward.TryGetValue(key, out string? existing)) return existing;
        int index = _next.GetValueOrDefault(kind) + 1;
        _next[kind] = index;
        string handle = kind + index.ToString();
        _forward[key] = handle;
        _reverse[handle] = key;
        return handle;
    }

    public string Resolve(char kind, string input)
    {
        if (_reverse.TryGetValue(input, out var found))
        {
            if (found.Kind != kind) throw new McpFault("INVALID_HANDLE");
            return found.Identity;
        }
        if (input.Length > 1 && input[0] == kind && char.IsDigit(input[1]))
            throw new McpFault("STALE_HANDLE", "Inspect this resource again.");
        return input;
    }

    public void Clear()
    {
        _forward.Clear();
        _reverse.Clear();
        _next.Clear();
    }
}
