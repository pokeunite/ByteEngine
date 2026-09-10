using System.Numerics;

using ByteEngine.Core.Scene;

namespace ByteEngine.Editor;

internal sealed class EditorCamera
{
    public Vector2 Position { get; set; }

    public float Zoom { get; set; } =
        1.0f;

    public void Reset()
    {
        Position =
            Vector2.Zero;

        Zoom =
            1.0f;
    }

    public void Frame(
        GameObject gameObject,
        Vector2 viewportSize)
    {
        Position =
            gameObject.Transform.Position;

        Vector2 size =
            gameObject.Transform.Size;

        float horizontalZoom =
            viewportSize.X /
            Math.Max(
                size.X * 2.0f,
                1.0f
            );

        float verticalZoom =
            viewportSize.Y /
            Math.Max(
                size.Y * 2.0f,
                1.0f
            );

        Zoom =
            Math.Clamp(
                Math.Min(
                    horizontalZoom,
                    verticalZoom
                ),
                0.2f,
                4.0f
            );
    }
}
