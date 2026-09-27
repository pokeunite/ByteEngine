using System.Numerics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Graphics;

public enum UiScaleMode { ConstantPixels, ScaleWithScreen }

public static class UiLayout
{
    public static (Vector2 Origin, Vector2 Size, float Scale) ResolveCanvas(
        UiCanvas canvas, Vector2 viewport)
    {
        Vector2 reference = canvas.ReferenceResolution;
        float scale = canvas.ScaleMode == UiScaleMode.ScaleWithScreen
            ? MathF.Min(viewport.X / MathF.Max(1, reference.X),
                viewport.Y / MathF.Max(1, reference.Y))
            : 1f;
        scale = Math.Clamp(scale * canvas.UserScale, .1f, 8f);
        Vector4 insets = canvas.SafeAreaInsets * scale;
        Vector2 origin = new(Math.Max(0f, insets.X), Math.Max(0f, insets.Y));
        Vector2 size = new(Math.Max(0f, viewport.X - origin.X - Math.Max(0f, insets.Z)),
            Math.Max(0f, viewport.Y - origin.Y - Math.Max(0f, insets.W)));
        return (origin, size, scale);
    }

    public static bool IsVisible(GameObject gameObject)
    {
        bool hasCanvas = false;
        for (GameObject? current = gameObject; current != null; current = current.Parent)
        {
            if (current.GetComponent<UiWidget>() is { Visible: false }) return false;
            if (current.GetComponent<UiCanvas>() is { } canvas)
            {
                if (!canvas.Enabled) return false;
                hasCanvas = true;
            }
        }
        return hasCanvas;
    }

    public static bool TryGetCanvas(GameObject gameObject, out UiCanvas? canvas)
    {
        GameObject? current = gameObject;
        while (current != null)
        {
            canvas = current.GetComponent<UiCanvas>();
            if (canvas is { Enabled: true }) return true;
            current = current.Parent;
        }
        canvas = null;
        return false;
    }

    public static (Vector2 Origin, Vector2 Size, float Scale) ResolveParent(
        GameObject gameObject, Vector2 viewport)
    {
        if (!TryGetCanvas(gameObject, out UiCanvas? canvas) || canvas == null)
            return (Vector2.Zero, Vector2.Zero, 1f);
        var root = ResolveCanvas(canvas, viewport);
        for (GameObject? parent = gameObject.Parent; parent != null; parent = parent.Parent)
        {
            if (parent.GetComponent<UiWidget>() is { } widget)
            {
                var rect = Resolve(parent, widget.Anchor, widget.Offset,
                    widget.Size, viewport);
                return (rect.Position, rect.Size, root.Scale);
            }
            if (ReferenceEquals(parent, canvas.GameObject)) break;
        }
        return root;
    }

    public static (Vector2 Position, Vector2 Size, float Scale) Resolve(
        GameObject gameObject, UiAnchor anchor, Vector2 offset, Vector2 size,
        Vector2 viewport)
    {
        if (!TryGetCanvas(gameObject, out UiCanvas? canvas) || canvas == null)
            return (Vector2.Zero, Vector2.Zero, 1f);
        var root = ResolveCanvas(canvas, viewport);
        Vector2 parentOrigin = root.Origin;
        Vector2 parentSize = root.Size;
        for (GameObject? parent = gameObject.Parent; parent != null; parent = parent.Parent)
        {
            if (parent.GetComponent<UiWidget>() is { } widget)
            {
                var parentRect = Resolve(parent, widget.Anchor, widget.Offset,
                    widget.Size, viewport);
                parentOrigin = parentRect.Position;
                parentSize = parentRect.Size;
                break;
            }
            if (ReferenceEquals(parent, canvas.GameObject)) break;
        }

        Vector2 anchorPoint = anchor switch
        {
            UiAnchor.TopCenter => new(.5f, 0f),
            UiAnchor.TopRight => new(1f, 0f),
            UiAnchor.Center => new(.5f, .5f),
            UiAnchor.BottomLeft => new(0f, 1f),
            UiAnchor.BottomCenter => new(.5f, 1f),
            UiAnchor.BottomRight => new(1f, 1f),
            _ => Vector2.Zero
        };
        Vector2 scaledSize = Vector2.Max(Vector2.Zero, size * root.Scale);
        Vector2 position = parentOrigin + parentSize * anchorPoint +
            (offset + new Vector2(gameObject.Transform.LocalPosition.X,
                gameObject.Transform.LocalPosition.Y)) * root.Scale -
            scaledSize * anchorPoint;
        if (gameObject.GetComponent<UiWidget>() is { } current)
        {
            if (current.StretchHorizontal)
            {
                float inset = Math.Max(0f, offset.X * root.Scale);
                position.X = parentOrigin.X + inset;
                scaledSize.X = Math.Max(0f, parentSize.X - inset * 2f);
            }
            if (current.StretchVertical)
            {
                float inset = Math.Max(0f, offset.Y * root.Scale);
                position.Y = parentOrigin.Y + inset;
                scaledSize.Y = Math.Max(0f, parentSize.Y - inset * 2f);
            }
        }
        return (position, scaledSize, root.Scale);
    }
}
