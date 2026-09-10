using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Graphics;

public sealed class Camera2D : Component
{
    private float _zoom =
        1.0f;

    public float Zoom
    {
        get => _zoom;

        set =>
            _zoom =
                Math.Max(
                    0.01f,
                    value
                );
    }
}