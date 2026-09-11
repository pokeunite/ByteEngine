using System.Text.Json;
using System.Text.Json.Serialization;

using ByteEngine.Core.VisualLogic;

namespace ByteEngine.Editor.Panels;

internal sealed class EventModuleHistory
{
    private sealed record Snapshot(
        string Name,
        string Json);

    private readonly Stack<Snapshot> _undo =
        new();

    private readonly Stack<Snapshot> _redo =
        new();

    private readonly JsonSerializerOptions _options =
        CreateOptions();

    private string _savedJson =
        string.Empty;

    public bool CanUndo =>
        _undo.Count >
        0;

    public bool CanRedo =>
        _redo.Count >
        0;

    public string? UndoName =>
        _undo.TryPeek(
            out Snapshot? snapshot)
            ? snapshot.Name
            : null;

    public string? RedoName =>
        _redo.TryPeek(
            out Snapshot? snapshot)
            ? snapshot.Name
            : null;

    public void Reset(
        EventModuleDefinition module)
    {
        ArgumentNullException.ThrowIfNull(
            module);

        _undo.Clear();
        _redo.Clear();

        _savedJson =
            Serialize(
                module);
    }

    public void MarkSaved(
        EventModuleDefinition module)
    {
        ArgumentNullException.ThrowIfNull(
            module);

        _savedJson =
            Serialize(
                module);
    }

    public bool IsDirty(
        EventModuleDefinition module)
    {
        ArgumentNullException.ThrowIfNull(
            module);

        return !string.Equals(
            Serialize(
                module),
            _savedJson,
            StringComparison.Ordinal);
    }

    public void Record(
        string name,
        EventModuleDefinition module)
    {
        ArgumentNullException.ThrowIfNull(
            module);

        string json =
            Serialize(
                module);

        if (_undo.TryPeek(
                out Snapshot? previous) &&
            string.Equals(
                previous.Json,
                json,
                StringComparison.Ordinal))
        {
            return;
        }

        _undo.Push(
            new Snapshot(
                name,
                json));

        _redo.Clear();
    }

    public bool TryUndo(
        EventModuleDefinition current,
        out EventModuleDefinition restored)
    {
        ArgumentNullException.ThrowIfNull(
            current);

        restored =
            current;

        if (!_undo.TryPop(
                out Snapshot? snapshot))
        {
            return false;
        }

        _redo.Push(
            new Snapshot(
                snapshot.Name,
                Serialize(
                    current)));

        restored =
            Deserialize(
                snapshot.Json);

        return true;
    }

    public bool TryRedo(
        EventModuleDefinition current,
        out EventModuleDefinition restored)
    {
        ArgumentNullException.ThrowIfNull(
            current);

        restored =
            current;

        if (!_redo.TryPop(
                out Snapshot? snapshot))
        {
            return false;
        }

        _undo.Push(
            new Snapshot(
                snapshot.Name,
                Serialize(
                    current)));

        restored =
            Deserialize(
                snapshot.Json);

        return true;
    }

    private string Serialize(
        EventModuleDefinition module)
    {
        return JsonSerializer.Serialize(
            module,
            _options);
    }

    private EventModuleDefinition Deserialize(
        string json)
    {
        return JsonSerializer.Deserialize<EventModuleDefinition>(
                   json,
                   _options)
               ?? throw new InvalidDataException(
                   "Could not restore Event Module undo snapshot.");
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options =
            new JsonSerializerOptions
            {
                PropertyNamingPolicy =
                    JsonNamingPolicy.CamelCase,

                PropertyNameCaseInsensitive =
                    true,

                WriteIndented =
                    false,

                IncludeFields =
                    true
            };

        options.Converters.Add(
            new JsonStringEnumConverter());

        return options;
    }
}
