using System.Numerics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Graphics;

/// <summary>
/// World-level procedural sky and ambient-light settings.
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

    /// <summary>
    /// Draws the procedural sky behind the 3D scene.
    /// </summary>
    public bool DrawSky { get; set; } =
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

    /// <summary>
    /// Multiplier applied to the procedural sky before display mapping.
    /// Values above 1 are allowed for brighter skies.
    /// </summary>
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

    /// <summary>
    /// Controls how quickly the sky transitions away from the horizon.
    /// </summary>
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

    /// <summary>
    /// When enabled, this environment replaces the scalar ambient-light
    /// intensity normally derived from directional lights.
    /// </summary>
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
