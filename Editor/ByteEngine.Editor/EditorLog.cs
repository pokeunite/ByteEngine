namespace ByteEngine.Editor;

internal sealed record EditorLogEntry(
    DateTime Timestamp,
    EditorLogLevel Level,
    string Message);

internal enum EditorLogLevel
{
    Info,
    Warning,
    Error
}

internal sealed class EditorLog
{
    private readonly List<EditorLogEntry> _entries =
        new();

    public IReadOnlyList<EditorLogEntry> Entries =>
        _entries;

    public void Info(
        string message)
    {
        Add(
            EditorLogLevel.Info,
            message
        );
    }

    public void Warning(
        string message)
    {
        Add(
            EditorLogLevel.Warning,
            message
        );
    }

    public void Error(
        string message)
    {
        Add(
            EditorLogLevel.Error,
            message
        );
    }

    public void Clear()
    {
        _entries.Clear();
    }

    private void Add(
        EditorLogLevel level,
        string message)
    {
        _entries.Add(
            new EditorLogEntry(
                DateTime.Now,
                level,
                message
            )
        );
    }
}
