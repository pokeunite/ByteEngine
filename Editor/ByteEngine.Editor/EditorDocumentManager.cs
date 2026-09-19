namespace ByteEngine.Editor;

internal sealed class EditorDocumentManager
{
    private readonly Dictionary<EditorDocumentId, EditorDocument> _documents = new();

    public EditorDocument? ActiveDocument { get; private set; }
    public EditorDocumentType? ActiveDocumentType => ActiveDocument?.Id.Type;
    public IReadOnlyCollection<EditorDocument> Documents => _documents.Values;
    public bool HasDirtyDocuments => _documents.Values.Any(document => document.IsDirty);
    public event Action<EditorDocument?>? ActiveDocumentChanged;

    public bool RegisterOrFocus(EditorDocument document)
    {
        if (_documents.TryGetValue(document.Id, out EditorDocument? existing))
        {
            existing.UpdateTitle(document.Title);
            Activate(existing.Id);
            existing.Focus();
            return false;
        }

        _documents.Add(document.Id, document);
        Activate(document.Id);
        return true;
    }

    public bool Activate(EditorDocumentId id)
    {
        if (!_documents.TryGetValue(id, out EditorDocument? document)) return false;
        if (ReferenceEquals(ActiveDocument, document)) return true;
        ActiveDocument = document;
        ActiveDocumentChanged?.Invoke(document);
        return true;
    }

    public bool Unregister(EditorDocumentId id)
    {
        if (!_documents.Remove(id, out EditorDocument? removed)) return false;
        if (ReferenceEquals(ActiveDocument, removed))
        {
            ActiveDocument = _documents.Values.LastOrDefault();
            ActiveDocumentChanged?.Invoke(ActiveDocument);
        }
        return true;
    }

    public bool SaveActive() => ActiveDocument?.Save() == true;
    public bool SaveAllDirty()
    {
        foreach (EditorDocument document in _documents.Values.Where(item => item.IsDirty).ToArray())
        {
            if (!document.Save()) return false;
        }
        return true;
    }

    public void DiscardAllDirty()
    {
        foreach (EditorDocument document in _documents.Values.Where(item => item.IsDirty).ToArray())
            document.Discard();
    }

    public void RequestClose(EditorDocumentId id)
    {
        if (_documents.TryGetValue(id, out EditorDocument? document)) document.RequestClose();
    }

    public void RequestCloseOthers(EditorDocumentId keep)
    {
        foreach (EditorDocument document in _documents.Values.Where(item => item.Id != keep).ToArray())
            document.RequestClose();
    }

    public void RequestCloseAll()
    {
        foreach (EditorDocument document in _documents.Values.ToArray())
            document.RequestClose();
    }

    public void Clear()
    {
        _documents.Clear();
        ActiveDocument = null;
        ActiveDocumentChanged?.Invoke(null);
    }
}