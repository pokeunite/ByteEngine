using System.Numerics;
using ByteEngine.Core.Graphics;

namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>
/// Collects 3D render submissions for one render pass.
///
/// v0.9-c responsibilities:
/// - immutable view snapshot
/// - immutable multi-light snapshot
/// - render queues
/// - world-space render bounds
/// - frustum culling
/// - opaque/transparent distance sorting
/// - render/light statistics
/// </summary>
public sealed class RenderWorld
{
    private readonly List<RenderSubmission> _submissions =
        new();

    private RenderView3D? _view;

    private RenderLighting3D _lighting =
        RenderLighting3D.Default;

    private int _nextSubmissionIndex;

    public int SubmissionCount =>
        _submissions.Count;

    public RenderView3D? View =>
        _view;

    public RenderLighting3D Lighting =>
        _lighting;

    public RenderWorldStats LastStats { get; private set; }

    public void BeginFrame(
        RenderView3D? view,
        RenderLighting3D lighting)
    {
        _submissions.Clear();

        _view =
            view;

        _lighting =
            lighting ??
            RenderLighting3D.Default;

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
        bool frustumCulling = true)
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

        if (submitted ==
            0)
        {
            LastStats =
                new RenderWorldStats(
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    _lighting.DirectionalLightCount,
                    _lighting.PointLightCount,
                    _lighting.DroppedLightCount);

            return;
        }

        if (_view is not
            RenderView3D view)
        {
            LastStats =
                new RenderWorldStats(
                    submitted,
                    0,
                    submitted,
                    0,
                    opaqueSubmitted,
                    transparentSubmitted,
                    overlaySubmitted,
                    _lighting.DirectionalLightCount,
                    _lighting.PointLightCount,
                    _lighting.DroppedLightCount);

            _submissions.Clear();

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
                _lighting);

            drawCalls++;
        }

        LastStats =
            new RenderWorldStats(
                submitted,
                visible.Count,
                culled,
                drawCalls,
                opaqueSubmitted,
                transparentSubmitted,
                overlaySubmitted,
                _lighting.DirectionalLightCount,
                _lighting.PointLightCount,
                _lighting.DroppedLightCount);

        _submissions.Clear();
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
    int DroppedLights);
