using System.Text.Json;

namespace ByteEngine.Editor.Assistant;

internal sealed class AssistantJournal
{
    private readonly List<object> _entries = [];
    internal void Record(string kind, object data) => _entries.Add(new { time = DateTimeOffset.Now, kind, data });
    internal string Export(object? visibleChat = null) => JsonSerializer.Serialize(new
    {
        format = "ByteEngine Assistant session log", version = 1,
        exportedAt = DateTimeOffset.Now,
        note = "Tool arguments and results describe attempted and applied edits. Success does not imply gameplay testing. No authentication tokens or hidden model reasoning are recorded.",
        visibleChat, entries = _entries
    }, new JsonSerializerOptions { WriteIndented = true });
}
