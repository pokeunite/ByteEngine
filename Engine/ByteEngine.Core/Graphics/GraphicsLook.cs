using System.Numerics;
using System.Text.Json.Nodes;

namespace ByteEngine.Core.Graphics;

public enum GraphicsQuality { Custom, Fast, Balanced, High }
public enum LightingPreset { Custom, Daylight, Overcast, Interior, Night }
public enum BuiltInSceneShader { Standard, PixelArt }

/// <summary>Immutable, bounded settings captured once per view. Defaults preserve legacy scenes.</summary>
public readonly record struct GraphicsLook(GraphicsQuality Quality,float Occlusion,float Radius,float Bloom,float Threshold,float Saturation,float Contrast,float Warmth,bool ProfileGpu)
{
    public float RenderScale { get; init; } = 1;
    public BuiltInSceneShader ShaderStyle { get; init; }
    public int PixelSize { get; init; } = 4;
    public int PixelColorLevels { get; init; } = 8;
    public float PixelDither { get; init; } = .35f;
    public int WaterDetail => Quality==GraphicsQuality.Fast ? 0 : Quality==GraphicsQuality.Balanced ? 1 : 2;
    public int AoSamples => Quality==GraphicsQuality.High ? 12 : 8;
    public int ShadowResolutionCap => Quality==GraphicsQuality.Fast ? 1024 : Quality==GraphicsQuality.Balanced ? 2048 : 4096;
    public int PointShadowLimit => Quality==GraphicsQuality.Fast ? 0 : Quality==GraphicsQuality.Balanced ? 1 : 2;
    public static GraphicsLook Default => new(GraphicsQuality.Custom,0,.5f,0,1.2f,1,1,0,false);
}

public sealed partial class SkyEnvironment
{
    private GraphicsQuality _quality;
    private LightingPreset _lightingPreset;
    private float _ao,_radius=.5f,_bloom,_threshold=1.2f,_saturation=1,_contrast=1,_warmth;
    public GraphicsQuality Quality { get=>_quality; set { _quality=Enum.IsDefined(value)?value:GraphicsQuality.Custom; ApplyQuality(); } }
    public LightingPreset Lighting { get=>_lightingPreset; set { _lightingPreset=Enum.IsDefined(value)?value:LightingPreset.Custom; ApplyLighting(); } }
    public float AmbientOcclusion { get=>_ao; set=>_ao=Safe(value,0,0,1); }
    public float OcclusionRadius { get=>_radius; set=>_radius=Safe(value,.5f,.05f,3); }
    public float Bloom { get=>_bloom; set=>_bloom=Safe(value,0,0,1); }
    public float BloomThreshold { get=>_threshold; set=>_threshold=Safe(value,1.2f,.1f,20); }
    public float Saturation { get=>_saturation; set=>_saturation=Safe(value,1,0,2); }
    public float Contrast { get=>_contrast; set=>_contrast=Safe(value,1,.5f,1.5f); }
    public float Warmth { get=>_warmth; set=>_warmth=Safe(value,0,-1,1); }
    public bool ProfileGraphicsGpu { get; set; }
    private float _renderScale=1;
    /// <summary>3D raster resolution only. UI and pointer coordinates retain native resolution.</summary>
    public float RenderScale { get=>_renderScale; set=>_renderScale=Safe(value,1,.5f,1); }
    private BuiltInSceneShader _shaderStyle;
    private int _pixelSize=4,_pixelColorLevels=8;
    private float _pixelDither=.35f;
    public BuiltInSceneShader ShaderStyle { get=>_shaderStyle; set=>_shaderStyle=Enum.IsDefined(value)?value:BuiltInSceneShader.Standard; }
    public int PixelSize { get=>_pixelSize; set=>_pixelSize=Math.Clamp(value,1,32); }
    public int PixelColorLevels { get=>_pixelColorLevels; set=>_pixelColorLevels=Math.Clamp(value,2,32); }
    public float PixelDither { get=>_pixelDither; set=>_pixelDither=Safe(value,.35f,0,1); }
    public GraphicsLook CaptureLook() => new(Quality,AmbientOcclusion,OcclusionRadius,Bloom,BloomThreshold,Saturation,Contrast,Warmth,ProfileGraphicsGpu){RenderScale=RenderScale,ShaderStyle=ShaderStyle,PixelSize=PixelSize,PixelColorLevels=PixelColorLevels,PixelDither=PixelDither};
    internal void RestorePresetLabels(GraphicsQuality quality,LightingPreset lighting) { _quality=Enum.IsDefined(quality)?quality:GraphicsQuality.Custom; _lightingPreset=Enum.IsDefined(lighting)?lighting:LightingPreset.Custom; }
    private static float Safe(float value,float fallback,float min,float max) => Math.Clamp(float.IsFinite(value)?value:fallback,min,max);
    private void ApplyQuality()
    {
        if(Quality==GraphicsQuality.Custom) return;
        AmbientOcclusion=Quality==GraphicsQuality.Fast?0:Quality==GraphicsQuality.Balanced?.3f:.45f;
        Bloom=Quality==GraphicsQuality.Fast?0:.08f; OcclusionRadius=.5f; BloomThreshold=1.2f; SmoothEdges=true;
    }
    private void ApplyLighting()
    {
        if(Lighting==LightingPreset.Custom) return;
        DrawSky=true; OverrideAmbient=true; EnvironmentLightingEnabled=true; Exposure=1; Saturation=1; Contrast=1; Warmth=0;
        // Preserve an assigned HDRI rather than overwriting the artist's environment.
        EnvironmentIntensity=Lighting==LightingPreset.Night?.35f:1;
        AmbientIntensity=Lighting switch { LightingPreset.Daylight=>.12f, LightingPreset.Overcast=>.28f, LightingPreset.Interior=>.08f, _=>.04f };
        ZenithColor=Lighting==LightingPreset.Night?new(.015f,.025f,.07f):Lighting==LightingPreset.Overcast?new(.4f,.46f,.55f):new(.08f,.2f,.48f);
        HorizonColor=Lighting==LightingPreset.Night?new(.04f,.06f,.12f):Lighting==LightingPreset.Overcast?new(.65f,.68f,.72f):new(.58f,.72f,.95f);
        GroundColor=new(.08f,.075f,.07f); FogEnabled=Lighting==LightingPreset.Night; FogColor=HorizonColor; FogDensity=.008f;
        if(FogEnabled) FogMode=ThreeD.FogMode3D.Exponential;
    }
    /// <summary>Explicit editor operation; deserializing presets never creates lights.</summary>
    public void ApplyLightingToScene()
    {
        if(Lighting==LightingPreset.Custom || GameObject.Scene is not {} scene) return;
        var sun=scene.GameObjects.Where(o=>o.ActiveInHierarchy).SelectMany(o=>o.Components).OfType<DirectionalLight>().FirstOrDefault(l=>l.Enabled);
        if(sun==null) sun=scene.CreateGameObject("Sunlight").AddComponent(new DirectionalLight());
        sun.Intensity=Lighting switch { LightingPreset.Daylight=>2, LightingPreset.Overcast=>.7f, LightingPreset.Interior=>.3f, _=>.2f };
        sun.Color=Lighting==LightingPreset.Night?new(.5f,.65f,1):Lighting==LightingPreset.Daylight?new(1,.95f,.85f):Vector3.One;
        sun.AmbientIntensity=0; sun.CastShadows=true; sun.ShadowResolution=2048; sun.ShadowDistance=60; sun.ShadowSoftness=Lighting==LightingPreset.Overcast?2:1;
        sun.Transform.EulerAngles=new(45,-30,0);
    }
}

