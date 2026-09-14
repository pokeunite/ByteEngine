using System.Numerics;

namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>
/// Immutable environment snapshot used for one render pass.
/// </summary>
public readonly record struct RenderEnvironment3D(
    bool DrawSky,
    Vector3 ZenithColor,
    Vector3 HorizonColor,
    Vector3 GroundColor,
    float SkyIntensity,
    float HorizonSharpness,
    bool OverrideAmbient,
    float AmbientIntensity)
{
    public static RenderEnvironment3D Default =>
        new(
            false,
            new Vector3(
                0.08f,
                0.20f,
                0.48f),
            new Vector3(
                0.58f,
                0.72f,
                0.95f),
            new Vector3(
                0.08f,
                0.075f,
                0.07f),
            1.0f,
            1.25f,
            false,
            0.25f);
}
