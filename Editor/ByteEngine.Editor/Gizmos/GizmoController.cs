using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using ImGuiNET;

namespace ByteEngine.Editor.Gizmos;

internal enum GizmoMode { Select, Move, Rotate, Scale }
internal enum GizmoHandle { None, Free, X, Y, Uniform, Rotate }

internal sealed class GizmoController
{
    private readonly Dictionary<Guid, Vector2> _startPositions = new();
    private readonly Dictionary<Guid, Vector2> _startSizes = new();
    private readonly Dictionary<Guid, float> _startRotations = new();
    private GizmoHandle _activeHandle;
    private Vector2 _startMouseWorld;
    private Vector2 _pivotWorld;

    public GizmoMode Mode { get; private set; } = GizmoMode.Select;
    public bool SnapEnabled { get; private set; }
    public int SnapSize { get; private set; } = 16;
    public GameObject? HoveredObject { get; private set; }

    public void DrawToolbar(EditorState state)
    {
        DrawModeButton("Q Select", GizmoMode.Select);
        ImGui.SameLine(); DrawModeButton("W Move", GizmoMode.Move);
        ImGui.SameLine(); DrawModeButton("E Rotate", GizmoMode.Rotate);
        ImGui.SameLine(); DrawModeButton("R Scale", GizmoMode.Scale);
        ImGui.SameLine();
        bool snap = SnapEnabled;
        if (ImGui.Checkbox("Snap", ref snap)) SnapEnabled = snap;
        ImGui.SameLine(); ImGui.SetNextItemWidth(65f);
        if (ImGui.BeginCombo("##SnapSize", SnapSize.ToString()))
        {
            foreach (int value in new[] { 1, 8, 16, 32, 64 })
                if (ImGui.Selectable(value.ToString(), SnapSize == value)) SnapSize = value;
            ImGui.EndCombo();
        }

        if (state.Mode == EditorMode.Edit && !ImGui.GetIO().WantTextInput)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Q)) Mode = GizmoMode.Select;
            if (ImGui.IsKeyPressed(ImGuiKey.W)) Mode = GizmoMode.Move;
            if (ImGui.IsKeyPressed(ImGuiKey.E)) Mode = GizmoMode.Rotate;
            if (ImGui.IsKeyPressed(ImGuiKey.R)) Mode = GizmoMode.Scale;
        }
    }

    public void Update(EditorState state, bool hovered, Vector2 minimum, Vector2 viewportSize)
    {
        Vector2 mouse = ImGui.GetMousePos();
        Vector2 mouseWorld = ScreenToWorld(state.Camera, mouse, minimum, viewportSize);
        HoveredObject = hovered ? HitTest(state.DisplayedScene, mouseWorld) : null;
        if (state.Mode != EditorMode.Edit || (!hovered && _activeHandle == GizmoHandle.None)) return;

        if (_activeHandle != GizmoHandle.None)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left)) ApplyDrag(state, mouseWorld);
            else { _activeHandle = GizmoHandle.None; state.Undo?.CommitGesture(state); }
            return;
        }

        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left)) return;
        GizmoHandle handle = HitGizmo(state, mouse, minimum, viewportSize);
        if (handle != GizmoHandle.None) { BeginDrag(state, handle, mouseWorld); return; }

        GameObject? hit = HoveredObject;
        bool control = ImGui.GetIO().KeyCtrl;
        if (control && hit != null) state.Selection.Toggle(hit);
        else if (hit == null) state.Selection.Clear();
        else if (!state.Selection.Contains(hit)) state.Selection.Set(hit);
        if (hit != null)
        {
            state.SelectedAssetId = null;
            state.SelectedAssetPath = null;
            if (!control && (Mode is GizmoMode.Select or GizmoMode.Move)) BeginDrag(state, GizmoHandle.Free, mouseWorld);
        }
    }

    public void Draw(EditorState state, Vector2 minimum, Vector2 viewportSize)
    {
        if (HoveredObject != null && !state.Selection.Contains(HoveredObject))
            DrawObjectOutline(HoveredObject, state.Camera, minimum, viewportSize, new Vector4(.55f, .65f, .8f, .7f), 1.5f);
        foreach (GameObject selected in state.Selection.Objects)
            DrawObjectOutline(selected, state.Camera, minimum, viewportSize, new Vector4(1f, .82f, .22f, 1f), 2.5f);

        if (state.SelectedObject?.Parent is GameObject parent)
            ImGui.GetWindowDrawList().AddLine(WorldToScreen(state.Camera, parent.Transform.Position, minimum, viewportSize),
                WorldToScreen(state.Camera, state.SelectedObject.Transform.Position, minimum, viewportSize),
                ImGui.GetColorU32(new Vector4(.55f, .65f, .8f, .65f)), 1.5f);

        if (state.Mode != EditorMode.Edit || state.Selection.Count == 0 || Mode == GizmoMode.Select) return;
        Vector2 pivot = WorldToScreen(state.Camera, SelectionPivot(state), minimum, viewportSize);
        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        uint red = ImGui.GetColorU32(new Vector4(1f, .25f, .2f, 1f));
        uint green = ImGui.GetColorU32(new Vector4(.25f, 1f, .35f, 1f));
        uint yellow = ImGui.GetColorU32(new Vector4(1f, .85f, .2f, 1f));
        if (Mode is GizmoMode.Move or GizmoMode.Scale)
        {
            draw.AddLine(pivot, pivot + new Vector2(70f, 0f), red, 3f);
            draw.AddLine(pivot, pivot + new Vector2(0f, -70f), green, 3f);
            if (Mode == GizmoMode.Move)
            {
                draw.AddTriangleFilled(pivot + new Vector2(78f, 0f), pivot + new Vector2(66f, -6f), pivot + new Vector2(66f, 6f), red);
                draw.AddTriangleFilled(pivot + new Vector2(0f, -78f), pivot + new Vector2(-6f, -66f), pivot + new Vector2(6f, -66f), green);
                draw.AddRectFilled(pivot - new Vector2(6f), pivot + new Vector2(6f), yellow);
            }
            else
            {
                draw.AddRectFilled(pivot + new Vector2(65f, -5f), pivot + new Vector2(75f, 5f), red);
                draw.AddRectFilled(pivot + new Vector2(-5f, -75f), pivot + new Vector2(5f, -65f), green);
                draw.AddRectFilled(pivot - new Vector2(7f), pivot + new Vector2(7f), yellow);
            }
        }
        else if (Mode == GizmoMode.Rotate) draw.AddCircle(pivot, 55f, yellow, 48, 3f);
    }

    private void BeginDrag(EditorState state, GizmoHandle handle, Vector2 mouseWorld)
    {
        if (state.Selection.Count == 0) return;
        _activeHandle = handle;
        _startMouseWorld = mouseWorld;
        _pivotWorld = SelectionPivot(state);
        _startPositions.Clear(); _startSizes.Clear(); _startRotations.Clear();
        foreach (GameObject item in state.Selection.Objects)
        {
            _startPositions[item.Id] = item.Transform.Position;
            _startSizes[item.Id] = EditableSize(item);
            _startRotations[item.Id] = item.Transform.Rotation;
        }
        state.Undo?.BeginGesture(state, Mode switch { GizmoMode.Rotate => "Rotate Objects", GizmoMode.Scale => "Scale Objects", _ => "Move Objects" });
    }

    private void ApplyDrag(EditorState state, Vector2 mouseWorld)
    {
        Vector2 delta = mouseWorld - _startMouseWorld;
        bool snap = SnapEnabled || ImGui.GetIO().KeyCtrl;
        foreach (GameObject item in state.Selection.Objects)
        {
            if (Mode is GizmoMode.Select or GizmoMode.Move)
            {
                Vector2 value = _startPositions[item.Id] + new Vector2(_activeHandle == GizmoHandle.Y ? 0f : delta.X, _activeHandle == GizmoHandle.X ? 0f : delta.Y);
                item.Transform.Position = snap ? new Vector2(Snap(value.X, SnapSize), Snap(value.Y, SnapSize)) : value;
            }
            else if (Mode == GizmoMode.Rotate)
            {
                float start = MathF.Atan2(_startMouseWorld.Y - _pivotWorld.Y, _startMouseWorld.X - _pivotWorld.X);
                float current = MathF.Atan2(mouseWorld.Y - _pivotWorld.Y, mouseWorld.X - _pivotWorld.X);
                float value = _startRotations[item.Id] + (current - start) * 180f / MathF.PI;
                item.Transform.Rotation = snap ? Snap(value, 15f) : value;
            }
            else if (Mode == GizmoMode.Scale)
            {
                Vector2 start = _startSizes[item.Id];
                Vector2 value = _activeHandle switch
                {
                    GizmoHandle.X => new Vector2(start.X + delta.X * 2f, start.Y),
                    GizmoHandle.Y => new Vector2(start.X, start.Y + delta.Y * 2f),
                    _ => start + new Vector2((delta.X - delta.Y) * .5f)
                };
                if (snap) value = new Vector2(Snap(value.X, SnapSize), Snap(value.Y, SnapSize));
                SetEditableSize(item, Vector2.Max(value, Vector2.One));
            }
        }
    }

    private GizmoHandle HitGizmo(EditorState state, Vector2 mouse, Vector2 minimum, Vector2 viewportSize)
    {
        if (state.Selection.Count == 0 || Mode == GizmoMode.Select) return GizmoHandle.None;
        Vector2 pivot = WorldToScreen(state.Camera, SelectionPivot(state), minimum, viewportSize);
        if (Mode == GizmoMode.Rotate) return MathF.Abs(Vector2.Distance(mouse, pivot) - 55f) <= 8f ? GizmoHandle.Rotate : GizmoHandle.None;
        if (Vector2.Distance(mouse, pivot) <= 12f) return Mode == GizmoMode.Move ? GizmoHandle.Free : GizmoHandle.Uniform;
        if (DistanceToSegment(mouse, pivot, pivot + new Vector2(78f, 0f)) <= 8f) return GizmoHandle.X;
        if (DistanceToSegment(mouse, pivot, pivot + new Vector2(0f, -78f)) <= 8f) return GizmoHandle.Y;
        return GizmoHandle.None;
    }

    private static GameObject? HitTest(Scene scene, Vector2 world) => scene.GameObjects.Select((item, index) => new { item, index })
        .Where(value => value.item.ActiveInHierarchy && ContainsPoint(value.item, world))
        .OrderBy(value => value.item.GetComponent<SpriteRenderer>()?.OrderInLayer ?? 0).ThenBy(value => value.index)
        .Select(value => value.item).LastOrDefault();

    private static bool ContainsPoint(GameObject gameObject, Vector2 world)
    {
        Vector2 local = Rotate(world - gameObject.Transform.Position, -gameObject.Transform.Rotation * MathF.PI / 180f);
        Vector2 half = EditableSize(gameObject) * .5f;
        return MathF.Abs(local.X) <= half.X && MathF.Abs(local.Y) <= half.Y;
    }

    private static void DrawObjectOutline(GameObject item, EditorCamera camera, Vector2 minimum, Vector2 viewportSize, Vector4 color, float thickness)
    {
        Vector2 half = EditableSize(item) * .5f;
        float radians = item.Transform.Rotation * MathF.PI / 180f;
        Vector2[] local = { new(-half.X, -half.Y), new(half.X, -half.Y), new(half.X, half.Y), new(-half.X, half.Y) };
        Vector2[] points = local.Select(value => WorldToScreen(camera, Rotate(value, radians) + item.Transform.Position, minimum, viewportSize)).ToArray();
        ImGui.GetWindowDrawList().AddQuad(points[0], points[1], points[2], points[3], ImGui.GetColorU32(color), thickness);
    }

    private static Vector2 SelectionPivot(EditorState state) => state.Selection.Objects.Aggregate(Vector2.Zero, (sum, item) => sum + item.Transform.Position) / state.Selection.Count;
    private static Vector2 EditableSize(GameObject item)
    {
        SpriteRenderer? sprite = item.GetComponent<SpriteRenderer>();
        return sprite?.Size ?? new Vector2(item.Transform.LocalScale.X, item.Transform.LocalScale.Y);
    }

    private static void SetEditableSize(GameObject item, Vector2 value)
    {
        SpriteRenderer? sprite = item.GetComponent<SpriteRenderer>();
        if (sprite != null) sprite.Size = value;
        else item.Transform.LocalScale = new Vector3(value.X, value.Y, item.Transform.LocalScale.Z);
    }
    internal static Vector2 ScreenToWorld(EditorCamera camera, Vector2 screen, Vector2 minimum, Vector2 size) => camera.Position + (screen - minimum - size * .5f) / camera.Zoom;
    internal static Vector2 WorldToScreen(EditorCamera camera, Vector2 world, Vector2 minimum, Vector2 size) => minimum + size * .5f + (world - camera.Position) * camera.Zoom;
    private static Vector2 Rotate(Vector2 value, float radians) => new(value.X * MathF.Cos(radians) - value.Y * MathF.Sin(radians), value.X * MathF.Sin(radians) + value.Y * MathF.Cos(radians));
    private static float Snap(float value, float increment) => MathF.Round(value / increment) * increment;
    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 segment = b - a;
        float t = Math.Clamp(Vector2.Dot(point - a, segment) / Math.Max(segment.LengthSquared(), .001f), 0f, 1f);
        return Vector2.Distance(point, a + segment * t);
    }

    private void DrawModeButton(string label, GizmoMode mode)
    {
        if (Mode == mode) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.2f, .45f, .75f, 1f));
        if (ImGui.SmallButton(label)) Mode = mode;
        if (Mode == mode) ImGui.PopStyleColor();
    }
}
