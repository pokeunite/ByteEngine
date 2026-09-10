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
    private readonly Stack<IEditorCommand> _undo = new();
    private readonly Stack<IEditorCommand> _redo = new();
    private PendingEdit? _pending;
    private int _currentRevision;
    private int _savedRevision;
    private int _nextRevision;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string? UndoName => _undo.TryPeek(out IEditorCommand? command) ? command.Name : null;
    public string? RedoName => _redo.TryPeek(out IEditorCommand? command) ? command.Name : null;

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
        if (_pending != null || state.Mode != EditorMode.Edit) return;
        SceneData scene = _serializer.Serialize(state.EditorScene);
        List<VariableData> globals = CloneGlobals(state.Project.GlobalVariables);
        _pending = new PendingEdit(name, scene, globals, JsonSerializer.Serialize(new { Scene=scene, Globals=globals }, JsonSerialization.Options), SelectionIds(state));
    }

    public void CommitGesture(EditorState state)
    {
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
        }
        _pending = null;
        UpdateDirty(state);
    }

    public void CancelGesture() => _pending = null;

    public void Undo(EditorState state)
    {
        if (state.Mode != EditorMode.Edit || !_undo.TryPop(out IEditorCommand? command)) return;
        Restore(state, command.Before, command.BeforeGlobals, command.BeforeSelection);
        _currentRevision = command.BeforeRevision;
        _redo.Push(command);
        UpdateDirty(state);
    }

    public void Redo(EditorState state)
    {
        if (state.Mode != EditorMode.Edit || !_redo.TryPop(out IEditorCommand? command)) return;
        Restore(state, command.After, command.AfterGlobals, command.AfterSelection);
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
