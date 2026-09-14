using System.Numerics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Graphics;

public sealed class DirectionalLight : Component
{
    private float _intensity = 1.0f;
    private float _ambientIntensity = 0.25f;
    private int _shadowResolution = 2048;
    private float _shadowDistance = 50.0f;
    private float _shadowBias = 0.0015f;
    private float _shadowStrength = 1.0f;

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

    public float AmbientIntensity
    {
        get =>
            _ambientIntensity;

        set =>
            _ambientIntensity =
                Math.Max(
                    0.0f,
                    value);
    }

    /// <summary>
    /// Enables the directional shadow-map pass for this light.
    ///
    /// v0.9-d supports one shadow-casting directional light per render view.
    /// The first enabled directional light with CastShadows=true is used.
    /// </summary>
    public bool CastShadows { get; set; } =
        false;

    /// <summary>
    /// Depth texture resolution used by the directional shadow map.
    /// Values are normalized to 256, 512, 1024, 2048 or 4096.
    /// </summary>
    public int ShadowResolution
    {
        get =>
            _shadowResolution;

        set =>
            _shadowResolution =
                NormalizeShadowResolution(
                    value);
    }

    /// <summary>
    /// Approximate world-space extent covered around the active camera.
    /// </summary>
    public float ShadowDistance
    {
        get =>
            _shadowDistance;

        set =>
            _shadowDistance =
                Math.Clamp(
                    value,
                    5.0f,
                    500.0f);
    }

    /// <summary>
    /// Depth comparison bias used to reduce surface acne.
    /// </summary>
    public float ShadowBias
    {
        get =>
            _shadowBias;

        set =>
            _shadowBias =
                Math.Clamp(
                    value,
                    0.00001f,
                    0.05f);
    }

    public float ShadowStrength
    {
        get =>
            _shadowStrength;

        set =>
            _shadowStrength =
                Math.Clamp(
                    value,
                    0.0f,
                    1.0f);
    }

    // Direction is the direction the light rays travel. The editor arrow/transform
    // points toward the scene, while shaders use -Direction as the surface-to-light
    // vector. Keeping that convention here also fixes existing starter scenes.
    public Vector3 Direction =>
        -Transform.Forward;

    private static int NormalizeShadowResolution(
        int value)
    {
        if (value <= 384)
        {
            return 256;
        }

        if (value <= 768)
        {
            return 512;
        }

        if (value <= 1536)
        {
            return 1024;
        }

        if (value <= 3072)
        {
            return 2048;
        }

        return 4096;
    }
}
