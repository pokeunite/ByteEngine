using System.Numerics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ImGuiNET;

namespace ByteEngine.Editor.Gizmos;

internal enum Gizmo3DMode
{
    Move,
    Rotate,
    Scale
}

internal sealed class Gizmo3DController
{
    private Vector3 _axis;
    private Vector2 _startMouse;
    private Vector3 _startPosition;
    private Vector3 _startRotation;
    private Vector3 _startScale;
    private bool _dragging;
    private bool _hoveredHandle;

    public Gizmo3DMode Mode { get; private set; } = Gizmo3DMode.Move;
    public bool OwnsMouse => _dragging || _hoveredHandle;

    public void SetMode(Gizmo3DMode mode) => Mode = mode;

    public void DrawToolbar()
    {
        DrawModeButton("Move", Gizmo3DMode.Move);
        ImGui.SameLine();
        DrawModeButton("Rotate", Gizmo3DMode.Rotate);
        ImGui.SameLine();
        DrawModeButton("Scale", Gizmo3DMode.Scale);
        ImGui.SameLine();
        ImGui.TextDisabled("W / E / R");
    }

    public void UpdateAndDraw(EditorState state, EditorCamera3D camera, bool hovered,
        Vector2 minimum, Vector2 size)
    {
        if (hovered)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.W)) Mode = Gizmo3DMode.Move;
            if (ImGui.IsKeyPressed(ImGuiKey.E)) Mode = Gizmo3DMode.Rotate;
            if (ImGui.IsKeyPressed(ImGuiKey.R)) Mode = Gizmo3DMode.Scale;
        }

        GameObject? selected = state.SelectedObject;
        Vector3 hoveredAxis = default;
        _hoveredHandle = selected != null && hovered &&
            TryHitHandle(selected, camera, minimum, size, out hoveredAxis);
        if (selected != null) DrawHandles(selected, camera, minimum, size);
        if (state.Mode != EditorMode.Edit) return;

        if (_dragging)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                Vector2 origin = Project(_startPosition, camera, minimum, size);
                Vector2 endpoint = Project(_startPosition + _axis, camera, minimum, size);
                Vector2 projectedAxis = endpoint - origin;
                float pixels = projectedAxis.LengthSquared() > .0001f
                    ? Vector2.Dot(ImGui.GetMousePos() - _startMouse, Vector2.Normalize(projectedAxis))
                    : (ImGui.GetMousePos() - _startMouse).X;
                float distance = Vector3.Distance(camera.Position, _startPosition);
                float units = pixels * 2f * distance * MathF.Tan(camera.FieldOfView * MathF.PI / 360f) /
                    Math.Max(size.Y, 1f);
                ApplyDragDelta(selected!, Mode, _axis, pixels, units,
                    _startPosition, _startRotation, _startScale);
                state.MarkDirty();
            }
            else
            {
                _dragging = false;
                state.Undo?.CommitGesture(state);
            }
            return;
        }

        if (!hovered || !ImGui.IsMouseClicked(ImGuiMouseButton.Left)) return;
        if (selected != null && _hoveredHandle)
        {
            _axis = hoveredAxis;
            _startMouse = ImGui.GetMousePos();
            _startPosition = selected.Transform.WorldPosition;
            _startRotation = selected.Transform.EulerAngles;
            _startScale = selected.Transform.LocalScale;
            _dragging = true;
            state.Undo?.BeginGesture(state, Mode switch
            {
                Gizmo3DMode.Rotate => "Rotate Object 3D",
                Gizmo3DMode.Scale => "Scale Object 3D",
                _ => "Move Object 3D"
            });
            return;
        }

        Ray ray = ScreenRay(ImGui.GetMousePos(), camera, minimum, size);
        GameObject? hit = null;
        float bestDistance = float.MaxValue;
        foreach (GameObject item in state.DisplayedScene.GameObjects)
        {
            if (item.GetComponent<MeshRenderer>() == null || !item.ActiveInHierarchy) continue;
            if (RayBox(ray, item.Transform.WorldMatrix, out float distance) && distance < bestDistance)
            {
                bestDistance = distance;
                hit = item;
            }
        }
        state.Selection.Set(hit);
        if (hit != null)
        {
            state.SelectedAssetId = null;
            state.SelectedAssetPath = null;
        }
    }

    internal static void ApplyDragDelta(GameObject selected, Gizmo3DMode mode, Vector3 axis,
        float pixels, float units, Vector3 startPosition, Vector3 startRotation, Vector3 startScale)
    {
        if (mode == Gizmo3DMode.Move)
            selected.Transform.WorldPosition = startPosition + axis * units;
        else if (mode == Gizmo3DMode.Rotate)
            selected.Transform.EulerAngles = startRotation + axis * pixels * .5f;
        else
            selected.Transform.LocalScale = Vector3.Max(startScale + axis * pixels * .01f, new Vector3(.001f));
    }

    internal static Vector3 ScreenToGroundPlane(Vector2 mouse, EditorCamera3D camera,
        Vector2 minimum, Vector2 size)
    {
        Ray ray = ScreenRay(mouse, camera, minimum, size);
        if (MathF.Abs(ray.Direction.Y) > .0001f)
        {
            float distance = -ray.Origin.Y / ray.Direction.Y;
            if (distance >= 0f) return ray.Origin + ray.Direction * distance;
        }
        float fallbackDistance = Math.Max(camera.Position.Length() * .5f, 5f);
        return ray.Origin + ray.Direction * fallbackDistance;
    }

    private void DrawHandles(GameObject item, EditorCamera3D camera, Vector2 minimum, Vector2 size)
    {
        Vector3 position = item.Transform.WorldPosition;
        Vector2 origin = Project(position, camera, minimum, size);
        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        Vector4[] colors = { new(1f, .2f, .2f, 1f), new(.2f, 1f, .3f, 1f), new(.2f, .45f, 1f, 1f) };
        if (Mode == Gizmo3DMode.Rotate)
        {
            for (int index = 0; index < 3; index++)
                draw.AddCircle(origin, 45f + index * 10f, ImGui.GetColorU32(colors[index]), 48, 3f);
            return;
        }
        DrawAxis(draw, origin, Project(position + Vector3.UnitX, camera, minimum, size), colors[0]);
        DrawAxis(draw, origin, Project(position + Vector3.UnitY, camera, minimum, size), colors[1]);
        DrawAxis(draw, origin, Project(position + Vector3.UnitZ, camera, minimum, size), colors[2]);
    }

    private bool TryHitHandle(GameObject item, EditorCamera3D camera, Vector2 minimum, Vector2 size,
        out Vector3 axis)
    {
        Vector3 position = item.Transform.WorldPosition;
        Vector2 origin = Project(position, camera, minimum, size);
        Vector2 mouse = ImGui.GetMousePos();
        Vector3[] axes = { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ };
        if (Mode == Gizmo3DMode.Rotate)
        {
            float radius = Vector2.Distance(mouse, origin);
            for (int index = 0; index < 3; index++)
            {
                if (MathF.Abs(radius - (45f + index * 10f)) <= 5f)
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
            Vector2 direction = Project(position + candidate, camera, minimum, size) - origin;
            if (direction.LengthSquared() < 1f) continue;
            Vector2 end = origin + Vector2.Normalize(direction) * 70f;
            if (DistanceToSegment(mouse, origin, end) < 9f)
            {
                axis = candidate;
                return true;
            }
        }
        axis = default;
        return false;
    }

    private static void DrawAxis(ImDrawListPtr draw, Vector2 start, Vector2 end, Vector4 color)
    {
        Vector2 direction = end - start;
        if (direction.LengthSquared() < 1f) return;
        end = start + Vector2.Normalize(direction) * 70f;
        uint packed = ImGui.GetColorU32(color);
        draw.AddLine(start, end, packed, 4f);
        draw.AddCircleFilled(end, 5f, packed);
    }

    internal static Vector2 Project(Vector3 point, EditorCamera3D camera, Vector2 minimum, Vector2 size)
    {
        Matrix4x4 viewProjection = camera.View * camera.Projection(size.X / Math.Max(size.Y, 1f));
        Vector4 clip = Vector4.Transform(new Vector4(point, 1f), viewProjection);
        if (Math.Abs(clip.W) < .0001f) return minimum + size * .5f;
        Vector3 normalized = new(clip.X / clip.W, clip.Y / clip.W, clip.Z / clip.W);
        return minimum + new Vector2((normalized.X + 1f) * .5f * size.X, (1f - normalized.Y) * .5f * size.Y);
    }

    private static Ray ScreenRay(Vector2 mouse, EditorCamera3D camera, Vector2 minimum, Vector2 size)
    {
        float width = Math.Max(size.X, 1f);
        float height = Math.Max(size.Y, 1f);
        float x = (mouse.X - minimum.X) / width * 2f - 1f;
        float y = 1f - (mouse.Y - minimum.Y) / height * 2f;
        Matrix4x4.Invert(camera.View * camera.Projection(width / height), out Matrix4x4 inverse);
        Vector4 near = Vector4.Transform(new Vector4(x, y, 0f, 1f), inverse);
        Vector4 far = Vector4.Transform(new Vector4(x, y, 1f, 1f), inverse);
        Vector3 origin = new(near.X / near.W, near.Y / near.W, near.Z / near.W);
        Vector3 destination = new(far.X / far.W, far.Y / far.W, far.Z / far.W);
        return new Ray(origin, Vector3.Normalize(destination - origin));
    }

    private static bool RayBox(Ray ray, Matrix4x4 world, out float distance)
    {
        distance = 0f;
        if (!Matrix4x4.Invert(world, out Matrix4x4 inverse)) return false;
        Vector3 origin = Vector3.Transform(ray.Origin, inverse);
        Vector3 direction = Vector3.TransformNormal(ray.Direction, inverse);
        float minimum = 0f;
        float maximum = 100000f;
        for (int axis = 0; axis < 3; axis++)
        {
            float originValue = axis == 0 ? origin.X : axis == 1 ? origin.Y : origin.Z;
            float directionValue = axis == 0 ? direction.X : axis == 1 ? direction.Y : direction.Z;
            if (Math.Abs(directionValue) < .00001f)
            {
                if (originValue < -.5f || originValue > .5f) return false;
                continue;
            }
            float entry = (-.5f - originValue) / directionValue;
            float exit = (.5f - originValue) / directionValue;
            if (entry > exit) (entry, exit) = (exit, entry);
            minimum = Math.Max(minimum, entry);
            maximum = Math.Min(maximum, exit);
            if (minimum > maximum) return false;
        }
        distance = minimum;
        return true;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 direction = end - start;
        float fraction = Math.Clamp(
            Vector2.Dot(point - start, direction) / Math.Max(direction.LengthSquared(), .001f), 0f, 1f);
        return Vector2.Distance(point, start + direction * fraction);
    }

    private void DrawModeButton(string label, Gizmo3DMode mode)
    {
        bool active = Mode == mode;
        if (active) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.18f, .48f, .82f, 1f));
        if (ImGui.SmallButton(label)) Mode = mode;
        if (active) ImGui.PopStyleColor();
    }

    private readonly record struct Ray(Vector3 Origin, Vector3 Direction);
}
