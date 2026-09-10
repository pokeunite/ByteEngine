namespace ByteEngine.Core.Variables;

public sealed class VariableStore : IEnumerable<KeyValuePair<string, VariableValue>>
{
    private readonly Dictionary<string, VariableValue> _values = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _values.Count;
    public IEnumerable<string> Names => _values.Keys;
    public bool Contains(string name) => _values.ContainsKey(name);
    public bool TryGet(string name, out VariableValue? value) => _values.TryGetValue(name, out value);

    public VariableValue this[string name]
    {
        get => _values[name];
        set => Set(name, value);
    }

    public void Set(string name, VariableValue value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Variable name cannot be empty.", nameof(name));
        ArgumentNullException.ThrowIfNull(value);
        _values[name.Trim()] = value;
    }

    public bool Remove(string name) => _values.Remove(name);

    public bool Rename(string oldName, string newName)
    {
        if (!_values.TryGetValue(oldName, out VariableValue? value) ||
            string.IsNullOrWhiteSpace(newName) ||
            _values.ContainsKey(newName))
            return false;

        _values.Remove(oldName);
        _values[newName.Trim()] = value;
        return true;
    }

    public void Clear() => _values.Clear();

    public VariableStore Clone()
    {
        var result = new VariableStore();
        foreach (KeyValuePair<string, VariableValue> pair in _values)
            result.Set(pair.Key, pair.Value.Clone());
        return result;
    }

    public IEnumerator<KeyValuePair<string, VariableValue>> GetEnumerator() => _values.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
