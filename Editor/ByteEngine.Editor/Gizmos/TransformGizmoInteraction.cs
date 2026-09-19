using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Scene;
using ImGuiNET;

namespace ByteEngine.Editor.Gizmos;

internal enum GizmoOrientation { World, Local }

/// <summary>Shared viewport interaction. Hosts own selection and history; this owns a single drag.</summary>
internal sealed class TransformGizmoInteraction
{
    private static readonly Vector3[] Axes = { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ };
    private GameObject? _target;
    private int _handle = -1;
    private int _hover = -1;
    private Vector2 _startMouse;
    private Vector3 _position, _scale, _axis;
    private Quaternion _rotation;
    private Vector3 _lastRingVector;
    private float _angle;
    public Gizmo3DMode Mode { get; set; }
    public GizmoOrientation Orientation { get; set; }
    public bool Dragging => _target != null;
    public bool OwnsMouse => Dragging || _hover >= 0;

    internal static bool CanUseShortcuts(bool focused, bool textInput, bool activeItem, bool captured) =>
        focused && !textInput && !activeItem && !captured;


    public void HandleShortcuts(bool sceneFocused)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        if (Dragging || !CanUseShortcuts(sceneFocused, io.WantTextInput,
                ImGui.IsAnyItemActive(), Input.IsGameInputCaptured)) return;
        if (ImGui.IsKeyPressed(ImGuiKey.W)) Mode = Gizmo3DMode.Move;
        else if (ImGui.IsKeyPressed(ImGuiKey.E)) Mode = Gizmo3DMode.Rotate;
        else if (ImGui.IsKeyPressed(ImGuiKey.R)) Mode = Gizmo3DMode.Scale;
    }
    public void DrawToolbar()
    {
        foreach (Gizmo3DMode mode in Enum.GetValues<Gizmo3DMode>())
        {
            if (mode != Gizmo3DMode.Move) ImGui.SameLine();
            string shortcut = mode switch
            {
                Gizmo3DMode.Move => "W",
                Gizmo3DMode.Rotate => "E",
                _ => "R"
            };
            if (EditorUi.ToolbarToggle(mode.ToString(), Mode == mode, $"{mode} Tool ({shortcut})") && !Dragging)
                Mode = mode;
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(88.0f);
        int orientation = (int)Orientation;
        if (ImGui.Combo("##GizmoOrientation", ref orientation, "World\0Local\0") && !Dragging)
            Orientation = (GizmoOrientation)orientation;
        EditorUi.Tooltip("Transform orientation");
    }

    public bool Update(GameObject? selected, EditorCamera3D camera, bool hovered, Vector2 min, Vector2 size,
        Action begin, Action changed, Action end)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        if (Dragging && (!ReferenceEquals(selected, _target) || !ImGui.IsMouseDown(ImGuiMouseButton.Left)))
        {
            _target = null;
            end();
            _hover = -1;
            return true;
        }
        _hover = -1;
        if (selected == null) return false;
        Vector3 position = selected.Transform.WorldPosition;
        float length = HandleLength(camera, position, size.Y);
        Vector2 origin = Gizmo3DController.Project(position, camera, min, size);
        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        Vector2 mouse = ImGui.GetMousePos();
        float best = 9;
        for (int i = 0; i < 3; i++)
        {
            Vector3 axis = Axis(i, Orientation, selected.Transform.WorldRotation);
            if (Mode == Gizmo3DMode.Rotate)
            {
                Vector3 u = Vector3.Normalize(Vector3.Cross(axis, MathF.Abs(axis.Y) < .9f ? Vector3.UnitY : Vector3.UnitX));
                Vector3 v = Vector3.Cross(axis, u);
                for (int segment = 0; segment < 64; segment++)
                {
                    Vector2 a = RingPoint(segment), b = RingPoint(segment + 1);
                    float distance = DistanceToSegment(mouse, a, b);
                    if (hovered && distance < best) { best = distance; _hover = i; }
                    Vector2 RingPoint(int n) => Gizmo3DController.Project(position +
                        (u * MathF.Cos(n * MathF.Tau / 64) + v * MathF.Sin(n * MathF.Tau / 64)) * length, camera, min, size);
                }
            }
            else
            {
                Vector2 tip = Gizmo3DController.Project(position + axis * length, camera, min, size);
                float distance = DistanceToSegment(mouse, origin, tip);
                if (hovered && distance < best) { best = distance; _hover = i; }
            }
        }
        if (Mode == Gizmo3DMode.Scale && hovered && Vector2.Distance(mouse, origin) < 10) _hover = 3;
        for (int i = 0; i < 3; i++)
        {
            Vector3 axis = Axis(i, Orientation, selected.Transform.WorldRotation);
            Vector4 color = i == 0 ? new(1, .2f, .2f, 1) : i == 1 ? new(.2f, 1, .3f, 1) : new(.2f, .45f, 1, 1);
            if (i == (Dragging ? _handle : _hover)) color = new(1, .85f, .15f, 1);
            uint packed = ImGui.GetColorU32(color);
            if (Mode == Gizmo3DMode.Rotate)
            {
                Vector3 u = Vector3.Normalize(Vector3.Cross(axis, MathF.Abs(axis.Y) < .9f ? Vector3.UnitY : Vector3.UnitX));
                Vector3 v = Vector3.Cross(axis, u);
                Vector2 previous = Gizmo3DController.Project(position + u * length, camera, min, size);
                for (int n = 1; n <= 64; n++)
                {
                    float angle = n * MathF.Tau / 64;
                    Vector2 next = Gizmo3DController.Project(position + length * (u * MathF.Cos(angle) + v * MathF.Sin(angle)), camera, min, size);
                    draw.AddLine(previous, next, packed, 3);
                    previous = next;
                }
            }
            else
            {
                Vector2 tip = Gizmo3DController.Project(position + axis * length, camera, min, size);
                draw.AddLine(origin, tip, packed, 3);
                if (Mode == Gizmo3DMode.Scale) draw.AddRectFilled(tip - new Vector2(5), tip + new Vector2(5), packed);
                else if (Vector2.DistanceSquared(tip, origin) > 1)
                {
                    Vector2 direction = Vector2.Normalize(tip - origin), normal = new(-direction.Y, direction.X);
                    draw.AddTriangleFilled(tip, tip - direction * 13 + normal * 6, tip - direction * 13 - normal * 6, packed);
                }
            }
        }
        if (Mode == Gizmo3DMode.Scale)
            draw.AddRectFilled(origin - new Vector2(6), origin + new Vector2(6),
                ImGui.GetColorU32(_hover == 3 || (Dragging && _handle == 3) ? new Vector4(1, .85f, .15f, 1) : Vector4.One));

        if (Dragging)
        {
            if (Mode == Gizmo3DMode.Rotate)
            {
                if (TryRingVector(mouse, camera, min, size, _position, _axis, out Vector3 vector))
                {
                    _angle += SignedAngle(_lastRingVector, vector, _axis);
                    _lastRingVector = vector;
                    selected.Transform.WorldRotation = Quaternion.Normalize(Quaternion.CreateFromAxisAngle(_axis, _angle) * _rotation);
                }
                draw.AddText(mouse + new Vector2(15), 0xffffffff, $"{(char)('X' + _handle)}: {_angle * 180 / MathF.PI:0.0} deg");
            }
            else
            {
                Vector2 delta = mouse - _startMouse;
                Vector2 projected = Gizmo3DController.Project(_position + _axis, camera, min, size) -
                    Gizmo3DController.Project(_position, camera, min, size);
                float pixels = _handle == 3 ? delta.X - delta.Y :
                    projected.LengthSquared() > .01f ? Vector2.Dot(delta, Vector2.Normalize(projected)) : -delta.Y;
                if (Mode == Gizmo3DMode.Move)
                    selected.Transform.WorldPosition = _position + _axis * pixels / Math.Max(projected.Length(), .1f);
                else selected.Transform.LocalScale = Scale(_scale, _handle, pixels * .01f);
            }
            changed();
            return true;
        }
        if (hovered && _hover >= 0 && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _handle = _hover;
            _position = position;
            _rotation = selected.Transform.WorldRotation;
            _scale = selected.Transform.LocalScale;
            _axis = _handle == 3 ? Vector3.One : Axis(_handle, Orientation, _rotation);
            _startMouse = mouse;
            _angle = 0;
            if (Mode == Gizmo3DMode.Rotate && !TryRingVector(mouse, camera, min, size, position, _axis, out _lastRingVector)) return true;
            begin();
            _target = selected;
            return true;
        }
        return OwnsMouse;
    }

    internal static Vector3 Axis(int index, GizmoOrientation orientation, Quaternion rotation) =>
        orientation == GizmoOrientation.World ? Axes[index] : Vector3.Transform(Axes[index], rotation);
    internal static Vector3 Scale(Vector3 start, int handle, float delta) =>
        Vector3.Max(start * (Vector3.One + (handle == 3 ? Vector3.One : Axes[handle]) * delta), new Vector3(.001f));
    internal static float SignedAngle(Vector3 from, Vector3 to, Vector3 axis) =>
        MathF.Atan2(Vector3.Dot(axis, Vector3.Cross(from, to)), Vector3.Dot(from, to));
    private static float HandleLength(EditorCamera3D camera, Vector3 position, float height) =>
        Math.Clamp(2 * Vector3.Distance(camera.Position, position) * MathF.Tan(camera.FieldOfView * MathF.PI / 360) * 75 / Math.Max(height, 1), .01f, 10000);
    private static bool TryRingVector(Vector2 mouse, EditorCamera3D camera, Vector2 min, Vector2 size,
        Vector3 position, Vector3 axis, out Vector3 vector)
    {
        var ray = Gizmo3DController.ScreenRay(mouse, camera, min, size);
        float denominator = Vector3.Dot(ray.Direction, axis);
        vector = default;
        if (MathF.Abs(denominator) < .00001f) return false;
        float t = Vector3.Dot(position - ray.Origin, axis) / denominator;
        Vector3 radial = ray.Origin + ray.Direction * t - position;
        if (t < 0 || radial.LengthSquared() < .000001f) return false;
        vector = Vector3.Normalize(radial);
        return true;
    }
    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 d = end - start;
        return Vector2.Distance(point, start + d * Math.Clamp(Vector2.Dot(point - start, d) / Math.Max(d.LengthSquared(), .001f), 0, 1));
    }
}
