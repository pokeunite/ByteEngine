using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using ByteEngine.Editor.Gizmos;
using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class SceneViewPanel : IDisposable
{
    private readonly SceneFramebuffer _framebuffer = new();
    private readonly GizmoController _gizmo = new();
    private Vector2 _lastViewportSize = new(800f, 500f);
    private Vector2 _contextWorld;
    public bool IsOpen { get; set; } = true;

    public void FrameSelected(EditorState state)
    {
        if (state.SelectedObject != null) state.Camera.Frame(state.SelectedObject, _lastViewportSize);
    }

    public void Draw(
        EditorState state,
        Renderer2D renderer,
        int windowWidth,
        int windowHeight,
        EditorProjectContext project,
        Action<AssetRecord, Vector2> createSpriteFromAsset,
        Action<Vector2> createEmpty,
        Action<Vector2> createSprite,
        Action<Vector2> createCamera,
        Action paste)
    {
        bool isOpen = IsOpen;
        ImGui.Begin("Scene View", ref isOpen, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        IsOpen = isOpen;
        DrawToolbar(state);

        Vector2 viewportSize = ImGui.GetContentRegionAvail();
        viewportSize.X = Math.Max(viewportSize.X, 1f);
        viewportSize.Y = Math.Max(viewportSize.Y, 1f);
        _lastViewportSize = viewportSize;

        _framebuffer.Render(renderer, state.DisplayedScene, state.Mode, state.Camera,
            (int)viewportSize.X, (int)viewportSize.Y, windowWidth, windowHeight);
        ImGui.Image(_framebuffer.TextureId, viewportSize, new Vector2(0f, 1f), new Vector2(1f, 0f));
        Vector2 minimum = ImGui.GetItemRectMin();
        bool hovered = ImGui.IsItemHovered();

        if (ImGui.BeginDragDropTarget())
        {
            Guid? guid = AssetDragDrop.Accept();
            if (guid.HasValue && project.AssetDatabase.TryGetAsset(guid.Value, out AssetRecord? asset) && asset?.Type == AssetType.Texture2D)
                createSpriteFromAsset(asset, GizmoController.ScreenToWorld(state.Camera, ImGui.GetMousePos(), minimum, viewportSize));
            ImGui.EndDragDropTarget();
        }

        HandleCameraInput(state, hovered);
        _gizmo.Update(state, hovered, minimum, viewportSize);
        _gizmo.Draw(state, minimum, viewportSize);
        DrawCameraViewport(state, minimum, viewportSize);

        if (hovered && ImGui.IsMouseReleased(ImGuiMouseButton.Right))
        {
            _contextWorld = GizmoController.ScreenToWorld(state.Camera, ImGui.GetMousePos(), minimum, viewportSize);
            ImGui.OpenPopup("Scene View Context");
        }
        if (ImGui.BeginPopup("Scene View Context"))
        {
            bool editable = state.Mode == EditorMode.Edit;
            if (ImGui.MenuItem("Create Empty", string.Empty, false, editable)) createEmpty(_contextWorld);
            if (ImGui.MenuItem("Create Sprite", string.Empty, false, editable)) createSprite(_contextWorld);
            if (ImGui.MenuItem("Create Camera", string.Empty, false, editable)) createCamera(_contextWorld);
            ImGui.Separator();
            if (ImGui.MenuItem("Paste", "Ctrl+V", false, editable)) paste();
            ImGui.EndPopup();
        }

        ImGui.End();
    }

    private void DrawToolbar(EditorState state)
    {
        _gizmo.DrawToolbar(state);
        ImGui.SameLine();
        if (ImGui.SmallButton("Frame")) FrameSelected(state);
        ImGui.SameLine();
        if (ImGui.SmallButton("Reset Camera")) state.Camera.Reset();
        ImGui.SameLine();
        ImGui.TextColored(state.Mode switch
        {
            EditorMode.Play => new Vector4(.35f, 1f, .45f, 1f),
            EditorMode.Paused => new Vector4(1f, .75f, .2f, 1f),
            _ => new Vector4(.4f, .8f, 1f, 1f)
        }, state.Mode.ToString().ToUpperInvariant());
    }

    private static void HandleCameraInput(EditorState state, bool hovered)
    {
        if (!hovered) return;
        ImGuiIOPtr io = ImGui.GetIO();
        if (ImGui.IsMouseDragging(ImGuiMouseButton.Middle)) state.Camera.Position -= io.MouseDelta / state.Camera.Zoom;
        if (io.MouseWheel != 0f) state.Camera.Zoom = Math.Clamp(state.Camera.Zoom + io.MouseWheel * .1f, .2f, 4f);
    }

    private static void DrawCameraViewport(EditorState state, Vector2 minimum, Vector2 viewportSize)
    {
        GameObject? selected = state.SelectedObject;
        Camera2D? camera = selected?.GetComponent<Camera2D>();
        if (selected == null || camera == null) return;
        Vector2 half = new(state.Project.Window.Width / Math.Max(camera.Zoom, .01f) / 2f,
            state.Project.Window.Height / Math.Max(camera.Zoom, .01f) / 2f);
        Vector2 min = GizmoController.WorldToScreen(state.Camera, selected.Transform.Position - half, minimum, viewportSize);
        Vector2 max = GizmoController.WorldToScreen(state.Camera, selected.Transform.Position + half, minimum, viewportSize);
        ImGui.GetWindowDrawList().AddRect(min, max, ImGui.GetColorU32(new Vector4(.25f, .75f, 1f, 1f)), 0f, ImDrawFlags.None, 2f);
    }

    public void Dispose() => _framebuffer.Dispose();
}
