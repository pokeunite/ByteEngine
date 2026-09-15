using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Graphics;

/// <summary>
/// World-level procedural sky, ambient-light, image-based-lighting,
/// atmospheric-fog and display-exposure settings.
///
/// Add one enabled SkyEnvironment to a scene. If multiple are enabled,
/// ByteEngine uses the first active instance encountered in scene order.
/// </summary>
public sealed class SkyEnvironment : Component
{
    private Vector3 _zenithColor =
        new(
            0.08f,
            0.20f,
            0.48f);

    private Vector3 _horizonColor =
        new(
            0.58f,
            0.72f,
            0.95f);

    private Vector3 _groundColor =
        new(
            0.08f,
            0.075f,
            0.07f);

    private float _skyIntensity =
        1.0f;

    private float _horizonSharpness =
        1.25f;

    private float _ambientIntensity =
        0.20f;

    private float _environmentIntensity =
        1.0f;

    private float _environmentRotationDegrees;

    private float _exposure =
        1.0f;

    private Vector3 _fogColor =
        new(
            0.58f,
            0.72f,
            0.95f);

    private float _fogStartDistance =
        20.0f;

    private float _fogEndDistance =
        100.0f;

    private float _fogDensity =
        0.025f;

    private float _fogMaxOpacity =
        1.0f;

    /// <summary>
    /// Draws the selected sky source behind the 3D scene.
    /// </summary>
    public bool DrawSky { get; set; } =
        true;

    public SkyMode3D SkyMode { get; set; } =
        SkyMode3D.Procedural;

    public AssetReference EnvironmentMapReference { get; set; } =
        AssetReference.Empty;

    public Texture2D? EnvironmentMapTexture { get; private set; }

    public float EnvironmentIntensity
    {
        get =>
            _environmentIntensity;

        set =>
            _environmentIntensity =
                Math.Clamp(
                    value,
                    0.0f,
                    16.0f);
    }

    public float EnvironmentRotationDegrees
    {
        get =>
            _environmentRotationDegrees;

        set =>
            _environmentRotationDegrees =
                NormalizeDegrees(
                    value);
    }

    /// <summary>
    /// Scene-wide display exposure applied after the complete 3D scene has
    /// been composed into the HDR framebuffer and before tone mapping.
    /// 1.0 preserves the previous ByteEngine appearance.
    /// </summary>
    public float Exposure
    {
        get =>
            _exposure;

        set =>
            _exposure =
                Math.Clamp(
                    float.IsFinite(value)
                        ? value
                        : 1.0f,
                    0.0f,
                    16.0f);
    }

    public void SetEnvironmentMapTexture(
        Texture2D? texture)
    {
        EnvironmentMapTexture =
            texture;
    }

    /// <summary>
    /// Allows the environment map to contribute diffuse and specular
    /// image-based lighting to standard 3D materials.
    /// </summary>
    public bool EnvironmentLightingEnabled { get; set; } =
        true;

    public Vector3 ZenithColor
    {
        get =>
            _zenithColor;

        set =>
            _zenithColor =
                ClampColor(
                    value);
    }

    public Vector3 HorizonColor
    {
        get =>
            _horizonColor;

        set =>
            _horizonColor =
                ClampColor(
                    value);
    }

    public Vector3 GroundColor
    {
        get =>
            _groundColor;

        set =>
            _groundColor =
                ClampColor(
                    value);
    }

    public float SkyIntensity
    {
        get =>
            _skyIntensity;

        set =>
            _skyIntensity =
                Math.Clamp(
                    value,
                    0.0f,
                    16.0f);
    }

    public float HorizonSharpness
    {
        get =>
            _horizonSharpness;

        set =>
            _horizonSharpness =
                Math.Clamp(
                    value,
                    0.1f,
                    8.0f);
    }

    public bool OverrideAmbient { get; set; } =
        true;

    public float AmbientIntensity
    {
        get =>
            _ambientIntensity;

        set =>
            _ambientIntensity =
                Math.Clamp(
                    value,
                    0.0f,
                    4.0f);
    }

    public bool FogEnabled { get; set; }

    public FogMode3D FogMode { get; set; } =
        FogMode3D.Linear;

    public Vector3 FogColor
    {
        get =>
            _fogColor;

        set =>
            _fogColor =
                ClampColor(
                    value);
    }

    public float FogStartDistance
    {
        get =>
            _fogStartDistance;

        set
        {
            _fogStartDistance =
                Math.Clamp(
                    value,
                    0.0f,
                    10000.0f);

            if (_fogEndDistance <
                _fogStartDistance +
                0.01f)
            {
                _fogEndDistance =
                    _fogStartDistance +
                    0.01f;
            }
        }
    }

    public float FogEndDistance
    {
        get =>
            _fogEndDistance;

        set =>
            _fogEndDistance =
                Math.Clamp(
                    value,
                    _fogStartDistance +
                    0.01f,
                    10000.0f);
    }

    public float FogDensity
    {
        get =>
            _fogDensity;

        set =>
            _fogDensity =
                Math.Clamp(
                    value,
                    0.0f,
                    10.0f);
    }

    public float FogMaxOpacity
    {
        get =>
            _fogMaxOpacity;

        set =>
            _fogMaxOpacity =
                Math.Clamp(
                    value,
                    0.0f,
                    1.0f);
    }

    private static float NormalizeDegrees(
        float value)
    {
        if (!float.IsFinite(
                value))
        {
            return 0.0f;
        }

        float normalized =
            value %
            360.0f;

        return normalized <
            0.0f
                ? normalized +
                  360.0f
                : normalized;
    }

    private static Vector3 ClampColor(
        Vector3 value)
    {
        return
            new Vector3(
                Math.Clamp(
                    value.X,
                    0.0f,
                    16.0f),
                Math.Clamp(
                    value.Y,
                    0.0f,
                    16.0f),
                Math.Clamp(
                    value.Z,
                    0.0f,
                    16.0f));
    }
}
