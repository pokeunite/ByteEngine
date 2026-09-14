using System.Numerics;
using ByteEngine.Core.Graphics;

namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>
/// Collects 3D render submissions for one render pass.
/// </summary>
public sealed class RenderWorld
{
    private readonly List<RenderSubmission> _submissions =
        new();

    private RenderView3D? _view;

    private RenderLighting3D _lighting =
        RenderLighting3D.Default;

    private RenderEnvironment3D _environment =
        RenderEnvironment3D.Default;

    private int _nextSubmissionIndex;

    public int SubmissionCount =>
        _submissions.Count;

    public RenderView3D? View =>
        _view;

    public RenderLighting3D Lighting =>
        _lighting;

    public RenderEnvironment3D Environment =>
        _environment;

    public RenderWorldStats LastStats { get; private set; }

    public void BeginFrame(
        RenderView3D? view,
        RenderLighting3D lighting)
    {
        BeginFrame(
            view,
            lighting,
            RenderEnvironment3D.Default);
    }

    public void BeginFrame(
        RenderView3D? view,
        RenderLighting3D lighting,
        RenderEnvironment3D environment)
    {
        _submissions.Clear();

        _view =
            view;

        _lighting =
            lighting ??
            RenderLighting3D.Default;

        _environment =
            environment;

        _nextSubmissionIndex =
            0;

        LastStats =
            default;
    }

    public void Submit(
        Mesh mesh,
        Material material,
        Matrix4x4 modelMatrix,
        RenderQueue3D queue = RenderQueue3D.Opaque,
        bool frustumCulling = true,
        bool castShadows = true,
        bool receiveShadows = true)
    {
        ArgumentNullException.ThrowIfNull(
            mesh);

        ArgumentNullException.ThrowIfNull(
            material);

        BoundingBox3D worldBounds =
            mesh.LocalBounds.Transform(
                modelMatrix);

        float distanceSquared =
            0.0f;

        if (_view is
            RenderView3D view)
        {
            distanceSquared =
                Vector3.DistanceSquared(
                    worldBounds.Center,
                    view.CameraPosition);
        }

        _submissions.Add(
            new RenderSubmission(
                mesh,
                material,
                modelMatrix,
                worldBounds,
                queue,
                frustumCulling,
                castShadows,
                receiveShadows,
                distanceSquared,
                _nextSubmissionIndex));

        _nextSubmissionIndex++;
    }

