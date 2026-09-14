using System.Numerics;
using ByteEngine.Core.Graphics;

namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>
/// Immutable environment snapshot used for one render pass.
/// </summary>
public readonly record struct RenderEnvironment3D(
    bool DrawSky,
    SkyMode3D SkyMode,
    Texture2D? EnvironmentMapTexture,
    float EnvironmentIntensity,
    float EnvironmentRotationDegrees,
    bool EnvironmentLightingEnabled,
    Vector3 ZenithColor,
    Vector3 HorizonColor,
    Vector3 GroundColor,
    float SkyIntensity,
    float HorizonSharpness,
    bool OverrideAmbient,
    float AmbientIntensity,
    bool FogEnabled,
    FogMode3D FogMode,
    Vector3 FogColor,
    float FogStartDistance,
    float FogEndDistance,
    float FogDensity,
    float FogMaxOpacity)
{
    public static RenderEnvironment3D Default =>
        new(
            false,
            SkyMode3D.Procedural,
            null,
            1.0f,
            0.0f,
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
            0.25f,
            false,
            FogMode3D.Linear,
            new Vector3(
                0.58f,
                0.72f,
                0.95f),
            20.0f,
            100.0f,
            0.025f,
            1.0f);
}
