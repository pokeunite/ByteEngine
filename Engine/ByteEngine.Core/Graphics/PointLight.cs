using System.Numerics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Graphics;

/// <summary>
/// Local omnidirectional light with finite range.
/// </summary>
public sealed class PointLight : Component
{
    private float _intensity = 2.0f;
    private float _range = 10.0f;

    public Vector3 Color { get; set; } =
        Vector3.One;

    public float Intensity
    {
        get =>
            _intensity;

        set =>
            _intensity =
                Math.Max(
                    0.0f,
                    value);
    }

    public float Range
    {
        get =>
            _range;

        set =>
            _range =
                Math.Max(
                    0.01f,
                    value);
    }

    public Vector3 Position =>
        Transform.WorldPosition;
}
