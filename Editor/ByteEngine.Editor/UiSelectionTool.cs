using System.Numerics;

using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;

using ImGuiNET;

namespace ByteEngine.Editor;

internal sealed class UiSelectionTool
{
    internal sealed record Element(GameObject Object, Component Component, Vector2 Position, Vector2 Size, float Scale, int Order);
    private readonly List<(Element Element, Vector2 Offset)> _dragged = new();
    private Vector2 _dragStart;
    private bool _dragging;
    private Scene? _dragScene;
    internal bool OwnsMouse => _dragging;
    internal static bool IsUiObject(GameObject? obj) => obj?.GetComponent<UiWidget>() != null || obj?.GetComponent<UiText>() != null;

    internal static List<Element> Elements(Scene scene, Vector2 viewport, Func<string, string?, int, Vector2> measure)
    {
        var result = new List<Element>();
        // Match Scene.GetRenderOrder and each object's component submission order.
        foreach (var obj in scene.GameObjects.OrderBy(item => item.RenderOrder))
        {
            if (!obj.ActiveInHierarchy || !UiLayout.IsVisible(obj)) continue;
            foreach (var component in obj.Components)
            {
                if (component is UiWidget { Enabled: true, Visible: true } widget)
                {
                    var rect = UiLayout.Resolve(obj, widget.Anchor, widget.Offset, widget.Size, viewport);
                    result.Add(new(obj, widget, rect.Position, rect.Size, rect.Scale, obj.RenderOrder));
                }
                else if (component is UiText { Enabled: true, Visible: true } text)
                {
                    var rect = text.ResolveEditorBounds(viewport, measure);
                    result.Add(new(obj, text, rect.Position, rect.Size, rect.Scale, obj.RenderOrder));
                }
            }
        }
        return result;
    }

    internal static bool Contains(Element element, Vector2 point, Vector2 viewport)
    {
        var minimum = element.Position;
        var maximum = minimum + element.Size;
        if (UiLayout.ResolveClip(element.Object, viewport) is { } clip)
        {
            minimum = Vector2.Max(minimum, new(clip.X, clip.Y));
            maximum = Vector2.Min(maximum, new(clip.X + clip.Z, clip.Y + clip.W));
        }
        return point.X >= minimum.X && point.Y >= minimum.Y && point.X < maximum.X && point.Y < maximum.Y;
    }

    private static Vector2 Offset(Component component) => component is UiWidget widget ? widget.Offset : ((UiText)component).Offset;
    private static void SetOffset(Component component, Vector2 offset)
    {
        if (component is UiWidget widget) widget.Offset = offset;
        else ((UiText)component).Offset = offset;
    }

    internal static bool IsLayoutManaged(Element element) => element.Component is UiWidget &&
        element.Object.Parent?.GetComponent<UiWidget>() != null &&
        element.Object.Parent.GetComponent<UiContainer>() is { Enabled: true };

    internal void BeginDrag(EditorState state, IEnumerable<Element> elements, Vector2 pointer)
    {
        _dragged.Clear();
        foreach (var element in elements)
        {
            if (!state.Selection.Contains(element.Object) || IsLayoutManaged(element)) continue;
            if (state.Selection.Objects.Any(parent => parent != element.Object && element.Object.IsDescendantOf(parent) && parent.GetComponent<UiWidget>() != null)) continue;
            if (_dragged.Any(item => ReferenceEquals(item.Element.Component, element.Component))) continue;
            _dragged.Add((element, Offset(element.Component)));
        }
        if (_dragged.Count == 0) return;
        _dragStart = pointer;
        _dragScene = state.EditorScene;
        state.Undo?.BeginObjectGesture(state, "Move UI Objects", _dragged.Select(item => item.Element.Object.Id));
        _dragging = true;
    }

    internal void Move(EditorState state, Vector2 pointer)
    {
        foreach (var item in _dragged)
            SetOffset(item.Element.Component, item.Offset + (pointer - _dragStart) / Math.Max(.001f, item.Element.Scale));
        if (pointer != _dragStart) state.MarkDirty();
    }

    internal void Finish(EditorState state, bool cancel = false)
    {
        if (!_dragging) return;
        if (cancel) foreach (var item in _dragged) SetOffset(item.Element.Component, item.Offset);
        state.Undo?.CommitGesture(state);
        _dragged.Clear();
        _dragging = false;
    }

    internal bool Draw(EditorState state, Vector2 start, Vector2 imageSize, Vector2 viewport, bool hovered,
        Func<string, string?, int, Vector2> measure, bool outlines = false)
    {
        if (state.Mode != EditorMode.Edit) { Finish(state); return false; }
        if (_dragging && !ReferenceEquals(_dragScene, state.EditorScene))
        {
            state.Undo?.CancelGesture();
            _dragging = false;
            _dragged.Clear();
        }
        Vector2 scale = imageSize / viewport;
        Vector2 point = (ImGui.GetMousePos() - start) / scale;
        var elements = Elements(state.DisplayedScene, viewport, measure);
        var hits = hovered ? elements.Where(item => Contains(item, point, viewport)).Reverse().ToArray() : [];
        var hit = hits.FirstOrDefault();
        bool consumed = _dragging;
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            if (ImGui.GetIO().KeyAlt && hits.Length > 1)
            {
                int current = Array.FindIndex(hits, item => item.Object == state.SelectedObject);
                hit = hits[(current + 1) % hits.Length];
            }
            if (hit != null)
            {
                if (ImGui.GetIO().KeyCtrl) state.Selection.Toggle(hit.Object);
                else if (!state.Selection.Contains(hit.Object)) state.Selection.Set(hit.Object);
                state.SelectedAssetId = null;
                state.SelectedAssetPath = null;
                if (!ImGui.GetIO().KeyCtrl) BeginDrag(state, elements, point);
                consumed = true;
            }
            else if (IsUiObject(state.SelectedObject)) state.Selection.Clear();
        }
        if (_dragging)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Escape)) Finish(state, cancel: true);
            else if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) Finish(state);
            else Move(state, point);
        }
        var draw = ImGui.GetWindowDrawList();
        draw.PushClipRect(start, start + imageSize, true);
        foreach (var element in elements)
        {
            bool selected = state.Selection.Contains(element.Object);
            if (!selected && !outlines && element != hit) continue;
            Vector2 minimum = start + element.Position * scale;
            Vector2 maximum = minimum + element.Size * scale;
            if (UiLayout.ResolveClip(element.Object, viewport) is { } clip)
            {
                minimum = Vector2.Max(minimum, start + new Vector2(clip.X, clip.Y) * scale);
                maximum = Vector2.Min(maximum, start + new Vector2(clip.X + clip.Z, clip.Y + clip.W) * scale);
            }
            if (maximum.X > minimum.X && maximum.Y > minimum.Y)
                draw.AddRect(minimum, maximum, selected ? 0xFFFFCC44 : element == hit ? 0xFF55DFFF : 0x6068C0A0, 0, ImDrawFlags.None, selected ? 2 : 1);
        }
        if (hit != null && !_dragging)
        {
            string kind = hit.Component is UiWidget widget ? widget.Kind.ToString() : "Text";
            ImGui.SetTooltip(IsLayoutManaged(hit)
                ? $"{hit.Object.Name} ({kind})\nPosition is controlled by the parent UI container.\nAlt-click cycles overlapping elements."
                : $"{hit.Object.Name} ({kind})\nDrag to move. Alt-click cycles overlapping elements.");
        }
        draw.PopClipRect();
        return consumed;
    }
}
