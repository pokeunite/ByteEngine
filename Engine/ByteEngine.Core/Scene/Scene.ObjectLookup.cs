namespace ByteEngine.Core.Scene;

public sealed partial class Scene
{
    private bool _objectLookupDirty = true;
    private readonly Dictionary<string,GameObject> _firstObjectByName = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid,GameObject> _objectById = new();
    internal void InvalidateObjectLookup() => _objectLookupDirty = true;
    private void RefreshObjectLookup()
    {
        if (!_objectLookupDirty) return;
        _firstObjectByName.Clear(); _objectById.Clear();
        // Preserve first-in-scene semantics for duplicate names, including sibling reordering.
        foreach (var item in _gameObjects)
        {
            if (item.Name != null) _firstObjectByName.TryAdd(item.Name,item);
            _objectById.TryAdd(item.Id,item);
        }
        _objectLookupDirty = false;
    }
}
