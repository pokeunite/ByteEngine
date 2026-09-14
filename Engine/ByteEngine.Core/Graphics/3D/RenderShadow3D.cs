using System.Numerics;

namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>
/// GPU shadow-map data produced by the directional shadow pass and consumed by
/// the normal forward shading pass.
/// </summary>
public readonly record struct RenderDirectionalShadow3D(
    int DirectionalLightIndex,
    Matrix4x4 LightViewProjection,
    int DepthTextureId,
    int Resolution,
    float Bias,
    float Strength);

public readonly record struct DirectionalShadowPassResult(
    RenderDirectionalShadow3D? Shadow,
    int DrawCalls)
{
    public static DirectionalShadowPassResult None =>
        new(
            null,
            0);
}
