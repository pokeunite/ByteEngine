using System.Numerics;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Editor.Gizmos;
using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class SkeletalSocketPreview : IDisposable
{
    private const int PreviewRenderWidth = 640;
    private const int PreviewRenderHeight = 480;

    private readonly SceneFramebuffer _framebuffer = new();
    private readonly EditorCamera _camera2D = new();
    private readonly EditorCamera3D _camera = new();
    private readonly BlueprintTransformGizmo3D _gizmo = new();

    private Scene? _scene;
    private SkeletalMeshRenderer? _renderer;
    private GameObject? _gizmoProxy;
    private GameObject? _attachment;

    private Guid _modelGuid;
    private Guid? _previewGuid;
    private Vector3 _target = Vector3.Zero;
    private float _distance = 2.5f;
    private float _modelFrameDistance = 2.5f;
    private bool _paused = true;
    private string _previewAnimationName = string.Empty;
    private string? _error;

    public bool Draw(
        EditorProjectContext project,
        AssetReference reference,
        ModelAsset model,
        IReadOnlyList<SkeletalSocketDefinition> sockets,
        ref Guid selectedId,
        Renderer2D renderer,
        Renderer3D renderer3D,
        int windowWidth,
        int windowHeight,
        Action persist)
    {
        Ensure(project, reference, model);

        if (_scene == null ||
            _renderer == null ||
            _gizmoProxy == null)
        {
            ImGui.TextColored(
                new Vector4(1.0f, 0.40f, 0.35f, 1.0f),
                "Socket preview unavailable.");

            if (!string.IsNullOrWhiteSpace(_error))
            {
                ImGui.TextWrapped(_error);
            }

            return false;
        }

        if (!_paused &&
            !string.IsNullOrWhiteSpace(_previewAnimationName))
        {
            _scene.UpdateInternal();
        }

        Guid selectedGuid = selectedId;
        SkeletalSocketDefinition? selected =
            sockets.FirstOrDefault(socket => socket.Id == selectedGuid);

        EnsureAttachment(project, selected);
        SyncProxy(selected);
        SyncPreviewAttachment(selected);

        _framebuffer.Render(
            renderer,
            renderer3D,
            _scene,
            EditorMode.Edit,
            _camera2D,
            _camera,
            true,
            PreviewRenderWidth,
            PreviewRenderHeight,
            windowWidth,
            windowHeight,
            drawGrid3D: false,
            prepareEnvironmentLighting3D: false,
            renderShadows3D: false);

        bool toolbarVisible =
            EditorUi.BeginToolbar("##SocketPreviewToolbar");

        if (toolbarVisible)
        {
            _gizmo.DrawToolbar();

            ImGui.SameLine();
            DrawPoseControls(model);

            ImGui.SameLine();
            if (EditorUi.ToolbarButton(
                    selected != null ? "Frame Socket" : "Frame Model",
                    selected != null ? "Frame the selected socket" : "Frame the reference model"))
            {
                Frame(selected);
            }

            ImGui.SameLine();
            ImGui.TextColored(
                EditorTheme.TextMuted,
                "F Frame   RMB Orbit   MMB Pan   Wheel Zoom");
        }

        EditorUi.EndToolbar();

        Vector2 available =
            ImGui.GetContentRegionAvail();

        float availableWidth =
            Math.Max(
                available.X,
                240.0f);

        float availableHeight =
            Math.Max(
                available.Y,
                180.0f);

        const float previewAspect =
            (float)PreviewRenderWidth /
            PreviewRenderHeight;

        float imageWidth =
            availableWidth;

        float imageHeight =
            imageWidth /
            previewAspect;

        if (imageHeight >
            availableHeight)
        {
            imageHeight =
                availableHeight;

            imageWidth =
                imageHeight *
                previewAspect;
        }

        Vector2 imageSize =
            new(
                Math.Max(imageWidth, 240.0f),
                Math.Max(imageHeight, 180.0f));

        Vector2 imageMinimum =
            ImGui.GetCursorScreenPos();

        Vector2 imageMaximum =
            imageMinimum + imageSize;

        ImDrawListPtr drawList =
            ImGui.GetWindowDrawList();

        drawList.AddRectFilled(
            imageMinimum,
            imageMaximum,
            ImGui.GetColorU32(EditorTheme.BackgroundRaised),
            EditorTheme.SmallCornerRadius);

        ImGui.Image(
            _framebuffer.TextureId,
            imageSize,
            new Vector2(0.0f, 1.0f),
            new Vector2(1.0f, 0.0f));

        bool hovered =
            ImGui.IsItemHovered();

        DrawMarkers(
            sockets,
            ref selectedId,
            imageMinimum,
            imageSize,
            hovered);

        if (hovered)
        {
            _gizmo.ApplyShortcuts(
                ImGui.IsKeyPressed(ImGuiKey.W),
                ImGui.IsKeyPressed(ImGuiKey.E),
                ImGui.IsKeyPressed(ImGuiKey.R));
        }

        bool changed =
            false;

        if (selected != null &&
            SkeletalSocketResolver.TryGetSocketWorldTransform(
                _renderer,
                selected.Name,
                out _))
        {
            _gizmo.UpdateAndDraw(
                _gizmoProxy,
                _camera,
                hovered,
                imageMinimum,
                imageSize,
                () =>
                {
                    changed = ApplyProxy(selected);
                    if (changed)
                    {
                        persist();
                    }
                });
        }

        HandleCamera(
            hovered,
            selected);

        return changed;
    }

    private void DrawPoseControls(
        ModelAsset model)
    {
        if (_renderer == null)
        {
            return;
        }

        string preview =
            string.IsNullOrWhiteSpace(_previewAnimationName)
                ? "Rest Pose"
                : _previewAnimationName;

        ImGui.SetNextItemWidth(170.0f);

        if (ImGui.BeginCombo(
                "##SocketPreviewPose",
                preview))
        {
            bool restSelected =
                string.IsNullOrWhiteSpace(_previewAnimationName);

            if (ImGui.Selectable(
                    "Rest Pose",
                    restSelected))
            {
                _previewAnimationName =
                    string.Empty;

                _renderer.Stop();
                _paused =
                    true;

                /*
                 * Socket placement is authored against the neutral imported
                 * skeleton pose. This keeps the hand/bone stationary while
                 * the user aligns a weapon or prop.
                 */
                SyncProxy(null);
            }

            foreach (ImportedAnimation animation
                     in model.Animations)
            {
                if (string.IsNullOrWhiteSpace(animation.Name))
                {
                    continue;
                }

                bool selected =
                    string.Equals(
                        _previewAnimationName,
                        animation.Name,
                        StringComparison.OrdinalIgnoreCase);

                if (ImGui.Selectable(
                        animation.Name,
                        selected))
                {
                    _previewAnimationName =
                        animation.Name;

                    if (_renderer.Play(
                            animation.Name,
                            true,
                            0.0f))
                    {
                        _paused =
                            false;
                    }
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        EditorUi.Tooltip(
            "Rest Pose is the default for socket placement. Choose an animation only when you want to preview the attachment in motion.");

        bool animationSelected =
            !string.IsNullOrWhiteSpace(_previewAnimationName);

        ImGui.SameLine();
        ImGui.BeginDisabled(!animationSelected);

        if (EditorUi.ToolbarButton(
                _paused ? "Play" : "Pause",
                _paused ? "Play selected preview animation" : "Pause selected preview animation"))
        {
            if (_paused)
            {
                _renderer.Resume();
                _paused =
                    false;
            }
            else
            {
                _renderer.Pause();
                _paused =
                    true;
            }
        }

        ImGui.SameLine();

        if (EditorUi.ToolbarButton(
                "Restart",
                "Restart the selected preview animation"))
        {
            if (_renderer.Play(
                    _previewAnimationName,
                    true,
                    0.0f))
            {
                if (_paused)
                {
                    _renderer.Pause();
                }
            }
        }

        ImGui.EndDisabled();
    }

    private void Ensure(
        EditorProjectContext project,
        AssetReference reference,
        ModelAsset model)
    {
        if (_scene != null &&
            _renderer != null &&
            _modelGuid == model.Guid)
        {
            return;
        }

        DestroyPreviewScene();

        _modelGuid =
            model.Guid;

        _error =
            null;

        try
        {
            _scene =
                new Scene(
                    "Socket Preview",
                    project.Project.Classification);

            GameObject root =
                _scene.CreateGameObject(
                    "Reference Model");

            _renderer =
                root.AddComponent(
                    new SkeletalMeshRenderer
                    {
                        Model = reference,
                        SkeletonKey = model.Skeleton?.Key,
                        PlayOnStart = false,
                        Loop = true,
                        Speed = 1.0f,
                        TransitionDuration = 0.0f
                    });

            GameObject keyLight =
                _scene.CreateGameObject(
                    "Socket Preview Key Light");

            keyLight.Transform.EulerAngles =
                new Vector3(-35.0f, -35.0f, 0.0f);

            keyLight.AddComponent(
                new DirectionalLight
                {
                    Intensity = 1.25f,
                    AmbientIntensity = 0.25f,
                    CastShadows = false
                });

            GameObject fillLight =
                _scene.CreateGameObject(
                    "Socket Preview Fill Light");

            fillLight.Transform.EulerAngles =
                new Vector3(20.0f, 145.0f, 0.0f);

            fillLight.AddComponent(
                new DirectionalLight
                {
                    Intensity = 0.50f,
                    AmbientIntensity = 0.10f,
                    CastShadows = false
                });

            _gizmoProxy =
                _scene.CreateGameObject(
                    "Socket Gizmo Proxy");

            /*
             * Match the working animation preview lifecycle: load the private
             * preview scene first, then start an animation and frame the live
             * skinned bounds. Playing before Scene.LoadInternal() left the
             * socket preview dependent on unresolved runtime state.
             */
            _scene.LoadInternal();

            /*
             * Socket authoring starts in the imported rest/bind pose. Running
             * the first animation automatically made precise weapon alignment
             * unnecessarily difficult and could start on an arbitrary clip
             * such as a crouch walk. Animation playback is now opt-in from the
             * preview toolbar.
             */
            _renderer.ResolveRuntimeResources();
            _renderer.Stop();
            _previewAnimationName =
                string.Empty;
            _paused =
                true;

            _scene.UpdateInternal();
            FrameModel();
        }
        catch (Exception exception)
        {
            _error =
                exception.Message;

            DestroyPreviewScene();
        }
    }

    private void EnsureAttachment(
        EditorProjectContext project,
        SkeletalSocketDefinition? socket)
    {
        Guid? guid =
            socket?.PreviewAssetGuid;

        if (guid ==
            _previewGuid)
        {
            return;
        }

        if (_attachment != null &&
            _scene != null)
        {
            _scene.DestroyGameObject(
                _attachment);
        }

        _attachment =
            null;

        _previewGuid =
            guid;

        if (guid == null ||
            socket == null ||
            _scene == null)
        {
            return;
        }

        AssetRecord? asset =
            project.AssetDatabase.Resolve(
                new AssetReference(
                    guid.Value,
                    socket.PreviewAssetPath));

        if (asset?.Type !=
            AssetType.Model3D)
        {
            return;
        }

        AssetReference reference =
            new(
                asset.Guid,
                asset.ProjectPath);

        ModelAsset model =
            project.Assets.LoadModel(
                reference);

        _attachment =
            _scene.CreateGameObject(
                "Socket Preview Asset");

        Dictionary<string, GameObject> nodes =
            new();

        foreach (ImportedNode node in model.Nodes)
        {
            nodes[node.Key] =
                _scene.CreateGameObject(
                    node.Name);
        }

        foreach (ImportedNode node in model.Nodes)
        {
            GameObject item =
                nodes[node.Key];

            item.SetParent(
                node.ParentKey != null &&
                nodes.TryGetValue(
                    node.ParentKey,
                    out GameObject? parent)
                    ? parent
                    : _attachment,
                false);

            ApplyLocal(
                item,
                node.LocalTransform);

            foreach (string meshKey in node.MeshKeys)
            {
                ImportedMesh mesh =
                    model.Meshes.First(candidate => candidate.Key == meshKey);

                GameObject meshObject =
                    node.MeshKeys.Count == 1
                        ? item
                        : _scene.CreateGameObject(mesh.Name);

                if (!ReferenceEquals(
                        meshObject,
                        item))
                {
                    meshObject.SetParent(
                        item,
                        false);
                }

                MeshRenderer meshRenderer =
                    new()
                    {
                        MeshReference =
                            new ModelMeshReference(
                                reference,
                                meshKey),

                        Mesh =
                            project.Assets.GetModelMesh(
                                reference,
                                meshKey)
                    };

                if (mesh.MaterialKey !=
                    null)
                {
                    meshRenderer.MaterialReference =
                        new ModelMaterialReference(
                            reference,
                            mesh.MaterialKey);

                    meshRenderer.Material =
                        project.Assets.GetModelMaterial(
                            reference,
                            mesh.MaterialKey);
                }

                meshObject.AddComponent(
                    meshRenderer);
            }
        }
    }

    private void SyncPreviewAttachment(
        SkeletalSocketDefinition? socket)
    {
        if (socket == null ||
            _renderer == null ||
            _attachment == null)
        {
            return;
        }

        if (!SkeletalSocketResolver.TryGetSocketWorldTransform(
                _renderer,
                socket.Name,
                out SkeletalSocketTransform transform))
        {
            return;
        }

        _attachment.Transform.WorldPosition =
            transform.Position;

        _attachment.Transform.WorldRotation =
            transform.Rotation;

        _attachment.Transform.WorldScale =
            transform.Scale;
    }

    private void SyncProxy(
        SkeletalSocketDefinition? socket)
    {
        if (socket == null ||
            _renderer == null ||
            _gizmoProxy == null ||
            _gizmo.OwnsMouse)
        {
            return;
        }

        if (SkeletalSocketResolver.TryGetSocketWorldTransform(
                _renderer,
                socket.Name,
                out SkeletalSocketTransform transform))
        {
            _gizmoProxy.Transform.WorldPosition =
                transform.Position;

            _gizmoProxy.Transform.WorldRotation =
                transform.Rotation;

            _gizmoProxy.Transform.WorldScale =
                transform.Scale;
        }
    }

    private bool ApplyProxy(
        SkeletalSocketDefinition socket)
    {
        if (_renderer == null ||
            _gizmoProxy == null ||
            !_renderer.TryGetBoneWorldMatrix(
                socket.BoneName,
                out Matrix4x4 boneWorld) ||
            !SkeletalSocketResolver.TryExtractStableBoneFrame(
                boneWorld,
                out Vector3 bonePosition,
                out Quaternion boneRotation,
                out Vector3 boneScale))
        {
            return false;
        }

        Vector3 inheritedScale =
            socket.InheritBoneScale
                ? boneScale
                : Vector3.One;

        socket.PositionOffset =
            Vector3.Transform(
                _gizmoProxy.Transform.WorldPosition -
                bonePosition,
                Quaternion.Inverse(
                    boneRotation)) /
            inheritedScale;

        Quaternion localRotation =
            Quaternion.Normalize(
                _gizmoProxy.Transform.WorldRotation *
                Quaternion.Inverse(
                    boneRotation));

        socket.RotationOffsetDegrees =
            Euler(
                localRotation);

        socket.Scale =
            Vector3.Max(
                _gizmoProxy.Transform.WorldScale /
                inheritedScale,
                new Vector3(0.0001f));

        return true;
    }

    private void DrawMarkers(
        IReadOnlyList<SkeletalSocketDefinition> sockets,
        ref Guid selected,
        Vector2 imageMinimum,
        Vector2 imageSize,
        bool imageHovered)
    {
        if (_renderer ==
            null)
        {
            return;
        }

        ImDrawListPtr drawList =
            ImGui.GetWindowDrawList();

        Vector2 mouse =
            ImGui.GetMousePos();

        bool clicked =
            imageHovered &&
            ImGui.IsMouseClicked(
                ImGuiMouseButton.Left);

        foreach (SkeletalSocketDefinition socket
                 in sockets)
        {
            if (!SkeletalSocketResolver.TryGetSocketWorldTransform(
                    _renderer,
                    socket.Name,
                    out SkeletalSocketTransform transform))
            {
                continue;
            }

            Vector3 toSocket =
                transform.Position -
                _camera.Position;

            if (Vector3.Dot(
                    toSocket,
                    _camera.Forward) <=
                0.001f)
            {
                continue;
            }

            Vector2 position =
                Gizmo3DController.Project(
                    transform.Position,
                    _camera,
                    imageMinimum,
                    imageSize);

            bool isSelected =
                socket.Id ==
                selected;

            uint color =
                ImGui.GetColorU32(
                    isSelected
                        ? new Vector4(1.0f, 0.80f, 0.10f, 1.0f)
                        : new Vector4(0.20f, 0.80f, 1.0f, 1.0f));

            drawList.AddCircleFilled(
                position,
                isSelected ? 7.0f : 5.0f,
                color);

            drawList.AddLine(
                position - new Vector2(9.0f, 0.0f),
                position + new Vector2(9.0f, 0.0f),
                color,
                2.0f);

            drawList.AddLine(
                position - new Vector2(0.0f, 9.0f),
                position + new Vector2(0.0f, 9.0f),
                color,
                2.0f);

            drawList.AddText(
                position + new Vector2(10.0f, -8.0f),
                color,
                socket.Name);

            if (clicked &&
                Vector2.Distance(
                    mouse,
                    position) <
                12.0f)
            {
                selected =
                    socket.Id;
            }
        }
    }

    private void Frame(
        SkeletalSocketDefinition? socket)
    {
        if (socket == null ||
            _renderer == null ||
            !SkeletalSocketResolver.TryGetSocketWorldTransform(
                _renderer,
                socket.Name,
                out SkeletalSocketTransform transform))
        {
            FrameModel();
            return;
        }

        _target =
            transform.Position;

        _distance =
            Math.Clamp(
                _modelFrameDistance *
                0.25f,
                0.35f,
                3.0f);

        UpdateCamera();
    }

    private void FrameModel()
    {
        if (_renderer ==
            null)
        {
            return;
        }

        Vector3 minimum;
        Vector3 maximum;

        if (!_renderer.TryGetCurrentModelBounds(
                out BoundingBox3D bounds) ||
            !bounds.IsValid)
        {
            minimum =
                new Vector3(-0.5f, 0.0f, -0.5f);

            maximum =
                new Vector3(0.5f, 1.8f, 0.5f);
        }
        else
        {
            minimum =
                bounds.Minimum;

            maximum =
                bounds.Maximum;
        }

        Vector3 center =
            (minimum + maximum) *
            0.5f;

        Vector3 size =
            Vector3.Max(
                maximum - minimum,
                new Vector3(0.05f));

        _camera.Yaw =
            -135.0f;

        _camera.Pitch =
            -8.0f;

        _camera.FieldOfView =
            30.0f;

        float aspectRatio =
            (float)PreviewRenderWidth /
            PreviewRenderHeight;

        float verticalHalfFov =
            _camera.FieldOfView *
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

        float horizontalExtent =
            MathF.Sqrt(
                size.X * size.X +
                size.Z * size.Z) *
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

        _target =
            center;

        _distance =
            Math.Max(
                Math.Max(
                    horizontalDistance,
                    verticalDistance) *
                0.95f,
                0.20f);

        _modelFrameDistance =
            _distance;

        UpdateCamera();
    }

    private void HandleCamera(
        bool hovered,
        SkeletalSocketDefinition? selected)
    {
        if (!hovered)
        {
            return;
        }

        ImGuiIOPtr io =
            ImGui.GetIO();

        if (ImGui.IsKeyPressed(
                ImGuiKey.F))
        {
            Frame(
                selected);
        }

        if (!_gizmo.OwnsMouse &&
            ImGui.IsMouseDragging(
                ImGuiMouseButton.Right))
        {
            _camera.Yaw +=
                io.MouseDelta.X *
                0.30f;

            _camera.Pitch =
                Math.Clamp(
                    _camera.Pitch -
                    io.MouseDelta.Y *
                    0.30f,
                    -85.0f,
                    85.0f);

            UpdateCamera();
        }

        if (!_gizmo.OwnsMouse &&
            ImGui.IsMouseDragging(
                ImGuiMouseButton.Middle))
        {
            Vector3 cameraUp =
                Vector3.Normalize(
                    Vector3.Cross(
                        _camera.Right,
                        _camera.Forward));

            float panScale =
                Math.Max(
                    _distance *
                    0.0025f,
                    0.0005f);

            _target +=
                (
                    -_camera.Right *
                    io.MouseDelta.X +
                    cameraUp *
                    io.MouseDelta.Y
                ) *
                panScale;

            UpdateCamera();
        }

        if (MathF.Abs(
                io.MouseWheel) >
            0.0001f)
        {
            float zoomFactor =
                MathF.Pow(
                    0.88f,
                    io.MouseWheel);

            _distance =
                Math.Clamp(
                    _distance *
                    zoomFactor,
                    0.05f,
                    500.0f);

            UpdateCamera();
        }
    }

    private void UpdateCamera()
    {
        /*
         * EditorCamera3D owns the authoritative yaw/pitch -> Forward math.
         * The old socket preview duplicated that math with a different axis
         * convention, so the camera was positioned using one forward vector
         * while its View matrix looked along another. That put the mannequin
         * outside/behind the actual view and produced a blank preview.
         */
        _camera.Position =
            _target -
            _camera.Forward *
            _distance;
    }

    private static void ApplyLocal(
        GameObject item,
        Matrix4x4 matrix)
    {
        if (!Matrix4x4.Decompose(
                matrix,
                out Vector3 scale,
                out Quaternion rotation,
                out Vector3 position))
        {
            return;
        }

        item.Transform.LocalPosition =
            position;

        item.Transform.LocalRotation =
            rotation;

        item.Transform.LocalScale =
            scale;
    }

    private static Vector3 Euler(
        Quaternion value)
    {
        value =
            Quaternion.Normalize(
                value);

        float x =
            MathF.Asin(
                Math.Clamp(
                    2.0f *
                    (value.W * value.X - value.Y * value.Z),
                    -1.0f,
                    1.0f));

        float y =
            MathF.Atan2(
                2.0f *
                (value.W * value.Y + value.X * value.Z),
                1.0f -
                2.0f *
                (value.X * value.X + value.Y * value.Y));

        float z =
            MathF.Atan2(
                2.0f *
                (value.W * value.Z + value.X * value.Y),
                1.0f -
                2.0f *
                (value.X * value.X + value.Z * value.Z));

        return
            new Vector3(
                x * 180.0f / MathF.PI,
                y * 180.0f / MathF.PI,
                z * 180.0f / MathF.PI);
    }

    private void DestroyPreviewScene()
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

        _scene =
            null;

        _renderer =
            null;

        _gizmoProxy =
            null;

        _attachment =
            null;

        _previewGuid =
            null;

        _previewAnimationName =
            string.Empty;
    }

    public void Reset()
    {
        DestroyPreviewScene();

        _modelGuid =
            Guid.Empty;

        _target =
            Vector3.Zero;

        _distance =
            2.5f;

        _modelFrameDistance =
            2.5f;

        _paused =
            true;

        _previewAnimationName =
            string.Empty;

        _error =
            null;
    }

    public void Dispose()
    {
        Reset();
        _framebuffer.Dispose();
    }
}
