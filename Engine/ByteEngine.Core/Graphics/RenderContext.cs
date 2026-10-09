using System.Numerics;
using ByteEngine.Core.Graphics.ThreeD;

namespace ByteEngine.Core.Graphics;

public sealed class RenderContext
{
    /// <summary>Optional portable backend. Null keeps the existing desktop path.</summary>
    public string ViewportName {get;set;}="Game";
    public IRenderFrameSink? FrameSink { get; }
    public Renderer2D Renderer2D { get; }

    public Renderer3D Renderer3D { get; }

    public ByteEngine.Core.Scene.Scene Scene { get; }

    public int TargetWidth { get; }

    public int TargetHeight { get; }

    public Camera2D? Camera2D { get; }

    public Camera3D? Camera3D { get; }

    public Matrix4x4? ViewMatrix3D { get; }

    public Matrix4x4? ProjectionMatrix3D { get; }

    /// <summary>
    /// Controls whether this render pass may prepare, replace or clear the
    /// shared Renderer3D environment-lighting cache. Main Scene/Game renders
    /// keep the default true value. Auxiliary asset previews can disable it so
    /// a preview without a SkyEnvironment cannot invalidate the main view's
    /// already-prepared IBL resources.
    /// </summary>
    public bool PrepareEnvironmentLighting3D { get; }

    /// <summary>
    /// Controls whether this pass may render directional or point-light shadow
    /// maps. Main Scene/Game views keep the default true value. Small editor
    /// previews disable shadows because they add extra render passes without
    /// helping the user identify an animation clip.
    /// </summary>
    public bool RenderShadows3D { get; }

    public RenderWorld RenderWorld { get; }

    private readonly List<Action<Renderer2D>> _uiCommands = new();

    public Vector2 MeasureText(string text,string? fontPath,int size) => FrameSink?.MeasureText(text,fontPath,size) ?? Renderer2D.MeasureText(text,fontPath,size);
    internal void QueueUiClip(Vector4? rectangle)
    {
        if(FrameSink!=null)FrameSink.SetUiClip(rectangle);
        else _uiCommands.Add(renderer=>renderer.SetUiClip(rectangle));
    }

    internal void QueueUiText(string value, string? fontPath, int size, Vector2 position, Vector4 color, float wrapWidth, UiAnchor anchor)
    {
        if (FrameSink != null) FrameSink.DrawText(value, fontPath, size, position, color, wrapWidth, anchor);
        else _uiCommands.Add(renderer => renderer.DrawText(value, fontPath, size, position, color, wrapWidth, anchor));
    }

    internal void QueueUiQuad(Vector2 position, Vector2 size, Vector4 color)
    {
        if (FrameSink != null) FrameSink.DrawQuad(position, size, color);
        else _uiCommands.Add(renderer => renderer.DrawQuad(position + size * .5f, size, color));
    }

    internal void QueueUiImage(Texture2D texture, Vector2 position, Vector2 size, Vector4 color)
    {
        if (FrameSink != null) FrameSink.DrawImage(texture, position, size, color);
        else _uiCommands.Add(renderer => renderer.DrawSprite(texture, position + size * .5f, size, 0f, color));
    }

    internal void FlushUi()
    {
        if (_uiCommands.Count == 0) return;
        using var timing=Renderer3D.ProfilePass(this,"UI");
        Renderer2D.BeginUi();
        try {foreach (Action<Renderer2D> command in _uiCommands) command(Renderer2D);}
        finally {Renderer2D.SetUiClip(null);_uiCommands.Clear();}
    }

    public bool Has3DCamera =>
        Camera3D !=
        null ||
        (
            ViewMatrix3D.HasValue &&
            ProjectionMatrix3D.HasValue
        );

    public float AspectRatio =>
        (float)TargetWidth /
        Math.Max(
            TargetHeight,
            1);

    public RenderContext(
        Renderer2D renderer2D,
        Renderer3D renderer3D,
        ByteEngine.Core.Scene.Scene scene,
        int targetWidth,
        int targetHeight,
        Camera2D? camera2D = null,
        Camera3D? camera3D = null,
        Matrix4x4? viewMatrix3D = null,
        Matrix4x4? projectionMatrix3D = null,
        bool prepareEnvironmentLighting3D = true,
        bool renderShadows3D = true,
        IRenderFrameSink? frameSink = null,
        RenderWorld? renderWorld = null)
    {
        RenderWorld = renderWorld ?? new();
        FrameSink = frameSink;
        Renderer2D =
            renderer2D;

        Renderer3D =
            renderer3D;

        Scene =
            scene;

        TargetWidth =
            Math.Max(
                targetWidth,
                1);

        TargetHeight =
            Math.Max(
                targetHeight,
                1);

        Camera2D =
            camera2D;

        Camera3D =
            camera3D;

        ViewMatrix3D =
            viewMatrix3D;

        ProjectionMatrix3D =
            projectionMatrix3D;

        PrepareEnvironmentLighting3D =
            prepareEnvironmentLighting3D;

        RenderShadows3D =
            renderShadows3D;
    }

    public Matrix4x4 GetViewMatrix3D()
    {
        return
            Camera3D?.GetRenderViewMatrix() ??
            ViewMatrix3D ??
            Matrix4x4.Identity;
    }

    public Matrix4x4 GetProjectionMatrix3D()
    {
        return
            Camera3D?.GetProjectionMatrix(
                AspectRatio) ??
            ProjectionMatrix3D ??
            Matrix4x4.Identity;
    }

