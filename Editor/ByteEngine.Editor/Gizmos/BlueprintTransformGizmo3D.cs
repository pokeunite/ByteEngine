using System.Numerics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Editor.Gizmos;

internal enum BlueprintGizmoMode { Translate, Rotate, Scale }

internal sealed class BlueprintTransformGizmo3D
{
    private readonly TransformGizmoInteraction _interaction = new();
    public BlueprintGizmoMode Mode => (BlueprintGizmoMode)_interaction.Mode;
    public bool OwnsMouse => _interaction.OwnsMouse;
    public void SetMode(BlueprintGizmoMode mode) => _interaction.Mode = (Gizmo3DMode)mode;
    internal void ApplyShortcuts(bool movePressed, bool rotatePressed, bool scalePressed)
    {
        if (movePressed) SetMode(BlueprintGizmoMode.Translate);
        if (rotatePressed) SetMode(BlueprintGizmoMode.Rotate);
        if (scalePressed) SetMode(BlueprintGizmoMode.Scale);
    }
    public void DrawToolbar() => _interaction.DrawToolbar();
    public bool UpdateAndDraw(GameObject? selected, EditorCamera3D camera, bool hovered,
        Vector2 minimum, Vector2 size, Action changed, Action? begin = null, Action? end = null) =>
        _interaction.Update(selected, camera, hovered, minimum, size,
            begin ?? (() => { }), changed, end ?? (() => { }));

    internal static void ApplyDragDelta(GameObject selected, BlueprintGizmoMode mode, Vector3 axis,
        float pixels, float units, Vector3 startPosition, Vector3 startRotation, Vector3 startScale) =>
        Gizmo3DController.ApplyDragDelta(selected, (Gizmo3DMode)mode, axis, pixels, units,
            startPosition, startRotation, startScale);
}
