using System.Numerics;
using ByteEngine.Core.Graphics.ThreeD;

namespace ByteEngine.Core.Graphics;

public sealed class RenderContext
{
    public Renderer2D Renderer2D { get; }

    public Renderer3D Renderer3D { get; }

    public ByteEngine.Core.Scene.Scene Scene { get; }

    public int TargetWidth { get; }

    public int TargetHeight { get; }

    public Camera2D? Camera2D { get; }

    public Camera3D? Camera3D { get; }

    public Matrix4x4? ViewMatrix3D { get; }

    public Matrix4x4? ProjectionMatrix3D { get; }

    public RenderWorld RenderWorld { get; } =
        new();

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
        Matrix4x4? projectionMatrix3D = null)
    {
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
    }

    public Matrix4x4 GetViewMatrix3D()
    {
        return
            Camera3D?.GetViewMatrix() ??
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

    public RenderLighting3D CaptureRenderLighting3D(
        RenderView3D? view)
    {
        DirectionalLight[] directional =
            Scene.GameObjects
                .Where(
                    gameObject =>
                        gameObject.ActiveInHierarchy)
                .SelectMany(
                    gameObject =>
                        gameObject.Components
                            .OfType<DirectionalLight>())
                .Where(
                    light =>
                        light.Enabled)
                .ToArray();

        PointLight[] point =
            Scene.GameObjects
                .Where(
                    gameObject =>
                        gameObject.ActiveInHierarchy)
                .SelectMany(
                    gameObject =>
                        gameObject.Components
                            .OfType<PointLight>())
                .Where(
                    light =>
                        light.Enabled)
                .ToArray();

        if (directional.Length ==
                0 &&
            point.Length ==
                0)
        {
            return
                RenderLighting3D.Default;
        }

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
                            light.ShadowResolution,
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
                            light.CastShadows,
                            light.ShadowResolution,
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

        return
            new RenderLighting3D(
                directionalSnapshots,
                pointSnapshots,
                ambientIntensity,
                directional.Length,
                point.Length);
    }

    internal void Begin3DFrame()
    {
        RenderView3D? view =
            CaptureRenderView3D();

        RenderWorld.BeginFrame(
            view,
            CaptureRenderLighting3D(
                view));
    }

    internal void Flush3D()
    {
        RenderWorld.Execute(
            this);
    }
}
