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
    public int PointShadowLimit { get; set; } = MaxPointShadowLights;

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

    // Only freshly allocated internal buffers may use this path. Public construction still copies.
    internal static RenderLighting3D OwnSnapshot(RenderDirectionalLight3D[] suns,RenderPointLight3D[] lamps,float ambient,int submittedSuns,int submittedLamps,int shadowLimit)
        =>new(suns,lamps,ambient,submittedSuns,submittedLamps,shadowLimit);
    private RenderLighting3D(RenderDirectionalLight3D[] suns,RenderPointLight3D[] lamps,float ambient,int submittedSuns,int submittedLamps,int shadowLimit)
    {
        _directionalLights=suns;_pointLights=lamps;AmbientIntensity=Math.Clamp(ambient,0,4);
        SubmittedDirectionalLights=Math.Max(submittedSuns,suns.Length);SubmittedPointLights=Math.Max(submittedLamps,lamps.Length);PointShadowLimit=shadowLimit;
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

    /// <summary>
    /// Chooses the point lights that receive the limited cubemap-shadow slots.
    ///
    /// Lights whose influence volume is nearest to the active camera win.
    /// Intensity is used as a tie-breaker. This is more stable and useful than
    /// simply assigning slots by scene/enumeration order.
    /// </summary>
    public IReadOnlyList<int> FindShadowPointLightIndices(
        Vector3 cameraPosition)
    {
        int limit=Math.Clamp(PointShadowLimit,0,MaxPointShadowLights);
        if(limit==0)return Array.Empty<int>();
        Span<int> selected=stackalloc int[MaxPointShadowLights];int count=0;
        for(int i=0;i<_pointLights.Length;i++)
        {
            if(!_pointLights[i].CastShadows)continue;int index=count;
            while(index>0&&Compare(i,selected[index-1])<0)index--;
            if(index>=limit)continue;
            for(int j=Math.Min(count,limit-1);j>index;j--)selected[j]=selected[j-1];
            selected[index]=i;count=Math.Min(count+1,limit);
        }
        if(count==0)return Array.Empty<int>();
        return selected[..count].ToArray();
        int Compare(int a,int b)
        {
            var x=_pointLights[a];var y=_pointLights[b];
            int order=(Vector3.Distance(x.Position,cameraPosition)-x.Range).CompareTo(Vector3.Distance(y.Position,cameraPosition)-y.Range);
            if(order!=0)return order;order=y.Intensity.CompareTo(x.Intensity);return order!=0?order:a.CompareTo(b);
        }
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
