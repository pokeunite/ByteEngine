using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Editor.Gizmos;
using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class SceneViewPanel : IDisposable
{
    private readonly SceneFramebuffer _framebuffer = new();
    private readonly GizmoController _gizmo = new();
    private readonly Gizmo3DController _gizmo3D = new();
    private bool _is3D;
    private Vector2 _lastViewportSize = new(800f, 500f);
    private Vector2 _contextWorld;
    public bool IsOpen { get; set; } = true;

    public void FrameSelected(EditorState state)
    {
        if (state.SelectedObject != null)
        {
            if (_is3D) state.Camera3D.Frame(state.SelectedObject);
            else state.Camera.Frame(state.SelectedObject, _lastViewportSize);
        }
    }

    public void Draw(
        EditorState state,
        Renderer2D renderer,
        Renderer3D renderer3D,
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

        _framebuffer.Render(renderer, renderer3D, state.DisplayedScene, state.Mode, state.Camera, state.Camera3D, _is3D,
            (int)viewportSize.X, (int)viewportSize.Y, windowWidth, windowHeight);
        ImGui.Image(_framebuffer.TextureId, viewportSize, new Vector2(0f, 1f), new Vector2(1f, 0f));
        Vector2 minimum = ImGui.GetItemRectMin();
        bool hovered = ImGui.IsItemHovered();

        if (ImGui.BeginDragDropTarget())
        {
            Guid? guid = AssetDragDrop.Accept();
            if (!_is3D && guid.HasValue && project.AssetDatabase.TryGetAsset(guid.Value, out AssetRecord? asset) && asset?.Type == AssetType.Texture2D)
                createSpriteFromAsset(asset, GizmoController.ScreenToWorld(state.Camera, ImGui.GetMousePos(), minimum, viewportSize));
            ImGui.EndDragDropTarget();
        }

        if (_is3D)
        {
            HandleCamera3DInput(state, hovered);
            _gizmo3D.UpdateAndDraw(state, state.Camera3D, hovered, minimum, viewportSize);
        }
        else
        {
            HandleCameraInput(state, hovered);
            _gizmo.Update(state, hovered, minimum, viewportSize);
            _gizmo.Draw(state, minimum, viewportSize);
            DrawCameraViewport(state, minimum, viewportSize);
        }

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
        if (ImGui.SmallButton(_is3D ? "2D" : "[2D]")) _is3D = false;
        ImGui.SameLine();
        if (ImGui.SmallButton(_is3D ? "[3D]" : "3D")) _is3D = true;
        ImGui.SameLine();
        if (!_is3D) _gizmo.DrawToolbar(state);
        else ImGui.TextDisabled("XYZ Move");
        ImGui.SameLine();
        if (ImGui.SmallButton("Frame")) FrameSelected(state);
        ImGui.SameLine();
        if (ImGui.SmallButton("Reset Camera")) { if (_is3D) state.Camera3D.Reset(); else state.Camera.Reset(); }
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

    private static void HandleCamera3DInput(EditorState state,bool hovered)
    {
        if(!hovered)return;ImGuiIOPtr io=ImGui.GetIO();EditorCamera3D camera=state.Camera3D;
        if(ImGui.IsMouseDragging(ImGuiMouseButton.Right)){camera.Yaw+=io.MouseDelta.X*.18f;camera.Pitch=Math.Clamp(camera.Pitch-io.MouseDelta.Y*.18f,-89f,89f);}
        float speed=(io.KeyShift?12f:5f)*io.DeltaTime;
        if(ImGui.IsMouseDown(ImGuiMouseButton.Right)){if(ImGui.IsKeyDown(ImGuiKey.W))camera.Position+=camera.Forward*speed;if(ImGui.IsKeyDown(ImGuiKey.S))camera.Position-=camera.Forward*speed;if(ImGui.IsKeyDown(ImGuiKey.D))camera.Position+=camera.Right*speed;if(ImGui.IsKeyDown(ImGuiKey.A))camera.Position-=camera.Right*speed;if(ImGui.IsKeyDown(ImGuiKey.E))camera.Position+=Vector3.UnitY*speed;if(ImGui.IsKeyDown(ImGuiKey.Q))camera.Position-=Vector3.UnitY*speed;}
        if(ImGui.IsMouseDragging(ImGuiMouseButton.Middle))camera.Position+=(-camera.Right*io.MouseDelta.X+Vector3.UnitY*io.MouseDelta.Y)*speed*.12f;
        if(io.MouseWheel!=0)camera.Position+=camera.Forward*io.MouseWheel*Math.Max(Vector3.Distance(camera.Position,state.SelectedObject?.Transform.WorldPosition??Vector3.Zero)*.12f,.35f);
        if(ImGui.IsKeyPressed(ImGuiKey.F)&&state.SelectedObject!=null)camera.Frame(state.SelectedObject);
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
