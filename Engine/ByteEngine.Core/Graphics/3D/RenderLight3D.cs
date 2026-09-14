using System.Numerics;

namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>
/// Immutable directional-light snapshot for one render pass.
/// </summary>
public readonly record struct RenderDirectionalLight3D(
    Vector3 Direction,
    Vector3 Color,
    float Intensity,
    float AmbientIntensity,
    bool CastShadows,
    int ShadowResolution,
    float ShadowDistance,
    float ShadowBias,
    float ShadowStrength);

/// <summary>
/// Immutable point-light snapshot for one render pass.
/// </summary>
public readonly record struct RenderPointLight3D(
    Vector3 Position,
    Vector3 Color,
    float Intensity,
    float Range);
