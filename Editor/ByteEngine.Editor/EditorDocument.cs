namespace ByteEngine.Editor;

internal enum EditorDocumentType
{
    Scene,
    Animation,
    Blueprint,
    EventSheet,
    AnimationProfile
}

internal readonly record struct EditorDocumentId(EditorDocumentType Type, string Key)
{
    public override string ToString() => $"{Type}:{Key}";
}

internal sealed class EditorDocument
{
    private readonly Func<bool>? _save;
    private readonly Action? _discard;
    private readonly Action _focus;
    private readonly Action _requestClose;
    private readonly Func<bool> _isDirty;

    public EditorDocumentId Id { get; }
    public string Title { get; private set; }
    public bool IsDirty => _isDirty();

    public EditorDocument(
        EditorDocumentId id,
        string title,
        Action focus,
        Action requestClose,
        Func<bool>? save = null,
        Action? discard = null,
        Func<bool>? isDirty = null)
    {
        Id = id;
        Title = title;
        _focus = focus;
        _requestClose = requestClose;
        _save = save;
        _discard = discard;
        _isDirty = isDirty ?? (() => false);
    }

    public void UpdateTitle(string title) => Title = title;
    public void Focus() => _focus();
    public bool Save() => _save?.Invoke() ?? false;
    public void Discard() => _discard?.Invoke();
    public void RequestClose() => _requestClose();
}