/// <summary>Shared extension for both legacy and current SkyEnvironment codecs.</summary>
internal static class GraphicsLookSerialization
{
    public static JsonObject Save(SkyEnvironment e) => new() {
        ["quality"]=e.Quality.ToString(),["lighting"]=e.Lighting.ToString(),["ao"]=e.AmbientOcclusion,["radius"]=e.OcclusionRadius,
        ["bloom"]=e.Bloom,["threshold"]=e.BloomThreshold,["saturation"]=e.Saturation,["contrast"]=e.Contrast,["warmth"]=e.Warmth,["profileGpu"]=e.ProfileGraphicsGpu,["renderScale"]=e.RenderScale,
        ["shaderStyle"]=e.ShaderStyle.ToString(),["pixelSize"]=e.PixelSize,["pixelColorLevels"]=e.PixelColorLevels,["pixelDither"]=e.PixelDither };
    public static SkyEnvironment Load(SkyEnvironment e,JsonNode? node)
    {
        if(node==null) return e;
        // Preset labels are loaded without reapplying them over saved artist adjustments.
        e.RestorePresetLabels(Enum.TryParse<GraphicsQuality>(node["quality"]?.GetValue<string>(),out var q)?q:GraphicsQuality.Custom,
            Enum.TryParse<LightingPreset>(node["lighting"]?.GetValue<string>(),out var l)?l:LightingPreset.Custom);
        e.AmbientOcclusion=node["ao"]?.GetValue<float>()??0; e.OcclusionRadius=node["radius"]?.GetValue<float>()??.5f;
        e.Bloom=node["bloom"]?.GetValue<float>()??0; e.BloomThreshold=node["threshold"]?.GetValue<float>()??1.2f;
        e.Saturation=node["saturation"]?.GetValue<float>()??1; e.Contrast=node["contrast"]?.GetValue<float>()??1; e.Warmth=node["warmth"]?.GetValue<float>()??0;
        e.ProfileGraphicsGpu=node["profileGpu"]?.GetValue<bool>()??false; e.RenderScale=node["renderScale"]?.GetValue<float>()??1;
        e.ShaderStyle=Enum.TryParse<BuiltInSceneShader>(node["shaderStyle"]?.GetValue<string>(),out var style)?style:BuiltInSceneShader.Standard;
        e.PixelSize=node["pixelSize"]?.GetValue<int>()??4;e.PixelColorLevels=node["pixelColorLevels"]?.GetValue<int>()??8;e.PixelDither=node["pixelDither"]?.GetValue<float>()??.35f;return e;
    }
}