    public RenderView3D? CaptureRenderView3D()
    {
        if (!Has3DCamera)
        {
            return null;
        }

        Matrix4x4 view =
            GetViewMatrix3D();

        Matrix4x4 projection =
            GetProjectionMatrix3D();

        Vector3 cameraPosition =
            Vector3.Zero;

        if (Matrix4x4.Invert(
                view,
                out Matrix4x4 inverseView))
        {
            cameraPosition =
                inverseView.Translation;
        }

        return
            new RenderView3D(
                view,
                projection,
                cameraPosition,
                TargetWidth,
                TargetHeight);
    }

    public RenderEnvironment3D CaptureRenderEnvironment3D()
    {
        SkyEnvironment? environment =
            Scene.GameObjects
                .Where(
                    gameObject =>
                        gameObject.ActiveInHierarchy)
                .SelectMany(
                    gameObject =>
                        gameObject.Components
                            .OfType<SkyEnvironment>())
                .FirstOrDefault(
                    component =>
                        component.Enabled);

        if (environment ==
            null)
        {
            return
                RenderEnvironment3D.Default;
        }

        return
            new RenderEnvironment3D(
                environment.DrawSky,
                environment.SkyMode,
                environment.EnvironmentMapTexture,
                environment.EnvironmentIntensity,
                environment.EnvironmentRotationDegrees,
                environment.EnvironmentLightingEnabled,
                environment.Exposure,
                environment.ZenithColor,
                environment.HorizonColor,
                environment.GroundColor,
                environment.SkyIntensity,
                environment.HorizonSharpness,
                environment.OverrideAmbient,
                environment.AmbientIntensity,
                environment.FogEnabled,
                environment.FogMode,
                environment.FogColor,
                environment.FogStartDistance,
                environment.FogEndDistance,
                environment.FogDensity,
                environment.FogMaxOpacity) { SmoothEdges = environment.SmoothEdges, Look=environment.CaptureLook() };
    }

    public RenderLighting3D CaptureRenderLighting3D(
        RenderView3D? view)
    {
        return
            CaptureRenderLighting3D(
                view,
                CaptureRenderEnvironment3D());
    }

    private RenderLighting3D CaptureRenderLighting3D(
        RenderView3D? view,
        RenderEnvironment3D environment)
    {
        var directionalList=new List<DirectionalLight>();var pointList=new List<PointLight>();
        var objects=Scene.GameObjects;
        for(int i=0;i<objects.Count;i++){
            var obj=objects[i];if(!obj.ActiveInHierarchy)continue;var components=obj.Components;
            for(int j=0;j<components.Count;j++){var component=components[j];if(!component.Enabled)continue;if(component is DirectionalLight sun)directionalList.Add(sun);else if(component is PointLight lamp)pointList.Add(lamp);}
        }
        var directional=directionalList.ToArray();var point=pointList.ToArray();

        RenderDirectionalLight3D[] directionalSnapshots =
            directional
                .Take(
                    RenderLighting3D.MaxDirectionalLights)
                .Select(
                    light =>
                        new RenderDirectionalLight3D(
                            light.Direction,
                            light.Color,
                            Math.Max(
                                0.0f,
                                light.Intensity),
                            Math.Max(
                                0.0f,
                                light.AmbientIntensity),
                            light.CastShadows,
                            Math.Min(light.ShadowResolution,environment.Look.ShadowResolutionCap),
                            light.ShadowDistance,
                            light.ShadowBias,
                            light.ShadowStrength,
                            light.ShadowSoftness))
                .ToArray();

        Vector3 cameraPosition =
            view?.CameraPosition ??
            Vector3.Zero;

        RenderPointLight3D[] pointSnapshots =
            point
                .OrderBy(
                    light =>
                        Vector3.DistanceSquared(
                            light.Position,
                            cameraPosition))
                .Take(
                    RenderLighting3D.MaxPointLights)
                .Select(
                    light =>
                        new RenderPointLight3D(
                            light.Position,
                            light.Color,
                            Math.Max(
                                0.0f,
                                light.Intensity),
                            Math.Max(
                                0.05f,
                                light.Range),
                            light.CastShadows && environment.Look.PointShadowLimit>0,
                            Math.Min(light.ShadowResolution,environment.Look.ShadowResolutionCap),
                            light.ShadowBias,
                            light.ShadowStrength,
                            light.ShadowSoftness))
                .ToArray();

        float ambientIntensity =
            directionalSnapshots.Length >
            0
                ? Math.Clamp(
                    directionalSnapshots.Sum(
                        light =>
                            light.AmbientIntensity),
                    0.0f,
                    4.0f)
                : 0.05f;

        RenderLighting3D lighting =
            directional.Length ==
                    0 &&
                point.Length ==
                    0
                ? RenderLighting3D.Default
                : new RenderLighting3D(
                    directionalSnapshots,
                    pointSnapshots,
                    ambientIntensity,
                    directional.Length,
                    point.Length);

        if (!environment.OverrideAmbient)
        {
            lighting.PointShadowLimit=environment.Look.PointShadowLimit;
            return lighting;
        }

        return
            new RenderLighting3D(
                lighting.DirectionalLights,
                lighting.PointLights,
                environment.AmbientIntensity,
                lighting.SubmittedDirectionalLights,
                lighting.SubmittedPointLights) { PointShadowLimit=environment.Look.PointShadowLimit };
    }

    internal void Begin3DFrame()
    {
        RenderView3D? view =
            CaptureRenderView3D();

        RenderEnvironment3D environment =
            CaptureRenderEnvironment3D();

        RenderWorld.BeginFrame(
            view,
            CaptureRenderLighting3D(
                view,
                environment),
            environment);
    }

    internal void Flush3D()
    {
        if(FrameSink!=null){RenderWorld.Execute(this);return;}
        Renderer3D.BeginGeometryProfile(RenderWorld.Environment.Look.ProfileGpu);
        try { RenderWorld.Execute(this); } finally { Renderer3D.EndGeometryProfile(); }
    }
}
