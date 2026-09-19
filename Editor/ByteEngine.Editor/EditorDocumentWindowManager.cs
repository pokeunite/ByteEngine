using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Editor.Panels;

using ImGuiNET;

using OpenTK.Windowing.Desktop;

namespace ByteEngine.Editor;

internal sealed class EditorDocumentWindowManager : IDisposable
{
    private readonly GameWindow _owner;
    private readonly EditorDocumentManager _documents;
    private readonly Action _restoreMainImGui;
    private readonly EditorDocumentWindowRegistry _registry = new();
    private readonly Dictionary<EditorDocumentId, NativeEditorDocumentWindow> _windows = new();

    public EditorDocumentWindowManager(GameWindow owner, EditorDocumentManager documents, Action restoreMainImGui)
    {
        _owner = owner;
        _documents = documents;
        _restoreMainImGui = restoreMainImGui;
    }

    internal EditorDocumentWindowRegistry Registry => _registry;
    public int Count => _windows.Count;

    public bool TryFocus(EditorDocumentId id)
    {
        if (!_windows.TryGetValue(id, out NativeEditorDocumentWindow? window)) return false;
        window.RestoreAndFocus();
        _documents.Activate(id);
        return true;
    }

    public void OpenAnimation(AssetRecord asset, ImportedAnimation animation, EditorProjectContext project,
        EditorLog log, ByteEngine.Core.Graphics.Renderer2D renderer, ByteEngine.Core.Graphics.ThreeD.Renderer3D renderer3D)
    {
        EditorDocumentId id = new(EditorDocumentType.Animation, $"{asset.Guid:N}:{animation.Key}");
        if (TryFocus(id)) return;
        var panel = new AnimationTimelineWorkspacePanel(_documents);
        panel.Open(asset, animation, project, log);
        Add(id, $"{animation.Name} — Animation — ByteEngine",
            (windowRenderer, windowRenderer3D, width, height) =>
            {
                panel.Draw(log, windowRenderer, windowRenderer3D, width, height);
                ImGui.Begin("Animation Details");
                panel.DrawContextInspector();
                ImGui.End();
            }, panel.Dispose);
    }

    public void OpenProfile(AssetRecord asset, EditorProjectContext project, EditorLog log)
    {
        EditorDocumentId id = new(EditorDocumentType.AnimationProfile, asset.Guid.ToString("N"));
        if (TryFocus(id)) return;
        var panel = new AnimationProfileWorkspacePanel(_documents);
        panel.Open(asset, project, log);
        Add(id, $"{Path.GetFileNameWithoutExtension(asset.ProjectPath)} — Animation Profile — ByteEngine",
            (_, _, _, _) => panel.Draw(log), () => { });
    }

    public void OpenBlueprint(AssetRecord asset, EditorProjectContext project,
        ByteEngine.Core.Graphics.Renderer2D renderer, ByteEngine.Core.Graphics.ThreeD.Renderer3D renderer3D)
    {
        EditorDocumentId id = new(EditorDocumentType.Blueprint, asset.Guid.ToString("N"));
        if (TryFocus(id)) return;
        var panel = new BlueprintWorkspacePanel(_documents);
        panel.Open(asset, project);
        Add(id, $"{Path.GetFileNameWithoutExtension(asset.ProjectPath)} — Blueprint — ByteEngine",
            (windowRenderer, windowRenderer3D, width, height) => panel.Draw(windowRenderer, windowRenderer3D, width, height), panel.Dispose);
    }

    public void OpenEvent(AssetRecord asset, EditorLog log)
    {
        EditorDocumentId id = new(EditorDocumentType.EventSheet, asset.Guid.ToString("N"));
        if (TryFocus(id)) return;
        var panel = new EventWorkspacePanel(_documents);
        panel.Open(asset, log);
        Add(id, $"{Path.GetFileNameWithoutExtension(asset.ProjectPath)} — Event Sheet — ByteEngine",
            (_, _, _, _) => panel.Draw(log), () => { });
    }

    public void PumpAndRender(float deltaTime)
    {
        foreach ((EditorDocumentId id, NativeEditorDocumentWindow window) in _windows.ToArray())
        {
            window.PumpAndRender(deltaTime);
            if (!window.Exists || window.IsExiting)
            {
                window.Dispose();
                _windows.Remove(id);
                _registry.Unregister(id);
            }
        }
        _owner.MakeCurrent();
    }

    public void CloseAllImmediately()
    {
        foreach (NativeEditorDocumentWindow window in _windows.Values.ToArray())
        {
            window.CloseImmediately();
            window.ProcessEvents(0.0);
            window.Dispose();
        }
        _windows.Clear();
        _registry.Clear();
        _owner.MakeCurrent();
        _restoreMainImGui();
    }

    private void Add(EditorDocumentId id, string title, Action<ByteEngine.Core.Graphics.Renderer2D, ByteEngine.Core.Graphics.ThreeD.Renderer3D, int, int> draw, Action dispose)
    {
        if (!_documents.Documents.Any(document => document.Id == id))
        {
            dispose();
            return;
        }
        if (!_registry.Register(id))
        {
            dispose();
            TryFocus(id);
            return;
        }
        _windows.Add(id, new NativeEditorDocumentWindow(
            _owner, id, title, _documents, _registry, draw, dispose));
    }

    public void Dispose() => CloseAllImmediately();
}