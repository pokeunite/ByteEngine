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
    private int _shadowResolution = 512;
    private float _shadowBias = 0.05f;
    private float _shadowStrength = 1.0f;
    private float _shadowSoftness = 0.06f;

    public Vector3 Color { get; set; } =
        Vector3.One;

    public float Intensity
    {
        get => _intensity;
        set => _intensity = Math.Max(0.0f, value);
    }

    public float Range
    {
        get => _range;
        set => _range = Math.Max(0.05f, value);
    }

    /// <summary>
    /// Enables omnidirectional cubemap shadows for this light.
    /// v0.9-e renders at most two point-light shadow cubemaps per view.
    /// </summary>
    public bool CastShadows { get; set; } =
        false;

    public int ShadowResolution
    {
        get => _shadowResolution;
        set => _shadowResolution = NormalizeShadowResolution(value);
    }

    /// <summary>
    /// World-space depth bias. Point-light cubemaps store linear distance,
    /// so this value is expressed in world units.
    /// </summary>
    public float ShadowBias
    {
        get => _shadowBias;
        set => _shadowBias = Math.Clamp(value, 0.001f, 1.0f);
    }

    public float ShadowStrength
    {
        get => _shadowStrength;
        set => _shadowStrength = Math.Clamp(value, 0.0f, 1.0f);
    }

    /// <summary>
    /// Radius used by cubemap PCF sampling in world-space direction units.
    /// </summary>
    public float ShadowSoftness
    {
        get => _shadowSoftness;
        set => _shadowSoftness = Math.Clamp(value, 0.0f, 0.5f);
    }

    public Vector3 Position =>
        Transform.WorldPosition;

    private static int NormalizeShadowResolution(int value)
    {
        if (value <= 192) return 128;
        if (value <= 384) return 256;
        if (value <= 768) return 512;
        if (value <= 1536) return 1024;
        return 2048;
    }
}
