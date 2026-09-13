using System.Numerics;
using ByteEngine.Core.Scene;
using ImGuiNET;

namespace ByteEngine.Editor.Gizmos;

internal enum BlueprintGizmoMode
{
    Translate,
    Rotate,
    Scale
}

internal sealed class BlueprintTransformGizmo3D
{
    private bool _dragging;
    private bool _hoveredHandle;
    private Vector3 _axis;
    private Vector2 _startMouse;
    private Vector3 _startPosition;
    private Vector3 _startRotation;
    private Vector3 _startScale;

    public BlueprintGizmoMode Mode { get; private set; } = BlueprintGizmoMode.Translate;
    public bool OwnsMouse => _dragging || _hoveredHandle;

    public void SetMode(BlueprintGizmoMode mode) => Mode = mode;

    public void DrawToolbar()
    {
        DrawModeButton("Move", BlueprintGizmoMode.Translate);
        ImGui.SameLine();
        DrawModeButton("Rotate", BlueprintGizmoMode.Rotate);
        ImGui.SameLine();
        DrawModeButton("Scale", BlueprintGizmoMode.Scale);
        ImGui.SameLine();
        ImGui.TextDisabled("W / E / R");
    }

    public bool UpdateAndDraw(GameObject? selected, EditorCamera3D camera, bool hovered,
        Vector2 minimum, Vector2 size, Action changed)
    {
        if (hovered)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.W)) Mode = BlueprintGizmoMode.Translate;
            if (ImGui.IsKeyPressed(ImGuiKey.E)) Mode = BlueprintGizmoMode.Rotate;
            if (ImGui.IsKeyPressed(ImGuiKey.R)) Mode = BlueprintGizmoMode.Scale;
        }

        Vector3 hoveredAxis = default;
        _hoveredHandle = selected != null && hovered &&
            TryHitAxis(selected, camera, minimum, size, out hoveredAxis);
        if (selected == null) return false;
        DrawHandles(selected, camera, minimum, size);

        if (_dragging)
        {
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                _dragging = false;
                return true;
            }

            Vector2 origin = Gizmo3DController.Project(_startPosition, camera, minimum, size);
            Vector2 end = Gizmo3DController.Project(_startPosition + _axis, camera, minimum, size);
            Vector2 projected = end - origin;
            float pixels = projected.LengthSquared() > .001f
                ? Vector2.Dot(ImGui.GetMousePos() - _startMouse, Vector2.Normalize(projected))
                : (ImGui.GetMousePos() - _startMouse).X;
            float distance = Vector3.Distance(camera.Position, _startPosition);
            float units = pixels * 2f * distance * MathF.Tan(camera.FieldOfView * MathF.PI / 360f) /
                Math.Max(size.Y, 1f);
            ApplyDragDelta(selected, Mode, _axis, pixels, units, _startPosition, _startRotation, _startScale);
            changed();
            return true;
        }

        if (!hovered || !ImGui.IsMouseClicked(ImGuiMouseButton.Left) || !_hoveredHandle) return false;
        _axis = hoveredAxis;
        _startMouse = ImGui.GetMousePos();
        _startPosition = selected.Transform.WorldPosition;
        _startRotation = selected.Transform.EulerAngles;
        _startScale = selected.Transform.LocalScale;
        _dragging = true;
        return true;
    }

    internal static void ApplyDragDelta(GameObject selected, BlueprintGizmoMode mode, Vector3 axis,
        float pixels, float units, Vector3 startPosition, Vector3 startRotation, Vector3 startScale)
    {
        if (mode == BlueprintGizmoMode.Translate)
            selected.Transform.WorldPosition = startPosition + axis * units;
        else if (mode == BlueprintGizmoMode.Rotate)
            selected.Transform.EulerAngles = startRotation + axis * pixels * .5f;
        else
            selected.Transform.LocalScale = Vector3.Max(startScale + axis * pixels * .01f, new Vector3(.001f));
    }

    private void DrawHandles(GameObject selected, EditorCamera3D camera, Vector2 minimum, Vector2 size)
    {
        Vector3 position = selected.Transform.WorldPosition;
        Vector2 origin = Gizmo3DController.Project(position, camera, minimum, size);
        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        Vector4[] colors =
        {
            new(1f, .2f, .2f, 1f),
            new(.2f, 1f, .3f, 1f),
            new(.2f, .45f, 1f, 1f)
        };
        if (Mode == BlueprintGizmoMode.Rotate)
        {
            for (int index = 0; index < 3; index++)
                draw.AddCircle(origin, 45f + index * 10f, ImGui.GetColorU32(colors[index]), 48, 3f);
            return;
        }
        DrawAxis(draw, origin, Gizmo3DController.Project(position + Vector3.UnitX, camera, minimum, size), colors[0]);
        DrawAxis(draw, origin, Gizmo3DController.Project(position + Vector3.UnitY, camera, minimum, size), colors[1]);
        DrawAxis(draw, origin, Gizmo3DController.Project(position + Vector3.UnitZ, camera, minimum, size), colors[2]);
    }

    private bool TryHitAxis(GameObject selected, EditorCamera3D camera, Vector2 minimum, Vector2 size, out Vector3 axis)
    {
        Vector2 origin = Gizmo3DController.Project(selected.Transform.WorldPosition, camera, minimum, size);
        Vector2 mouse = ImGui.GetMousePos();
        Vector3[] axes = { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ };
        if (Mode == BlueprintGizmoMode.Rotate)
        {
            float distance = Vector2.Distance(mouse, origin);
            for (int index = 0; index < 3; index++)
            {
                if (MathF.Abs(distance - (45f + index * 10f)) <= 5f)
                {
                    axis = axes[index];
                    return true;
                }
            }
            axis = default;
            return false;
        }

        foreach (Vector3 candidate in axes)
        {
            Vector2 direction = Gizmo3DController.Project(selected.Transform.WorldPosition + candidate, camera, minimum, size) - origin;
            if (direction.LengthSquared() < 1f) continue;
            Vector2 end = origin + Vector2.Normalize(direction) * 65f;
            Vector2 segment = end - origin;
            float fraction = Math.Clamp(Vector2.Dot(mouse - origin, segment) / segment.LengthSquared(), 0f, 1f);
            if (Vector2.Distance(mouse, origin + segment * fraction) < 9f)
            {
                axis = candidate;
                return true;
            }
        }
        axis = default;
        return false;
    }

    private static void DrawAxis(ImDrawListPtr draw, Vector2 start, Vector2 projectedEnd, Vector4 color)
    {
        Vector2 direction = projectedEnd - start;
        if (direction.LengthSquared() < 1f) return;
        Vector2 end = start + Vector2.Normalize(direction) * 65f;
        uint packed = ImGui.GetColorU32(color);
        draw.AddLine(start, end, packed, 4f);
        draw.AddCircleFilled(end, 5f, packed);
    }

    private void DrawModeButton(string label, BlueprintGizmoMode mode)
    {
        bool active = Mode == mode;
        if (active) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.18f, .48f, .82f, 1f));
        if (ImGui.SmallButton(label)) Mode = mode;
        if (active) ImGui.PopStyleColor();
    }
}