    public void Execute(
        RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(
            context);

        int submitted =
            _submissions.Count;

        int opaqueSubmitted =
            CountQueue(
                RenderQueue3D.Opaque);

        int transparentSubmitted =
            CountQueue(
                RenderQueue3D.Transparent);

        int overlaySubmitted =
            CountQueue(
                RenderQueue3D.Overlay);

        if (_view is not
            RenderView3D view)
        {
            LastStats =
                CreateStats(
                    submitted,
                    0,
                    submitted,
                    0,
                    opaqueSubmitted,
                    transparentSubmitted,
                    overlaySubmitted,
                    0,
                    0,
                    0,
                    0);

            _submissions.Clear();

            return;
        }

        context.Renderer3D.PrepareEnvironmentLighting(
            _environment,
            view);

        int environmentDrawCalls =
            0;

        if (_environment.DrawSky)
        {
            SkyShader3D.CurrentEnvironment =
                _environment;

            try
            {
                context.Renderer3D.RenderSky(
                    view,
                    _environment);
            }
            finally
            {
                SkyShader3D.CurrentEnvironment =
                    RenderEnvironment3D.Default;
            }

            environmentDrawCalls =
                1;
        }

        if (submitted ==
            0)
        {
            LastStats =
                CreateStats(
                    submitted,
                    0,
                    0,
                    0,
                    opaqueSubmitted,
                    transparentSubmitted,
                    overlaySubmitted,
                    0,
                    0,
                    0,
                    environmentDrawCalls);

            return;
        }

        List<RenderSubmission> visible =
            new(
                submitted);

        int culled =
            0;

        foreach (RenderSubmission submission
                 in _submissions)
        {
            if (submission.FrustumCullingEnabled &&
                !view.Frustum.Intersects(
                    submission.WorldBounds))
            {
                culled++;

                continue;
            }

            visible.Add(
                submission);
        }

        DirectionalShadowPassResult shadowPass =
            visible.Count >
            0
                ? context.Renderer3D.RenderDirectionalShadowMap(
                    _submissions,
                    view,
                    _lighting)
                : DirectionalShadowPassResult.None;

        PointShadowPassResult pointShadowPass =
            visible.Count >
            0
                ? context.Renderer3D.RenderPointShadowMaps(
                    _submissions,
                    view,
                    _lighting)
                : PointShadowPassResult.None;

        IEnumerable<RenderSubmission> opaque =
            visible
                .Where(
                    submission =>
                        submission.Queue ==
                        RenderQueue3D.Opaque)
                .OrderBy(
                    submission =>
                        submission.DistanceSquaredToCamera)
                .ThenBy(
                    submission =>
                        submission.SubmissionIndex);

        IEnumerable<RenderSubmission> transparent =
            visible
                .Where(
                    submission =>
                        submission.Queue ==
                        RenderQueue3D.Transparent)
                .OrderByDescending(
                    submission =>
                        submission.DistanceSquaredToCamera)
                .ThenBy(
                    submission =>
                        submission.SubmissionIndex);

        IEnumerable<RenderSubmission> overlay =
            visible
                .Where(
                    submission =>
                        submission.Queue ==
                        RenderQueue3D.Overlay)
                .OrderBy(
                    submission =>
                        submission.SubmissionIndex);

        int drawCalls =
            0;

        foreach (RenderSubmission submission
                 in opaque
                     .Concat(
                         transparent)
                     .Concat(
                         overlay))
        {
            context.Renderer3D.Draw(
                submission.Mesh,
                submission.Material,
                submission.ModelMatrix,
                view.ViewMatrix,
                view.ProjectionMatrix,
                _lighting,
                shadowPass.Shadow,
                pointShadowPass.Shadows,
                submission.ReceiveShadows,
                submission.Queue ==
                    RenderQueue3D.Overlay
                    ? _environment with
                    {
                        FogEnabled = false
                    }
                    : _environment);

            drawCalls++;
        }

        LastStats =
            CreateStats(
                submitted,
                visible.Count,
                culled,
                drawCalls,
                opaqueSubmitted,
                transparentSubmitted,
                overlaySubmitted,
                shadowPass.Shadow.HasValue
                    ? 1
                    : 0,
                pointShadowPass.ShadowLightCount,
                shadowPass.DrawCalls +
                pointShadowPass.DrawCalls,
                environmentDrawCalls);

        _submissions.Clear();
    }

    private RenderWorldStats CreateStats(
        int submitted,
        int visible,
        int culled,
        int drawCalls,
        int opaqueSubmitted,
        int transparentSubmitted,
        int overlaySubmitted,
        int directionalShadowPasses,
        int pointShadowPasses,
        int shadowDrawCalls,
        int environmentDrawCalls)
    {
        return
            new RenderWorldStats(
                submitted,
                visible,
                culled,
                drawCalls,
                opaqueSubmitted,
                transparentSubmitted,
                overlaySubmitted,
                _lighting.DirectionalLightCount,
                _lighting.PointLightCount,
                _lighting.DroppedLightCount,
                directionalShadowPasses,
                pointShadowPasses,
                directionalShadowPasses +
                pointShadowPasses,
                shadowDrawCalls,
                environmentDrawCalls);
    }

    private int CountQueue(
        RenderQueue3D queue)
    {
        int count =
            0;

        foreach (RenderSubmission submission
                 in _submissions)
        {
            if (submission.Queue ==
                queue)
            {
                count++;
            }
        }

        return count;
    }
}

public readonly record struct RenderWorldStats(
    int SubmittedMeshes,
    int VisibleMeshes,
    int CulledMeshes,
    int DrawCalls,
    int OpaqueSubmissions,
    int TransparentSubmissions,
    int OverlaySubmissions,
    int DirectionalLights,
    int PointLights,
    int DroppedLights,
    int DirectionalShadowPasses,
    int PointShadowPasses,
    int ShadowPasses,
    int ShadowDrawCalls,
    int EnvironmentDrawCalls)
{
    /// <summary>
    /// Number of depth-map faces rendered this frame:
    /// one per directional shadow plus six per point-light cubemap.
    /// </summary>
    public int ShadowMapFaces =>
        DirectionalShadowPasses +
        PointShadowPasses *
        6;

    /// <summary>
    /// Main-pass and shadow-pass draw calls combined.
    /// </summary>
    public int TotalDrawCalls =>
        DrawCalls +
        ShadowDrawCalls +
        EnvironmentDrawCalls;
}
