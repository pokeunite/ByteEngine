using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.Variables;

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
    /*
     * The editor only has one active state at a time.
     *
     * This gives docked document editors such as EventWorkspacePanel
     * access to the currently active scene/project without forcing the
     * AssetsPanel and every document collection to pass EditorState
     * through several layers.
     */
    public static EditorState? Active { get; private set; }

    public EditorState()
    {
        Active = this;
    }

    public EditorMode Mode { get; set; } =
        EditorMode.Edit;

    public required Scene EditorScene { get; set; }

    public required ProjectData Project { get; init; }

    public required string ProjectFilePath { get; init; }

    public string? SceneFilePath { get; set; }

    public bool IsDirty { get; private set; }

    public Scene? RuntimeScene { get; set; }

    public VariableStore? RuntimeGlobals { get; set; }

    public Scene DisplayedScene =>
        RuntimeScene ??
        EditorScene;

    public EditorSelection Selection { get; } =
        new();

    public GameObject? SelectedObject
    {
        get => Selection.Primary;
        set => Selection.Set(value);
    }

    public UndoManager? Undo { get; set; }

    public Guid? SelectedAssetId { get; set; }

    public string? SelectedAssetPath { get; set; }

    /// <summary>
    /// Virtual animation sub-asset selected beneath a Model3D asset.
    /// The model remains the real AssetDatabase record; this stores only the
    /// imported clip key so no .byteanimation extraction is required.
    /// </summary>
    public string? SelectedModelAnimationKey { get; set; }

    public EditorCamera Camera { get; } =
        new();

    public EditorCamera3D Camera3D { get; } =
        new();

    public void MarkDirty()
    {
        if (Mode ==
            EditorMode.Edit)
        {
            IsDirty = true;
        }
    }

    public void ClearDirty()
    {
        IsDirty = false;
    }

    internal void SetDirty(
        bool value)
    {
        IsDirty = value;
    }
}