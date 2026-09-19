namespace ByteEngine.Editor;

internal enum NativeDocumentWindowState
{
    Normal,
    Maximized,
    Minimized,
    Closing
}

internal sealed class EditorDocumentWindowRegistry
{
    private readonly Dictionary<EditorDocumentId, NativeDocumentWindowState> _states = new();
    private readonly Dictionary<EditorDocumentId, NativeDocumentWindowState> _restoreStates = new();

    public int Count => _states.Count;
    public IReadOnlyCollection<EditorDocumentId> Documents => _states.Keys;

    public bool Register(EditorDocumentId id, NativeDocumentWindowState initial = NativeDocumentWindowState.Maximized)
    {
        if (_states.ContainsKey(id)) return false;
        _states.Add(id, initial);
        _restoreStates[id] = initial == NativeDocumentWindowState.Minimized
            ? NativeDocumentWindowState.Maximized
            : initial;
        return true;
    }

    public bool Contains(EditorDocumentId id) => _states.ContainsKey(id);

    public NativeDocumentWindowState? GetState(EditorDocumentId id) =>
        _states.TryGetValue(id, out NativeDocumentWindowState state) ? state : null;

    public void Record(EditorDocumentId id, NativeDocumentWindowState state)
    {
        if (!_states.ContainsKey(id)) return;
        if (state != NativeDocumentWindowState.Minimized && state != NativeDocumentWindowState.Closing)
            _restoreStates[id] = state;
        _states[id] = state;
    }

    public NativeDocumentWindowState Restore(EditorDocumentId id)
    {
        NativeDocumentWindowState state = _restoreStates.TryGetValue(id, out NativeDocumentWindowState saved)
            ? saved
            : NativeDocumentWindowState.Maximized;
        if (_states.ContainsKey(id)) _states[id] = state;
        return state;
    }

    public bool Unregister(EditorDocumentId id)
    {
        _restoreStates.Remove(id);
        return _states.Remove(id);
    }

    public void Clear()
    {
        _states.Clear();
        _restoreStates.Clear();
    }
}