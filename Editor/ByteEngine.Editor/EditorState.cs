using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Editor.Commands;
using ByteEngine.Editor.Selection;

namespace ByteEngine.Editor;

internal enum EditorMode
{
    Edit,
    Play,
    Paused
}

internal sealed class EditorState
{
    public EditorMode Mode { get; set; } =
        EditorMode.Edit;

    public required Scene EditorScene { get; set; }

    public required ProjectData Project { get; init; }

    public required string ProjectFilePath { get; init; }

    public string? SceneFilePath { get; set; }

    public bool IsDirty { get; private set; }

    public Scene? RuntimeScene { get; set; }

    public Scene DisplayedScene =>
        RuntimeScene ??
        EditorScene;

    public EditorSelection Selection { get; } = new();

    public GameObject? SelectedObject
    {
        get => Selection.Primary;
        set => Selection.Set(value);
    }

    public UndoManager? Undo { get; set; }

    public Guid? SelectedAssetId { get; set; }

    public string? SelectedAssetPath { get; set; }

    public EditorCamera Camera { get; } =
        new();

    public void MarkDirty()
    {
        if (Mode ==
            EditorMode.Edit)
        {
            IsDirty =
                true;
        }
    }

    public void ClearDirty()
    {
        IsDirty =
            false;
    }

    internal void SetDirty(bool value) => IsDirty = value;
}
