using System.Text.Json;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;

namespace ByteEngine.Editor.Commands;

internal sealed class UndoManager
{
    private sealed record PendingEdit(string Name, SceneData Scene, List<VariableData> Globals, string Json, Guid[] Selection);

    private readonly SceneSerializer _serializer;
    private readonly Action<Scene> _activateScene;
    private readonly Stack<IEditorHistoryEntry> _undo = new();
    private readonly Stack<IEditorHistoryEntry> _redo = new();
    private Dictionary<Guid, TransformValue>? _pendingTransforms;
    private string _transformName = "Transform Objects";
    public int MaximumHistoryEntries { get; set; } = 128;
    private PendingEdit? _pending;
    private int _currentRevision;
    private int _savedRevision;
    private int _nextRevision;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string? UndoName => _undo.TryPeek(out var command) ? command.Name : null;
    public string? RedoName => _redo.TryPeek(out var command) ? command.Name : null;

    public UndoManager(SceneSerializer serializer, Action<Scene> activateScene)
    {
        _serializer = serializer;
        _activateScene = activateScene;
    }

    public void Reset(EditorState state, bool isSaved)
    {
        _undo.Clear();
        _redo.Clear();
        _pending = null;
        _pendingTransforms = null;
        _currentRevision = 0;
        _nextRevision = 0;
        _savedRevision = isSaved ? 0 : -1;
        UpdateDirty(state);
    }

    public void MarkSaved(EditorState state)
    {
        _savedRevision = _currentRevision;
        UpdateDirty(state);
    }

    public void Execute(EditorState state, string name, Action action)
    {
        if (state.Mode != EditorMode.Edit) return;
        BeginGesture(state, name);
        try
        {
            action();
            CommitGesture(state);
        }
        catch
        {
            CancelGesture();
            throw;
        }
    }

    public void BeginGesture(EditorState state, string name)
    {
        if (_pending != null || _pendingTransforms != null || state.Mode != EditorMode.Edit) return;
        SceneData scene = _serializer.Serialize(state.EditorScene);
        List<VariableData> globals = CloneGlobals(state.Project.GlobalVariables);
        _pending = new PendingEdit(name, scene, globals, JsonSerializer.Serialize(new { Scene=scene, Globals=globals }, JsonSerialization.Options), SelectionIds(state));
    }

    public void CommitGesture(EditorState state)
    {
        if (_pendingTransforms != null)
        {
            var afterTransforms = CaptureTransforms(state, _pendingTransforms.Keys);
            if (_pendingTransforms.Count == afterTransforms.Count &&
                _pendingTransforms.Any(pair => afterTransforms[pair.Key] != pair.Value))
            {
                int next = ++_nextRevision;
                _undo.Push(new TransformEditorCommand(_transformName, _pendingTransforms, afterTransforms, _currentRevision, next));
                _redo.Clear(); _currentRevision = next;
                TrimHistory();
            }
            _pendingTransforms = null;
            UpdateDirty(state);
            return;
        }
        if (_pending == null) return;
        SceneData after = _serializer.Serialize(state.EditorScene);
        List<VariableData> afterGlobals = CloneGlobals(state.Project.GlobalVariables);
        string afterJson = JsonSerializer.Serialize(new { Scene=after, Globals=afterGlobals }, JsonSerialization.Options);
        if (_pending.Json != afterJson)
        {
            int next = ++_nextRevision;
            _undo.Push(new SnapshotEditorCommand(
                _pending.Name, _pending.Scene, after, _pending.Globals, afterGlobals, _pending.Selection, SelectionIds(state), _currentRevision, next));
            _redo.Clear();
            _currentRevision = next;
            TrimHistory();
        }
        _pending = null;
        UpdateDirty(state);
    }

    public void CancelGesture() { _pending = null; _pendingTransforms = null; }

    public void BeginTransformGesture(EditorState state, string name)
    {
        if (_pending != null || _pendingTransforms != null || state.Mode != EditorMode.Edit) return;
        _transformName = name;
        _pendingTransforms = CaptureTransforms(state, SelectionIds(state));
    }

    private static Dictionary<Guid, TransformValue> CaptureTransforms(EditorState state, IEnumerable<Guid> ids)
    {
        var values = new Dictionary<Guid, TransformValue>();
        foreach (Guid id in ids)
            if (state.EditorScene.FindGameObject(id) is { } gameObject)
                values[id] = new(gameObject.Transform.LocalPosition, gameObject.Transform.LocalRotation, gameObject.Transform.LocalScale);
        return values;
    }

    private void TrimHistory()
    {
        int maximum = Math.Clamp(MaximumHistoryEntries, 1, 4096);
        if (_undo.Count <= maximum) return;
        var retained = _undo.Take(maximum).Reverse().ToArray();
        _undo.Clear();
        foreach (var entry in retained) _undo.Push(entry);
    }

    private void RestoreEntry(EditorState state, IEditorHistoryEntry entry, bool after)
    {
        if (entry is TransformEditorCommand transform)
        {
            foreach (var (id, value) in after ? transform.After : transform.Before)
                if (state.EditorScene.FindGameObject(id) is { } gameObject)
                {
                    gameObject.Transform.LocalPosition = value.Position;
                    gameObject.Transform.LocalRotation = value.Rotation;
                    gameObject.Transform.LocalScale = value.Scale;
                }
        }
        else if (entry is IEditorCommand snapshot)
            Restore(state, after ? snapshot.After : snapshot.Before,
                after ? snapshot.AfterGlobals : snapshot.BeforeGlobals,
                after ? snapshot.AfterSelection : snapshot.BeforeSelection);
    }

    public void Undo(EditorState state)
    {
        if (state.Mode != EditorMode.Edit || !_undo.TryPop(out var command)) return;
        RestoreEntry(state, command, after: false);
        _currentRevision = command.BeforeRevision;
        _redo.Push(command);
        UpdateDirty(state);
    }

    public void Redo(EditorState state)
    {
        if (state.Mode != EditorMode.Edit || !_redo.TryPop(out var command)) return;
        RestoreEntry(state, command, after: true);
        _currentRevision = command.AfterRevision;
        _undo.Push(command);
        UpdateDirty(state);
    }

    private void Restore(EditorState state, SceneData data, List<VariableData> globals, Guid[] selection)
    {
        Scene scene = _serializer.Deserialize(data);
        state.EditorScene = scene;
        state.RuntimeScene = null;
        state.Project.GlobalVariables = CloneGlobals(globals);
        state.Selection.Set(selection.Select(scene.FindGameObject).Where(item => item != null).Cast<GameObject>());
        _activateScene(scene);
    }

    private void UpdateDirty(EditorState state) => state.SetDirty(_currentRevision != _savedRevision);
    private static Guid[] SelectionIds(EditorState state) => state.Selection.Objects.Select(item => item.Id).ToArray();
    private static List<VariableData> CloneGlobals(List<VariableData> values) =>
        JsonSerializer.Deserialize<List<VariableData>>(JsonSerializer.Serialize(values, JsonSerialization.Options), JsonSerialization.Options) ?? new();
}
