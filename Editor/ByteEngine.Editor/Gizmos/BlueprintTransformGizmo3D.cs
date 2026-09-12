using System.Numerics;
using ByteEngine.Core.Scene;
using ImGuiNET;

namespace ByteEngine.Editor.Gizmos;

internal sealed class BlueprintTransformGizmo3D
{
    private enum Mode { Translate, Rotate, Scale }
    private Mode _mode;
    private bool _dragging;
    private Vector3 _axis;
    private Vector2 _startMouse;
    private Vector3 _startPosition;
    private Vector3 _startRotation;
    private Vector3 _startScale;

    public bool UpdateAndDraw(GameObject? selected, EditorCamera3D camera, bool hovered,
        Vector2 minimum, Vector2 size, Action changed)
    {
        if (hovered)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.W)) _mode = Mode.Translate;
            if (ImGui.IsKeyPressed(ImGuiKey.E)) _mode = Mode.Rotate;
            if (ImGui.IsKeyPressed(ImGuiKey.R)) _mode = Mode.Scale;
        }
        if (selected == null) return false;
        DrawAxes(selected, camera, minimum, size);
        if (_dragging)
        {
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) { _dragging = false; return true; }
            Vector2 origin = Gizmo3DController.Project(_startPosition, camera, minimum, size);
            Vector2 end = Gizmo3DController.Project(_startPosition + _axis, camera, minimum, size);
            Vector2 projected = end - origin;
            if (projected.LengthSquared() > .001f)
            {
                float pixels = Vector2.Dot(ImGui.GetMousePos() - _startMouse, Vector2.Normalize(projected));
                if (_mode == Mode.Translate)
                {
                    float distance = Vector3.Distance(camera.Position, _startPosition);
                    float units = pixels * 2f * distance * MathF.Tan(camera.FieldOfView * MathF.PI / 360f) / Math.Max(size.Y, 1f);
                    selected.Transform.WorldPosition = _startPosition + _axis * units;
                }
                else if (_mode == Mode.Rotate) selected.Transform.EulerAngles = _startRotation + _axis * pixels * .5f;
                else selected.Transform.LocalScale = Vector3.Max(_startScale + _axis * pixels * .01f, new Vector3(.001f));
                changed();
            }
            return true;
        }
        if (!hovered || !ImGui.IsMouseClicked(ImGuiMouseButton.Left) || !TryHitAxis(selected, camera, minimum, size, out _axis)) return false;
        _startMouse = ImGui.GetMousePos();
        _startPosition = selected.Transform.WorldPosition;
        _startRotation = selected.Transform.EulerAngles;
        _startScale = selected.Transform.LocalScale;
        _dragging = true;
        return true;
    }

    private void DrawAxes(GameObject selected, EditorCamera3D camera, Vector2 minimum, Vector2 size)
    {
        Vector3 position = selected.Transform.WorldPosition;
        Vector2 origin = Gizmo3DController.Project(position, camera, minimum, size);
        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        DrawAxis(draw, origin, Gizmo3DController.Project(position + Vector3.UnitX, camera, minimum, size), new Vector4(1f, .2f, .2f, 1f));
        DrawAxis(draw, origin, Gizmo3DController.Project(position + Vector3.UnitY, camera, minimum, size), new Vector4(.2f, 1f, .3f, 1f));
        DrawAxis(draw, origin, Gizmo3DController.Project(position + Vector3.UnitZ, camera, minimum, size), new Vector4(.2f, .45f, 1f, 1f));
        draw.AddText(origin + new Vector2(8f), ImGui.GetColorU32(new Vector4(1f)), _mode.ToString());
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
    private static bool TryHitAxis(GameObject selected, EditorCamera3D camera, Vector2 minimum, Vector2 size, out Vector3 axis)
    {
        Vector2 origin = Gizmo3DController.Project(selected.Transform.WorldPosition, camera, minimum, size);
        Vector2 mouse = ImGui.GetMousePos();
        foreach (Vector3 candidate in new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ })
        {
            Vector2 direction = Gizmo3DController.Project(selected.Transform.WorldPosition + candidate, camera, minimum, size) - origin;
            if (direction.LengthSquared() < 1f) continue;
            Vector2 end = origin + Vector2.Normalize(direction) * 65f;
            Vector2 segment = end - origin;
            float fraction = Math.Clamp(Vector2.Dot(mouse - origin, segment) / segment.LengthSquared(), 0f, 1f);
            if (Vector2.Distance(mouse, origin + segment * fraction) < 9f) { axis = candidate; return true; }
        }
        axis = default;
        return false;
    }
}
