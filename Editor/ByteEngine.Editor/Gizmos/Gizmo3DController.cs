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
    private readonly TransformGizmoInteraction _interaction = new();
    public Gizmo3DMode Mode => _interaction.Mode;
    public bool OwnsMouse => _interaction.OwnsMouse;
    public void SetMode(Gizmo3DMode mode) => _interaction.Mode = mode;
    internal void ApplyShortcuts(bool movePressed, bool rotatePressed, bool scalePressed)
    {
        if (movePressed) SetMode(Gizmo3DMode.Move);
        if (rotatePressed) SetMode(Gizmo3DMode.Rotate);
        if (scalePressed) SetMode(Gizmo3DMode.Scale);
    }
    public void HandleShortcuts(bool sceneFocused) => _interaction.HandleShortcuts(sceneFocused);

    public void DrawToolbar() => _interaction.DrawToolbar();

    public void UpdateAndDraw(EditorState state, EditorCamera3D camera, bool hovered, Vector2 minimum, Vector2 size)
    {
        if (state.Mode != EditorMode.Edit) return;
        if (_interaction.Update(state.SelectedObject, camera, hovered, minimum, size,
            () => state.Undo?.BeginGesture(state, $"{Mode} Object 3D"),
            state.MarkDirty, () => state.Undo?.CommitGesture(state))) return;
        if (!hovered || !ImGui.IsMouseClicked(ImGuiMouseButton.Left)) return;
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
        if (hit != null) { state.SelectedAssetId = null; state.SelectedAssetPath = null; }
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

    internal static Vector2 Project(Vector3 point, EditorCamera3D camera, Vector2 minimum, Vector2 size)
    {
        Matrix4x4 viewProjection = camera.View * camera.Projection(size.X / Math.Max(size.Y, 1f));
        Vector4 clip = Vector4.Transform(new Vector4(point, 1f), viewProjection);
        if (Math.Abs(clip.W) < .0001f) return minimum + size * .5f;
        Vector3 normalized = new(clip.X / clip.W, clip.Y / clip.W, clip.Z / clip.W);
        return minimum + new Vector2((normalized.X + 1f) * .5f * size.X, (1f - normalized.Y) * .5f * size.Y);
    }

    internal static Ray ScreenRay(Vector2 mouse, EditorCamera3D camera, Vector2 minimum, Vector2 size)
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

    internal readonly record struct Ray(Vector3 Origin, Vector3 Direction);
}
