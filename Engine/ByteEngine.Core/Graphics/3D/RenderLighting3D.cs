using System.Numerics;

namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>
/// Immutable-per-pass lighting snapshot consumed by Renderer3D.
/// </summary>
public sealed class RenderLighting3D
{
    public const int MaxDirectionalLights =
        4;

    public const int MaxPointLights =
        8;

    public const int MaxPointShadowLights =
        2;

    private readonly RenderDirectionalLight3D[] _directionalLights;

    private readonly RenderPointLight3D[] _pointLights;

    public IReadOnlyList<RenderDirectionalLight3D> DirectionalLights =>
        _directionalLights;

    public IReadOnlyList<RenderPointLight3D> PointLights =>
        _pointLights;

    public int DirectionalLightCount =>
        _directionalLights.Length;

    public int PointLightCount =>
        _pointLights.Length;

    public int SubmittedDirectionalLights { get; }

    public int SubmittedPointLights { get; }

    public int DroppedLightCount =>
        Math.Max(
            0,
            SubmittedDirectionalLights -
            DirectionalLightCount) +
        Math.Max(
            0,
            SubmittedPointLights -
            PointLightCount);

    public float AmbientIntensity { get; }

    public RenderLighting3D(
        IEnumerable<RenderDirectionalLight3D>? directionalLights,
        IEnumerable<RenderPointLight3D>? pointLights,
        float ambientIntensity,
        int submittedDirectionalLights,
        int submittedPointLights)
    {
        _directionalLights =
            directionalLights?
                .Take(
                    MaxDirectionalLights)
                .ToArray() ??
            Array.Empty<RenderDirectionalLight3D>();

        _pointLights =
            pointLights?
                .Take(
                    MaxPointLights)
                .ToArray() ??
            Array.Empty<RenderPointLight3D>();

        AmbientIntensity =
            Math.Clamp(
                ambientIntensity,
                0.0f,
                4.0f);

        SubmittedDirectionalLights =
            Math.Max(
                submittedDirectionalLights,
                _directionalLights.Length);

        SubmittedPointLights =
            Math.Max(
                submittedPointLights,
                _pointLights.Length);
    }

    public int FindShadowDirectionalLightIndex()
    {
        for (int index =
                 0;
             index <
             _directionalLights.Length;
             index++)
        {
            if (_directionalLights[index].CastShadows)
            {
                return index;
            }
        }

        return -1;
    }

    public IReadOnlyList<int> FindShadowPointLightIndices()
    {
        List<int> indices =
            new();

        for (int index = 0;
             index < _pointLights.Length &&
             indices.Count < MaxPointShadowLights;
             index++)
        {
            if (_pointLights[index].CastShadows)
            {
                indices.Add(index);
            }
        }

        return indices;
    }

    /// <summary>
    /// Preserves ByteEngine's pre-v0.9-c visual fallback when a scene has no
    /// explicit lights. The fallback does not cast shadows.
    /// </summary>
    public static RenderLighting3D Default =>
        new(
            new[]
            {
                new RenderDirectionalLight3D(
                    new Vector3(
                        -0.4f,
                        -1.0f,
                        -0.3f),
                    Vector3.One,
                    1.0f,
                    0.25f,
                    false,
                    2048,
                    50.0f,
                    0.0015f,
                    1.0f,
                    1.0f)
            },
            Array.Empty<RenderPointLight3D>(),
            0.25f,
            0,
            0);

    public static RenderLighting3D FromLegacy(
        Vector3 direction,
        Vector3 color,
        float intensity,
        float ambientIntensity)
    {
        return
            new RenderLighting3D(
                new[]
                {
                    new RenderDirectionalLight3D(
                        direction,
                        color,
                        intensity,
                        ambientIntensity,
                        false,
                        2048,
                        50.0f,
                        0.0015f,
                        1.0f,
                        1.0f)
                },
                Array.Empty<RenderPointLight3D>(),
                ambientIntensity,
                1,
                0);
    }
}
