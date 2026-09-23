using System.Diagnostics;
using System.Numerics;

using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Animation;
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
    private const int PreviewRenderWidth = 512;
    private const int PreviewRenderHeight = 320;

    /*
     * 30 FPS is smooth enough for animation inspection while remaining
     * deliberately below the main editor viewport refresh rate.
     */
    private const double PreviewFramesPerSecond = 30.0;
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

    private readonly ModelImporterInspector _importerInspector =
        new();

    private Scene? _scene;

    private GameObject? _modelObject;

    private SkeletalMeshRenderer? _renderer;

    private Guid _modelGuid;
    private Guid _previewModelGuid;
    private Guid _selectionSourceGuid;
    private readonly Guid _previewInstanceGuid = Guid.NewGuid();
    private AssetReference _previewModelReference = AssetReference.Empty;
    private string _previewModelSearch = string.Empty;
    private ModelAsset? _temporaryClipOwner;
    private string _temporaryClipKey = string.Empty;

    private string _clipKey =
        string.Empty;

    private string _animationName =
        string.Empty;

    private float _animationDuration;

    private string? _error;

    private bool _paused;

    private bool _hasRenderedFrame;

    private bool _forceRender =
        true;

    private Vector3 _cameraTarget =
        Vector3.Zero;

    private float _cameraDistance =
        2.0f;

    private double _lastPreviewTickSeconds;

    public float PlaybackTime =>
        _renderer?.PlaybackTime ?? 0.0f;

    public float Duration =>
        _renderer?.Duration ?? 0.0f;

    public bool IsPaused =>
        _paused;

    /// <summary>
    /// Direct preview sampling used by animation authoring. This never routes
    /// through AnimationController, so scrubbing cannot emit gameplay events or
    /// window transitions.
    /// </summary>
    public bool Seek(float time)
    {
        if (_renderer == null || !_renderer.Seek(time))
        {
            return false;
        }

        _lastPreviewTickSeconds =
            _previewClock.Elapsed.TotalSeconds;
        _forceRender = true;
        return true;
    }

    public void DrawPlaybackControls()
    {
        bool available = _renderer != null;
        ImGui.BeginDisabled(!available);
        if (EditorUi.ToolbarButton(_paused ? "Play" : "Pause", _paused ? "Resume preview" : "Pause preview"))
        {
            _paused = !_paused;
            if (_paused)
                _renderer?.Pause();
            else
            {
                _lastPreviewTickSeconds = _previewClock.Elapsed.TotalSeconds;
                _renderer?.Resume();
            }
            _forceRender = true;
        }

        ImGui.SameLine();
        if (EditorUi.ToolbarButton("Restart", "Restart from the beginning") && _renderer != null)
        {
            _renderer.Play(_animationName, true, 0.0f);
            if (_paused) _renderer.Pause();
            _lastPreviewTickSeconds = _previewClock.Elapsed.TotalSeconds;
            _forceRender = true;
        }
        ImGui.EndDisabled();

        ImGui.SameLine();
        float duration = Math.Max(_renderer?.Duration ?? 0.0f, _animationDuration);
        ImGui.TextColored(EditorTheme.TextSecondary,
            $"{(_renderer?.PlaybackTime ?? 0.0f):0.000} / {duration:0.000} s");
    }
    public void Draw(
        EditorProjectContext project,
        AssetRecord asset,
        ModelAsset model,
        ImportedAnimation animation,
        Renderer2D renderer,
        Renderer3D renderer3D,
        int windowWidth,
        int windowHeight,
        bool showPlaybackControls = false)
    {
        /*
         * Imported animation assets finally have a real importer surface rather
         * than inheriting an invisible Generic default. Keep it beside the clip
         * preview so selecting an animation in the Asset Browser is enough to
         * configure Model/Rig/Animation settings and reimport immediately.
         */
        bool reimported =
            _importerInspector.Draw(
                project,
                asset,
                model,
                animation);

        ImGui.Spacing();

        if (reimported)
        {
            Reset();

            ImGui.TextDisabled(
                "Asset reimported. The preview will refresh from the new import on the next editor frame.");

            return;
        }

        if (_selectionSourceGuid != asset.Guid)
        {
            Reset();
            _selectionSourceGuid = asset.Guid;
            _previewModelReference = model.Meshes.Count > 0
                ? new AssetReference(asset.Guid, asset.ProjectPath)
                : model.DefaultRetargetTargetModel;
        }

        DrawPreviewModelPicker(project, model);
        if (_previewModelReference.IsEmpty)
        {
            ImGui.TextDisabled("Choose a skinned Humanoid model to preview this animation.");
            return;
        }

        EnsurePreview(project, asset, model, animation);

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

        if (showPlaybackControls)
        {
            EditorUi.BeginToolbar("##InspectorClipPlayback");
            DrawPlaybackControls();
            EditorUi.EndToolbar();
            float duration = Math.Max(_renderer.Duration, _animationDuration);
            if (duration > 0.0001f)
            {
                float playhead = _renderer.PlaybackTime;
                if (ImGui.SliderFloat("##InspectorClipPlayhead", ref playhead,
                    0.0f, duration, "%.3f s"))
                    Seek(playhead);
            }
        }
        double now =
            _previewClock.Elapsed.TotalSeconds;

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
                    false,
                prepareEnvironmentLighting3D:
                    false,
                renderShadows3D:
                    false);

            _hasRenderedFrame =
                true;

            _forceRender =
                false;

            _lastPreviewTickSeconds =
                now;
        }

        EditorUi.BeginToolbar("##AnimationPreviewToolbar");

        if (EditorUi.ToolbarButton(
                "Reset View",
                "Frame the current pose") &&
            _renderer !=
                null)
        {
            FrameCurrentPose(
                _renderer);

            _forceRender =
                true;
        }

        ImGui.SameLine();

        ImGui.TextColored(EditorTheme.TextMuted,
            "LMB Orbit   MMB Pan   Wheel Zoom   F Frame");

        EditorUi.EndToolbar();

        Vector2 availableRegion =
            ImGui.GetContentRegionAvail();

        float availableWidth =
            Math.Max(
                availableRegion.X,
                1.0f);

        /*
         * Leave enough vertical space below the preview for the playhead
         * scrubber, timeline header/buttons and event/window lanes.
         *
         * This makes the preview expand vertically with the native asset
         * window instead of being limited only by an arbitrary width cap.
         */
        const float ReservedTimelineHeight =
            205.0f;

        float previewHeightBudget =
            Math.Max(
                availableRegion.Y -
                ReservedTimelineHeight,
                180.0f);

        float previewWidthFromHeight =
            previewHeightBudget *
            PreviewRenderWidth /
            PreviewRenderHeight;

        float previewWidth =
            Math.Clamp(
                Math.Min(
                    availableWidth,
                    previewWidthFromHeight),
                240.0f,
                980.0f);

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

        Vector2 imageMinimum = ImGui.GetCursorScreenPos();
        Vector2 imageMaximum = imageMinimum + new Vector2(previewWidth, previewHeight);
        ImDrawListPtr drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(imageMinimum, imageMaximum,
            ImGui.GetColorU32(EditorTheme.BackgroundRaised), EditorTheme.SmallCornerRadius);
        drawList.AddRect(imageMinimum, imageMaximum, ImGui.GetColorU32(EditorTheme.Border),
            EditorTheme.SmallCornerRadius);

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

        HandlePreviewCameraInput();
    }

    public void Reset()
    {
        DestroyPreview();

        _modelGuid =
            Guid.Empty;
        _previewModelGuid = Guid.Empty;

        _clipKey =
            string.Empty;

        _animationName =
            string.Empty;

        _animationDuration =
            0.0f;

        _error =
            null;

        _paused =
            false;

        _hasRenderedFrame =
            false;

        _forceRender =
            true;

        _cameraTarget =
            Vector3.Zero;

        _cameraDistance =
            2.0f;

        _lastPreviewTickSeconds =
            _previewClock.Elapsed
                .TotalSeconds;
    }

    public void Dispose()
    {
        DestroyPreview();
        _framebuffer.Dispose();
    }

    private void DrawPreviewModelPicker(EditorProjectContext project, ModelAsset source)
    {
        AssetRecord? current = _previewModelReference.IsEmpty
            ? null : project.AssetDatabase.Resolve(_previewModelReference);
        string label = current?.ProjectPath ?? "Choose model...";
        if (ImGui.BeginCombo("Preview Model", label))
        {
            ImGui.InputTextWithHint("##PreviewModelSearch", "Search models...",
                ref _previewModelSearch, 96);
            foreach (AssetRecord record in project.AssetDatabase.Assets
                .Where(item => item.Type == AssetType.Model3D)
                .OrderBy(item => item.ProjectPath, StringComparer.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(_previewModelSearch) &&
                    !record.ProjectPath.Contains(_previewModelSearch,
                        StringComparison.OrdinalIgnoreCase)) continue;
                bool selected = record.Guid == current?.Guid;
                if (ImGui.Selectable(record.ProjectPath, selected))
                {
                    Reset();
                    _previewModelReference = new AssetReference(record.Guid, record.ProjectPath);
                }
                if (selected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        if (source.Meshes.Count == 0)
            ImGui.TextDisabled("Preview only: the chosen model and source asset are not modified.");
        if (!source.AnimationSourceRigModel.IsEmpty &&
            source.RigType == AnimationRigType.Humanoid &&
            source.ReferenceHumanoidPose?.IsReady == true)
            ImGui.TextWrapped("This clip already has a usable skeleton. Preview uses it; clear the assigned Source Model in Import Settings before baking if you want the bake to match.");
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
            _previewModelGuid == _previewModelReference.Guid &&
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
        _previewModelGuid = _previewModelReference.Guid;

        _clipKey =
            animation.Key;

        _animationName =
            animation.Name;

        _animationDuration =
            animation.Duration;

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
            AssetRecord previewAsset = project.AssetDatabase.Resolve(_previewModelReference)
                ?? throw new InvalidOperationException("Preview Model could not be found.");
            AssetReference previewReference = new(previewAsset.Guid, previewAsset.ProjectPath);
            ModelAsset previewModel = project.Assets.LoadModel(previewReference);
            if (previewModel.Meshes.Count == 0)
                throw new InvalidOperationException("Preview Model has no render mesh. Choose a character model.");
            if (previewModel.Skeleton == null)
                throw new InvalidOperationException("Preview Model has no skeleton.");

            if (previewAsset.Guid != asset.Guid)
            {
                // The source's own valid Humanoid hierarchy must win over an
                // old optional Source Rig override; otherwise FBX helper-node
                // rotations can be discarded before retargeting.
                AssetReference sourceRig = model.RigType == AnimationRigType.Humanoid &&
                    model.ReferenceHumanoidPose?.IsReady == true
                    ? new AssetReference(asset.Guid, asset.ProjectPath)
                    : AssetReference.Empty;
                ImportedAnimation generated = HumanoidRetargetRuntime.BuildClip(
                    project.Assets, new AssetReference(asset.Guid, asset.ProjectPath), sourceRig,
                    animation.Name, previewReference, animation.Name,
                    model.RetargetSamplesPerSecond);
                ImportedAnimation temporary = new()
                {
                    Key = $"inspector-preview:{_previewInstanceGuid:N}:{animation.Key}:{previewAsset.Guid:N}",
                    Name = $"__Preview_{_previewInstanceGuid:N}",
                    Duration = generated.Duration,
                    Channels = generated.Channels
                };
                previewModel.RegisterRuntimeAnimation(temporary);
                _temporaryClipOwner = previewModel;
                _temporaryClipKey = temporary.Key;
                _animationName = temporary.Name;
            }

            _scene =
                new Scene(
                    "Animation Clip Preview",
                    project.Project.Classification);

            _modelObject =
                _scene.CreateGameObject(
                    "Animated Model");

            _renderer =
                _modelObject.AddComponent(
                    new SkeletalMeshRenderer
                    {
                        Model = previewReference,

                        SkeletonKey = previewModel.Skeleton.Key,

                        PlayOnStart =
                            false,

                        Loop =
                            true,

                        Speed =
                            1.0f,

                        TransitionDuration =
                            0.0f
                    });

            /*
             * Unity-style preview philosophy: a cheap key/fill setup instead
             * of the game's HDR environment or shadow stack.
             */
            GameObject keyLightObject =
                _scene.CreateGameObject(
                    "Preview Key Light");

            keyLightObject.Transform.EulerAngles =
                new Vector3(
                    -35.0f,
                    -35.0f,
                    0.0f);

            keyLightObject.AddComponent(
                new DirectionalLight
                {
                    Intensity =
                        1.25f,

                    AmbientIntensity =
                        0.22f,

                    CastShadows =
                        false
                });

            GameObject fillLightObject =
                _scene.CreateGameObject(
                    "Preview Fill Light");

            fillLightObject.Transform.EulerAngles =
                new Vector3(
                    20.0f,
                    145.0f,
                    0.0f);

            fillLightObject.AddComponent(
                new DirectionalLight
                {
                    Intensity =
                        0.55f,

                    AmbientIntensity =
                        0.12f,

                    CastShadows =
                        false
                });

            _scene.LoadInternal();

            if (!_renderer.Play(
                    _animationName,
                    true,
                    0.0f))
            {
                _error =
                    $"Animation '{animation.Name}' could not be played.";
            }
            else
            {
                /*
                 * Frame the mesh that is actually visible after skinning.
                 * Raw FBX hierarchy bounds can contain conversion/armature
                 * transforms far outside the character and were the reason
                 * the Gorilla appeared as a tiny sliver in the old preview.
                 */
                FrameCurrentPose(
                    _renderer);
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

        if (_temporaryClipOwner != null && _temporaryClipKey.Length > 0)
            _temporaryClipOwner.RemoveRuntimeAnimation(_temporaryClipKey);
        _temporaryClipOwner = null;
        _temporaryClipKey = string.Empty;

        _renderer =
            null;

        _modelObject =
            null;

        _scene =
            null;

        _hasRenderedFrame =
            false;
    }

    private void FrameCurrentPose(
        SkeletalMeshRenderer renderer)
    {
        Vector3 minimum;
        Vector3 maximum;

        if (!renderer.TryGetCurrentModelBounds(
                out BoundingBox3D bounds) ||
            !bounds.IsValid)
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
        else
        {
            minimum =
                bounds.Minimum;

            maximum =
                bounds.Maximum;
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
                    0.05f));

        _camera3D.Yaw =
            -135.0f;

        _camera3D.Pitch =
            -8.0f;

        _camera3D.FieldOfView =
            30.0f;

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
         * Camera is diagonal by default, so X/Z both contribute to screen
         * width. The 0.90 factor intentionally fills most of the preview while
         * retaining a small safety margin for limb motion.
         */
        float horizontalExtent =
            MathF.Sqrt(
                size.X *
                size.X +
                size.Z *
                size.Z) *
            0.5f;

        float verticalExtent =
            size.Y *
            0.5f;

        float horizontalDistance =
            horizontalExtent /
            horizontalTangent;

        float verticalDistance =
            verticalExtent /
            verticalTangent;

        _cameraTarget =
            center;

        _cameraDistance =
            Math.Max(
                Math.Max(
                    horizontalDistance,
                    verticalDistance) *
                0.90f,
                0.20f);

        UpdatePreviewCameraPosition();
    }

    private void HandlePreviewCameraInput()
    {
        if (!ImGui.IsItemHovered())
        {
            return;
        }

        ImGuiIOPtr io =
            ImGui.GetIO();

        bool changed =
            false;

        if (ImGui.IsKeyPressed(
                ImGuiKey.F) &&
            _renderer !=
                null)
        {
            FrameCurrentPose(
                _renderer);

            changed =
                true;
        }

        if (ImGui.IsMouseDragging(
                ImGuiMouseButton.Left))
        {
            Vector2 delta =
                io.MouseDelta;

            _camera3D.Yaw +=
                delta.X *
                0.30f;

            _camera3D.Pitch =
                Math.Clamp(
                    _camera3D.Pitch -
                    delta.Y *
                    0.30f,
                    -85.0f,
                    85.0f);

            changed =
                true;
        }

        if (ImGui.IsMouseDragging(
                ImGuiMouseButton.Middle))
        {
            Vector2 delta =
                io.MouseDelta;

            Vector3 cameraUp =
                Vector3.Normalize(
                    Vector3.Cross(
                        _camera3D.Right,
                        _camera3D.Forward));

            float panScale =
                Math.Max(
                    _cameraDistance *
                    0.0025f,
                    0.0005f);

            _cameraTarget +=
                (
                    -_camera3D.Right *
                    delta.X +
                    cameraUp *
                    delta.Y
                ) *
                panScale;

            changed =
                true;
        }

        if (MathF.Abs(
                io.MouseWheel) >
            0.0001f)
        {
            float zoomFactor =
                MathF.Pow(
                    0.88f,
                    io.MouseWheel);

            _cameraDistance =
                Math.Clamp(
                    _cameraDistance *
                    zoomFactor,
                    0.05f,
                    500.0f);

            changed =
                true;
        }

        if (!changed)
        {
            return;
        }

        UpdatePreviewCameraPosition();

        _forceRender =
            true;
    }

    private void UpdatePreviewCameraPosition()
    {
        _camera3D.Position =
            _cameraTarget -
            _camera3D.Forward *
            _cameraDistance;
    }
}
