using System.Diagnostics;
using System.Numerics;

using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class AnimationClipPreview : IDisposable
{
    /*
     * v0.11-C4A preview performance policy:
     *
     * - The Inspector image can scale to fit the panel, but the actual 3D
     *   framebuffer is intentionally fixed and small.
     * - Animation sampling + CPU skinning + GPU upload + 3D rendering are
     *   throttled to a preview-specific cadence instead of running once for
     *   every editor frame.
     * - While paused, the last rendered texture is simply reused.
     *
     * This prevents Inspector layout changes / scrollbars from causing
     * framebuffer recreation churn and avoids effectively rendering another
     * full-speed game viewport just to show a small asset preview.
     */
    private const int PreviewRenderWidth = 384;
    private const int PreviewRenderHeight = 256;

    private const double PreviewFramesPerSecond = 12.0;
    private const double PreviewFrameInterval =
        1.0 /
        PreviewFramesPerSecond;

    private const double MaximumPreviewStep =
        0.25;

    private readonly SceneFramebuffer _framebuffer = new();

    private readonly EditorCamera _camera2D = new();

    private readonly EditorCamera3D _camera3D = new();

    private readonly Stopwatch _previewClock =
        Stopwatch.StartNew();

    private Scene? _scene;

    private GameObject? _modelObject;

    private SkeletalMeshRenderer? _renderer;

    private Guid _modelGuid;

    private string _clipKey =
        string.Empty;

    private string? _error;

    private bool _paused;

    private bool _hasRenderedFrame;

    private bool _forceRender =
        true;

    private double _lastPreviewTickSeconds;

    public void Draw(
        EditorProjectContext project,
        AssetRecord asset,
        ModelAsset model,
        ImportedAnimation animation,
        Renderer2D renderer,
        Renderer3D renderer3D,
        int windowWidth,
        int windowHeight)
    {
        EnsurePreview(
            project,
            asset,
            model,
            animation);

        if (_renderer ==
                null ||
            _scene ==
                null)
        {
            ImGui.TextColored(
                new Vector4(
                    1.0f,
                    0.35f,
                    0.35f,
                    1.0f),
                "Preview unavailable.");

            if (!string.IsNullOrWhiteSpace(
                    _error))
            {
                ImGui.TextWrapped(
                    _error);
            }

            return;
        }

        double now =
            _previewClock.Elapsed
                .TotalSeconds;

        if (ImGui.SmallButton(
                _paused
                    ? "Play"
                    : "Pause"))
        {
            _paused =
                !_paused;

            if (_paused)
            {
                _renderer.Pause();
            }
            else
            {
                /*
                 * Resume from the current pose without counting time spent
                 * paused as animation time.
                 */
                _lastPreviewTickSeconds =
                    now;

                _renderer.Resume();
            }

            _forceRender =
                true;
        }

        ImGui.SameLine();

        if (ImGui.SmallButton(
                "Restart"))
        {
            _renderer.Play(
                animation.Name,
                true,
                0.0f);

            if (_paused)
            {
                _renderer.Pause();
            }

            _lastPreviewTickSeconds =
                now;

            _forceRender =
                true;
        }

        ImGui.SameLine();

        ImGui.TextDisabled(
            $"{_renderer.PlaybackTime:0.00} / {Math.Max(_renderer.Duration, animation.Duration):0.00} s");

        bool previewTickDue =
            _forceRender ||
            !_paused &&
            now -
            _lastPreviewTickSeconds >=
            PreviewFrameInterval;

        if (previewTickDue)
        {
            if (!_paused)
            {
                double elapsed =
                    Math.Clamp(
                        now -
                        _lastPreviewTickSeconds,
                        0.0,
                        MaximumPreviewStep);

                /*
                 * SkeletalMeshRenderer advances from ByteEngine.Time.DeltaTime.
                 * Because this preview deliberately updates less often than
                 * the editor, temporarily scale Speed so the clip still runs
                 * at real-time speed rather than slow motion.
                 */
                if (elapsed >
                    0.0001)
                {
                    double engineDelta =
                        Math.Max(
                            ByteEngine.Core.Time.DeltaTime,
                            1.0 /
                            1000.0);

                    float previewSpeed =
                        Math.Clamp(
                            (float)(
                                elapsed /
                                engineDelta),
                            0.01f,
                            30.0f);

                    _renderer.Speed =
                        previewSpeed;

                    _scene.UpdateInternal();

                    /*
                     * The preview owns this renderer, so returning it to a
                     * normal value after the throttled tick keeps diagnostic
                     * values sane and makes Restart/Resume predictable.
                     */
                    _renderer.Speed =
                        1.0f;
                }
            }

            _framebuffer.Render(
                renderer,
                renderer3D,
                _scene,
                EditorMode.Edit,
                _camera2D,
                _camera3D,
                true,
                PreviewRenderWidth,
                PreviewRenderHeight,
                windowWidth,
                windowHeight,
                drawGrid3D:
                    false);

            _hasRenderedFrame =
                true;

            _forceRender =
                false;

            _lastPreviewTickSeconds =
                now;
        }

        float duration =
            Math.Max(
                _renderer.Duration,
                animation.Duration);

        float progress =
            duration >
                0.00001f
                ? Math.Clamp(
                    _renderer.PlaybackTime /
                    duration,
                    0.0f,
                    1.0f)
                : 0.0f;

        ImGui.ProgressBar(
            progress,
            new Vector2(
                -1.0f,
                5.0f),
            string.Empty);

        ImGui.TextDisabled(
            "Preview 12 FPS - performance mode");

        float availableWidth =
            Math.Max(
                ImGui.GetContentRegionAvail().X,
                1.0f);

        float previewWidth =
            Math.Clamp(
                availableWidth,
                120.0f,
                420.0f);

        float previewHeight =
            previewWidth *
            PreviewRenderHeight /
            PreviewRenderWidth;

        float horizontalPadding =
            Math.Max(
                (
                    availableWidth -
                    previewWidth
                ) *
                0.5f,
                0.0f);

        if (horizontalPadding >
            0.0f)
        {
            ImGui.SetCursorPosX(
                ImGui.GetCursorPosX() +
                horizontalPadding);
        }

        if (_hasRenderedFrame)
        {
            ImGui.Image(
                _framebuffer.TextureId,
                new Vector2(
                    previewWidth,
                    previewHeight),
                new Vector2(
                    0.0f,
                    1.0f),
                new Vector2(
                    1.0f,
                    0.0f));
        }
        else
        {
            ImGui.Dummy(
                new Vector2(
                    previewWidth,
                    previewHeight));
        }
    }

    public void Reset()
    {
        DestroyPreview();

        _modelGuid =
            Guid.Empty;

        _clipKey =
            string.Empty;

        _error =
            null;

        _paused =
            false;

        _hasRenderedFrame =
            false;

        _forceRender =
            true;

        _lastPreviewTickSeconds =
            _previewClock.Elapsed
                .TotalSeconds;
    }

    public void Dispose()
    {
        DestroyPreview();
        _framebuffer.Dispose();
    }

    private void EnsurePreview(
        EditorProjectContext project,
        AssetRecord asset,
        ModelAsset model,
        ImportedAnimation animation)
    {
        if (_scene !=
                null &&
            _renderer !=
                null &&
            _modelGuid ==
                asset.Guid &&
            string.Equals(
                _clipKey,
                animation.Key,
                StringComparison.Ordinal))
        {
            return;
        }

        DestroyPreview();

        _modelGuid =
            asset.Guid;

        _clipKey =
            animation.Key;

        _paused =
            false;

        _error =
            null;

        _hasRenderedFrame =
            false;

        _forceRender =
            true;

        _lastPreviewTickSeconds =
            _previewClock.Elapsed
                .TotalSeconds;

        try
        {
            _scene =
                new Scene(
                    "Animation Clip Preview",
                    project.Project.Classification);

            _modelObject =
                _scene.CreateGameObject(
                    "Animated Model");

            CenterModelAndFrameCamera(
                model,
                _modelObject);

            _renderer =
                _modelObject.AddComponent(
                    new SkeletalMeshRenderer
                    {
                        Model =
                            new AssetReference(
                                asset.Guid,
                                asset.ProjectPath),

                        SkeletonKey =
                            model.Skeleton?.Key,

                        PlayOnStart =
                            false,

                        Loop =
                            true,

                        Speed =
                            1.0f,

                        TransitionDuration =
                            0.0f
                    });

            GameObject lightObject =
                _scene.CreateGameObject(
                    "Preview Light");

            lightObject.Transform.EulerAngles =
                new Vector3(
                    -35.0f,
                    -35.0f,
                    0.0f);

            lightObject.AddComponent(
                new DirectionalLight
                {
                    Intensity =
                        1.6f,

                    AmbientIntensity =
                        0.55f,

                    CastShadows =
                        false
                });

            _scene.LoadInternal();

            if (!_renderer.Play(
                    animation.Name,
                    true,
                    0.0f))
            {
                _error =
                    $"Animation '{animation.Name}' could not be played.";
            }
        }
        catch (Exception exception)
        {
            _error =
                exception.Message;

            DestroyPreview();
        }
    }

    private void DestroyPreview()
    {
        if (_scene !=
            null)
        {
            if (_scene.IsLoaded)
            {
                _scene.UnloadInternal();
            }

            foreach (GameObject gameObject
                     in _scene.GameObjects.ToArray())
            {
                if (gameObject.Parent ==
                    null)
                {
                    _scene.DestroyGameObject(
                        gameObject);
                }
            }
        }

        _renderer =
            null;

        _modelObject =
            null;

        _scene =
            null;

        _hasRenderedFrame =
            false;
    }

    private void CenterModelAndFrameCamera(
        ModelAsset model,
        GameObject modelObject)
    {
        if (!TryCalculateBounds(
                model,
                out Vector3 minimum,
                out Vector3 maximum))
        {
            minimum =
                new Vector3(
                    -0.5f,
                    0.0f,
                    -0.5f);

            maximum =
                new Vector3(
                    0.5f,
                    1.8f,
                    0.5f);
        }

        Vector3 center =
            (
                minimum +
                maximum
            ) *
            0.5f;

        Vector3 size =
            Vector3.Max(
                maximum -
                minimum,
                new Vector3(
                    0.1f));

        modelObject.Transform.LocalPosition =
            -center;

        _camera3D.Yaw =
            -135.0f;

        _camera3D.Pitch =
            -10.0f;

        _camera3D.FieldOfView =
            38.0f;

        float aspectRatio =
            (float)PreviewRenderWidth /
            PreviewRenderHeight;

        float verticalHalfFov =
            _camera3D.FieldOfView *
            MathF.PI /
            360.0f;

        float verticalTangent =
            MathF.Max(
                MathF.Tan(
                    verticalHalfFov),
                0.05f);

        float horizontalHalfFov =
            MathF.Atan(
                verticalTangent *
                aspectRatio);

        float horizontalTangent =
            MathF.Max(
                MathF.Tan(
                    horizontalHalfFov),
                0.05f);

        /*
         * Fit the actual visible model dimensions instead of the diagonal
         * bounding-sphere radius used by the first C4A preview. The diagonal
         * method placed tall character assets much farther away than needed.
         *
         * X/Z are combined because the preview camera views the model from a
         * diagonal yaw; Y controls the vertical fit.
         */
        float horizontalExtent =
            MathF.Sqrt(
                size.X *
                size.X +
                size.Z *
                size.Z) *
            0.5f;

        float verticalExtent =
            (
                size.Y +
                MathF.Max(
                    size.X,
                    size.Z) *
                0.12f
            ) *
            0.5f;

        float horizontalDistance =
            horizontalExtent /
            horizontalTangent;

        float verticalDistance =
            verticalExtent /
            verticalTangent;

        float distance =
            Math.Max(
                Math.Max(
                    horizontalDistance,
                    verticalDistance) *
                1.08f,
                0.75f);

        _camera3D.Position =
            -_camera3D.Forward *
            distance;
    }

    private static bool TryCalculateBounds(
        ModelAsset model,
        out Vector3 minimum,
        out Vector3 maximum)
    {
        minimum =
            new Vector3(
                float.MaxValue);

        maximum =
            new Vector3(
                float.MinValue);

        bool hasVertex =
            false;

        ImportedNode[] nodes =
            model.Nodes.ToArray();

        Dictionary<string, int> nodeByKey =
            nodes
                .Select(
                    (node, index) =>
                        (
                            node.Key,
                            index
                        ))
                .ToDictionary(
                    item =>
                        item.Key,
                    item =>
                        item.index,
                    StringComparer.Ordinal);

        Matrix4x4[] globals =
            new Matrix4x4[
                nodes.Length];

        byte[] states =
            new byte[
                nodes.Length];

        Matrix4x4 ResolveGlobal(
            int index)
        {
            if (states[index] ==
                2)
            {
                return globals[index];
            }

            if (states[index] ==
                1)
            {
                return nodes[index]
                    .LocalTransform;
            }

            states[index] =
                1;

            ImportedNode node =
                nodes[index];

            Matrix4x4 result =
                node.LocalTransform;

            if (node.ParentKey !=
                    null &&
                nodeByKey.TryGetValue(
                    node.ParentKey,
                    out int parentIndex))
            {
                result *=
                    ResolveGlobal(
                        parentIndex);
            }

            globals[index] =
                result;

            states[index] =
                2;

            return result;
        }

        Dictionary<string, ImportedMesh> meshes =
            model.Meshes.ToDictionary(
                mesh =>
                    mesh.Key,
                StringComparer.Ordinal);

        for (int nodeIndex =
                 0;
             nodeIndex <
             nodes.Length;
             nodeIndex++)
        {
            Matrix4x4 global =
                ResolveGlobal(
                    nodeIndex);

            foreach (string meshKey
                     in nodes[nodeIndex].MeshKeys)
            {
                if (!meshes.TryGetValue(
                        meshKey,
                        out ImportedMesh? mesh))
                {
                    continue;
                }

                float[] vertices =
                    mesh.Vertices;

                for (int offset =
                         0;
                     offset +
                         2 <
                     vertices.Length;
                     offset +=
                         8)
                {
                    Vector3 position =
                        Vector3.Transform(
                            new Vector3(
                                vertices[offset],
                                vertices[offset + 1],
                                vertices[offset + 2]),
                            global);

                    minimum =
                        Vector3.Min(
                            minimum,
                            position);

                    maximum =
                        Vector3.Max(
                            maximum,
                            position);

                    hasVertex =
                        true;
                }
            }
        }

        if (!hasVertex)
        {
            foreach (ImportedMesh mesh
                     in model.Meshes)
            {
                float[] vertices =
                    mesh.Vertices;

                for (int offset =
                         0;
                     offset +
                         2 <
                     vertices.Length;
                     offset +=
                         8)
                {
                    Vector3 position =
                        new(
                            vertices[offset],
                            vertices[offset + 1],
                            vertices[offset + 2]);

                    minimum =
                        Vector3.Min(
                            minimum,
                            position);

                    maximum =
                        Vector3.Max(
                            maximum,
                            position);

                    hasVertex =
                        true;
                }
            }
        }

        return hasVertex;
    }
}
