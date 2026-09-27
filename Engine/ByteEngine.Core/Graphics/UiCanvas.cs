using System.Numerics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Graphics;

// Screen-space root. Children render after the 3D scene and never receive input.
public sealed class UiCanvas : Component
{
    public UiScaleMode ScaleMode { get; set; } = UiScaleMode.ScaleWithScreen;
    public Vector2 ReferenceResolution { get; set; } = new(1280f, 720f);
    public Vector4 SafeAreaInsets { get; set; } = Vector4.Zero;
    private float _userScale = 1f;
    public float UserScale
    {
        get => _userScale;
        set => _userScale = float.IsFinite(value) ? Math.Clamp(value, .1f, 4f) : 1f;
}
}
