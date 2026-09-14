using System.Numerics;

namespace ByteEngine.Core.Graphics.ThreeD;

public readonly record struct RenderDirectionalShadow3D(
    int DirectionalLightIndex,
    Matrix4x4 LightViewProjection,
    int DepthTextureId,
    int Resolution,
    float Bias,
    float Strength,
    float Softness);

public readonly record struct DirectionalShadowPassResult(
    RenderDirectionalShadow3D? Shadow,
    int DrawCalls)
{
    public static DirectionalShadowPassResult None =>
        new(null, 0);
}

/// <summary>
/// Cubemap shadow produced for one point light.
/// </summary>
public readonly record struct RenderPointShadow3D(
    int PointLightIndex,
    int DepthCubeTextureId,
    float FarPlane,
    float Bias,
    float Strength,
    float Softness,
    int Resolution);

public sealed class PointShadowPassResult
{
    public IReadOnlyList<RenderPointShadow3D> Shadows { get; }

    public int DrawCalls { get; }

    public int ShadowLightCount =>
        Shadows.Count;

    public int CubemapFacesRendered =>
        Shadows.Count *
        6;

    public PointShadowPassResult(
        IReadOnlyList<RenderPointShadow3D> shadows,
        int drawCalls)
    {
        Shadows = shadows;
        DrawCalls = drawCalls;
    }

    public static PointShadowPassResult None { get; } =
        new(Array.Empty<RenderPointShadow3D>(), 0);
}
