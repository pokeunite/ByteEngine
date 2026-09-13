using System.Numerics;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Editor.Gizmos;

using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class BlueprintWorkspacePanel
    : IDisposable
{
    private readonly SceneFramebuffer _framebuffer =
        new();

    private readonly EditorCamera3D _camera =
        new();

    private BlueprintDefinition? _blueprint;

    private Scene? _preview;

    private AssetRecord? _asset;

    private EditorProjectContext? _project;

    private Guid _selectedPreviewObjectId =
        Guid.Empty;

    private Component? _selectedComponent;
    private string _addComponentSearch = string.Empty;
    private readonly BlueprintTransformGizmo3D _transformGizmo = new();
    private readonly CameraActivationPromptState _cameraActivationPrompt = new();

    private bool _open;
    internal Guid? OpenAssetId => _asset?.Guid;

    internal void EvictDeletedAsset()
    {
        if (_asset == null || _project == null ||
            (_project.AssetDatabase.TryGetAsset(_asset.Guid, out _) && File.Exists(_asset.FullPath))) return;
        _open = false;
        _blueprint = null;
        _preview = null;
        _asset = null;
        _selectedPreviewObjectId = Guid.Empty;
        _selectedComponent = null;
        _propertyUndo.Clear();
        _propertyRedo.Clear();
        _propertyBefore = null;
        _dirty = false;
        _cameraActivationPrompt.Reset();
    }

    private bool _dirty;
    private bool _showAdvanced;
    private readonly Stack<SceneData> _propertyUndo = new();
    private readonly Stack<SceneData> _propertyRedo = new();
    private SceneData? _propertyBefore;

    private void BeginPropertyEdit()
    {
        if (_propertyBefore == null && _preview != null && _project != null)
            _propertyBefore = _project.Scenes.Serialize(_preview);
    }

    private void CommitPropertyEdit()
    {
        if (_propertyBefore == null) return;
        _propertyUndo.Push(_propertyBefore);
        _propertyRedo.Clear();
        _propertyBefore = null;
    }

    private void RestorePropertyEdit(bool redo)
    {
        var source = redo ? _propertyRedo : _propertyUndo;
        var destination = redo ? _propertyUndo : _propertyRedo;
        if (_preview == null || _project == null || !source.TryPop(out SceneData? snapshot)) return;
        destination.Push(_project.Scenes.Serialize(_preview));
        _preview = _project.Scenes.Deserialize(snapshot);
        _selectedComponent = null;
        MarkDirty();
    }

    private string _statusMessage =
        string.Empty;

    private bool _statusIsError;

    private Vector3? _previewBoundsMin;

    private Vector3? _previewBoundsMax;

    private string _lastDebugDumpPath =
        string.Empty;

    public void Open(
        AssetRecord asset,
        EditorProjectContext project)
    {
        ArgumentNullException.ThrowIfNull(
            asset);

        ArgumentNullException.ThrowIfNull(
            project);

        _cameraActivationPrompt.Reset();
        if (_project != null) _project.AssetDatabase.DatabaseChanged -= EvictDeletedAsset;
        project.AssetDatabase.DatabaseChanged += EvictDeletedAsset;
        _propertyUndo.Clear();
        _propertyRedo.Clear();
        _propertyBefore = null;

        _asset =
            asset;

        _project =
            project;

        _blueprint =
            new BlueprintSerializer()
                .Load(
                    asset.FullPath);

        _camera.Reset();

        RebuildPreview();

        _dirty =
            false;

        _open =
            true;
    }

    public void Draw(
        Renderer2D renderer,
        Renderer3D renderer3D,
        int windowWidth,
        int windowHeight)
    {
        EvictDeletedAsset();
        if (!_open ||
            _blueprint ==
                null ||
            _preview ==
                null ||
            _asset ==
                null ||
            _project ==
                null)
        {
            return;
        }

        ImGui.SetNextWindowSize(
            new Vector2(
                1240.0f,
                780.0f),
            ImGuiCond.FirstUseEver);

        string dirtyMarker =
            _dirty
                ? " *"
                : string.Empty;

        bool visible = ImGui.Begin(
            $"{_blueprint.Name} — BLUEPRINT ASSET{dirtyMarker}###BlueprintWorkspace",
            ref _open,
            ImGuiWindowFlags.NoScrollbar |
            ImGuiWindowFlags.NoScrollWithMouse |
            (_transformGizmo.OwnsMouse ? ImGuiWindowFlags.NoMove : ImGuiWindowFlags.None));

        if (!_open || !visible)
        {
            if (!_open) _cameraActivationPrompt.Reset();
            ImGui.End();
            return;
        }

        DrawToolbar();

        ImGui.Separator();

        if (ImGui.BeginTable(
                "BlueprintLayout",
                3,
                ImGuiTableFlags.Resizable |
                ImGuiTableFlags.BordersInnerV))
        {
            ImGui.TableSetupColumn(
                "Hierarchy",
                ImGuiTableColumnFlags.WidthFixed,
                285.0f);

            ImGui.TableSetupColumn(
                "Viewport",
                ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableSetupColumn(
                "Inspector",
                ImGuiTableColumnFlags.WidthFixed,
                330.0f);

            DrawHierarchyColumn();

            DrawViewportColumn(
                renderer,
                renderer3D,
                windowWidth,
                windowHeight);

            DrawInspectorColumn();

            ImGui.EndTable();
        }

        DrawCameraActivationPrompt();

        ImGui.End();
    }

    // ========================================================
    // TOOLBAR
    // ========================================================

    private void DrawToolbar()
    {
        if (ImGui.Button(_dirty ? "Save *##BlueprintSave" : "Save##BlueprintSave"))
        {
            SaveBlueprint();
        }
        ImGui.SameLine();
        if (ImGui.Button("+ Add##BlueprintAdd")) ImGui.OpenPopup("Blueprint Add Menu");
        DrawBlueprintAddMenu();
        ImGui.SameLine();
        ImGui.TextDisabled("|");
        ImGui.SameLine();
        _transformGizmo.DrawToolbar();
        ImGui.SameLine();
        ImGui.BeginDisabled(_propertyUndo.Count == 0);
        if (ImGui.SmallButton("Undo")) RestorePropertyEdit(false);
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(_propertyRedo.Count == 0);
        if (ImGui.SmallButton("Redo")) RestorePropertyEdit(true);
        ImGui.EndDisabled();
        ImGui.SameLine();

        ImGui.BeginDisabled(!_previewBoundsMin.HasValue || !_previewBoundsMax.HasValue);
        if (ImGui.Button("Frame##BlueprintFrame")) FramePreviewBounds();
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("...##BlueprintMore")) ImGui.OpenPopup("More##BlueprintToolbarMore");
        if (ImGui.BeginPopup("More##BlueprintToolbarMore"))
        {
            bool canDelete = GetSelectedPreviewObject()?.Parent != null;
            ImGui.BeginDisabled(!canDelete);
            if (ImGui.MenuItem("Delete Object##BlueprintDelete")) DeleteSelectedObject();
            ImGui.EndDisabled();
            if (ImGui.MenuItem("Normalize Models##BlueprintNormalize")) NormalizeExistingModelScales();
            if (ImGui.MenuItem("Reimport Models##BlueprintReimport")) ReimportReferencedModels();
            if (ImGui.MenuItem("Write Debug Dump##BlueprintDebug"))
                WriteDebugDump("Manual Blueprint debug dump");
            ImGui.EndPopup();
        }

        ImGui.SameLine();

        ImGui.TextDisabled(
            _asset!.ProjectPath);

        if (_dirty)
        {
            ImGui.SameLine();

            ImGui.TextColored(
                new Vector4(
                    1.0f,
                    0.72f,
                    0.25f,
                    1.0f),
                "Unsaved");
        }

        if (!string.IsNullOrWhiteSpace(
                _statusMessage))
        {
            ImGui.SameLine();

            ImGui.TextColored(
                _statusIsError
                    ? new Vector4(
                        1.0f,
                        0.35f,
                        0.30f,
                        1.0f)
                    : new Vector4(
                        0.35f,
                        0.95f,
                        0.45f,
                        1.0f),
                _statusMessage);
        }
    }

    // ========================================================
    // HIERARCHY
    // ========================================================

    private void DrawHierarchyColumn()
    {
        ImGui.TableNextColumn();

        ImGui.BeginChild(
            "BlueprintHierarchyScroll",
            Vector2.Zero,
            ImGuiChildFlags.None,
            ImGuiWindowFlags.HorizontalScrollbar);

        ImGui.SeparatorText("OBJECTS");

        foreach (GameObject root
                 in _preview!.GameObjects
                     .Where(
                         gameObject =>
                             gameObject.Parent ==
                             null)
                     .ToArray())
        {
            DrawGameObjectNode(
                root);
        }

        ImGui.Dummy(
            new Vector2(
                0.0f,
                8.0f));

        ImGui.SeparatorText(
            "BLUEPRINT");

        int type =
            (int)_blueprint!.Type;

        if (ImGui.Combo(
                "Type",
                ref type,
                "Generic Object\0Character\0"))
        {
            _blueprint.Type =
                (BlueprintType)type;

            MarkDirty();
        }

        ImGui.TextDisabled(
            _asset!.ProjectPath);

        ImGui.EndChild();
    }

    private void DrawGameObjectNode(
        GameObject gameObject)
    {
        bool selected =
            _selectedPreviewObjectId ==
            gameObject.Id;

        ImGuiTreeNodeFlags flags =
            ImGuiTreeNodeFlags.OpenOnArrow |
            ImGuiTreeNodeFlags.SpanFullWidth |
            ImGuiTreeNodeFlags.DefaultOpen;

        if (selected)
        {
            flags |=
                ImGuiTreeNodeFlags.Selected;
        }

        if (gameObject.Children.Count ==
            0)
        {
            flags |=
                ImGuiTreeNodeFlags.Leaf;
        }

        bool open =
            ImGui.TreeNodeEx(
                $"{gameObject.Name}##bp-object:{gameObject.Id}",
                flags);

        if (ImGui.IsItemClicked(
                ImGuiMouseButton.Left) &&
            !ImGui.IsItemToggledOpen())
        {
            _selectedPreviewObjectId =
                gameObject.Id;
            _selectedComponent = null;
        }

        if (ImGui.BeginDragDropSource())
        {
            GameObjectDragDrop.Set(gameObject.Id);
            ImGui.TextUnformatted(gameObject.Name);
            ImGui.EndDragDropSource();
        }
        if (ImGui.BeginDragDropTarget())
        {
            Guid? draggedId = GameObjectDragDrop.Accept();
            if (draggedId.HasValue && _preview!.FindGameObject(draggedId.Value) is { } dragged &&
                dragged.Parent != null && dragged.SetParent(gameObject, false)) MarkDirty();
            ImGui.EndDragDropTarget();
        }

        if (ImGui.BeginPopupContextItem(
                $"BlueprintObjectContext##{gameObject.Id}"))
        {
            if (ImGui.MenuItem(
                    "Add Child"))
            {
                _selectedPreviewObjectId =
                    gameObject.Id;

                AddChildToSelected();
            }

            bool canDelete =
                gameObject.Parent !=
                null;

            ImGui.BeginDisabled(
                !canDelete);

            if (ImGui.MenuItem(
                    "Delete"))
            {
                _selectedPreviewObjectId =
                    gameObject.Id;

                DeleteSelectedObject();
            }

            ImGui.EndDisabled();

            ImGui.EndPopup();
        }

        if (open)
        {
            foreach (GameObject child
                     in gameObject.Children.ToArray())
            {
                DrawGameObjectNode(
                    child);
            }

            ImGui.TreePop();
        }
    }

    // ========================================================
    // VIEWPORT
    // ========================================================

    private void DrawViewportColumn(
        Renderer2D renderer,
        Renderer3D renderer3D,
        int windowWidth,
        int windowHeight)
    {
        ImGui.TableNextColumn();

        ImGui.BeginChild(
            "BlueprintViewport",
            Vector2.Zero,
            ImGuiChildFlags.None,
            ImGuiWindowFlags.NoScrollbar |
            ImGuiWindowFlags.NoScrollWithMouse);

        Vector2 viewport =
            ImGui.GetContentRegionAvail();

        viewport.X =
            Math.Max(
                viewport.X,
                1.0f);

        viewport.Y =
            Math.Max(
                viewport.Y,
                1.0f);

        _framebuffer.Render(
            renderer,
            renderer3D,
            _preview!,
            EditorMode.Edit,
            new EditorCamera(),
            _camera,
            true,
            (int)viewport.X,
            (int)viewport.Y,
            windowWidth,
            windowHeight,
            drawGrid3D:
                false);

        ImGui.Image(
            _framebuffer.TextureId,
            viewport,
            new Vector2(
                0.0f,
                1.0f),
            new Vector2(
                1.0f,
                0.0f));

        CameraRigGizmoRenderer.Draw(
            _preview!,
            _camera,
            ImGui.GetItemRectMin(),
            viewport,
            GetSelectedPreviewObject(),
            _selectedComponent);

        bool viewportHovered = ImGui.IsItemHovered();
        bool gizmoConsumed = _transformGizmo.UpdateAndDraw(
            GetSelectedPreviewObject(), _camera, viewportHovered, ImGui.GetItemRectMin(), viewport,
            MarkDirty, BeginPropertyEdit, CommitPropertyEdit);

        if (viewportHovered && !gizmoConsumed && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            Vector2 mouse = ImGui.GetMousePos();
            GameObject? nearest = _preview!.GameObjects
                .Where(item => item.ActiveInHierarchy)
                .Select(item => new { Item = item, Distance = Vector2.Distance(mouse,
                    Gizmo3DController.Project(item.Transform.WorldPosition, _camera, ImGui.GetItemRectMin(), viewport)) })
                .Where(item => item.Distance <= 18f)
                .OrderBy(item => item.Distance)
                .Select(item => item.Item)
                .FirstOrDefault();
            if (nearest != null) { _selectedPreviewObjectId = nearest.Id; _selectedComponent = null; }
        }

        if (viewportHovered && !_transformGizmo.OwnsMouse)
        {
            ImGuiIOPtr io =
                ImGui.GetIO();

            if (ImGui.IsMouseDragging(
                    ImGuiMouseButton.Right))
            {
                Vector2 delta =
                    io.MouseDelta;

                _camera.Yaw +=
                    delta.X *
                    0.18f;

                _camera.Pitch =
                    Math.Clamp(
                        _camera.Pitch -
                        delta.Y *
                        0.18f,
                        -89.0f,
                        89.0f);
            }

            if (io.MouseWheel !=
                0.0f)
            {
                Vector3 focus =
                    GetPreviewFocus();

                float distance =
                    Math.Max(
                        Vector3.Distance(
                            _camera.Position,
                            focus) *
                        0.12f,
                        0.25f);

                _camera.Position +=
                    _camera.Forward *
                    io.MouseWheel *
                    distance;
            }
        }

        ImGui.EndChild();
    }

    // ========================================================
    // INSPECTOR
    // ========================================================

    private void DrawInspectorColumn()
    {
        ImGui.TableNextColumn();

        ImGui.BeginChild(
            "BlueprintInspectorScroll",
            Vector2.Zero,
            ImGuiChildFlags.None);

        ImGui.SeparatorText("DETAILS");

        GameObject? selected =
            GetSelectedPreviewObject();

        if (selected ==
            null)
        {
            ImGui.TextDisabled(
                "No Blueprint object selected.");

            ImGui.EndChild();

            return;
        }

        DrawObjectHeader(
            selected);

        DrawTransform(selected);

        DrawComponents(
            selected);

        DrawEventModules(
            selected);

        if (!string.IsNullOrWhiteSpace(
                _lastDebugDumpPath))
        {
            ImGui.SeparatorText(
                "DEBUG");

            ImGui.TextWrapped(
                _lastDebugDumpPath);
        }

        ImGui.EndChild();
    }

    private void DrawObjectHeader(
        GameObject selected)
    {
        string name =
            selected.Name;

        if (ImGui.InputText(
                "Name",
                ref name,
                256))
        {
            selected.Name =
                string.IsNullOrWhiteSpace(
                    name)
                    ? "GameObject"
                    : name;

            if (selected.Parent ==
                null)
            {
                _blueprint!.Name =
                    selected.Name;
            }

            MarkDirty();
        }

        bool active =
            selected.Active;

        if (ImGui.Checkbox(
                "Active",
                ref active))
        {
            selected.Active =
                active;

            MarkDirty();
        }

        ImGui.TextDisabled(
            selected.Parent ==
                null
                ? "Blueprint Root"
                : $"Child of {selected.Parent.Name}");
    }

    private void DrawTransform(
        GameObject selected)
    {
        ImGui.SeparatorText(
            "TRANSFORM");

        Vector3 position =
            selected.Transform.LocalPosition;

        if (ImGui.DragFloat3(
                "Position",
                ref position,
                0.05f))
        {
            selected.Transform.LocalPosition =
                position;

            MarkDirty();
        }

        Vector3 rotation =
            selected.Transform.EulerAngles;

        if (ImGui.DragFloat3(
                "Rotation",
                ref rotation,
                0.25f))
        {
            selected.Transform.EulerAngles =
                rotation;

            MarkDirty();
        }

        Vector3 scale =
            selected.Transform.LocalScale;

        if (ImGui.DragFloat3(
                "Scale",
                ref scale,
                0.02f,
                0.001f,
                10000.0f))
        {
            selected.Transform.LocalScale =
                Vector3.Max(
                    scale,
                    new Vector3(
                        0.001f));

            MarkDirty();
        }
    }

    private void DrawComponents(
        GameObject selected)
    {
        ImGui.SeparatorText("COMPONENTS");
        ImGui.Checkbox("Show Advanced", ref _showAdvanced);

        Component[] components =
            selected.Components
                .Where(
                    component =>
                        component is not
                        EventModuleComponent)
                .ToArray();

        if (components.Length ==
            0)
        {
            ImGui.TextDisabled(
                "No components.");
        }

        foreach (Component component
                 in components)
        {
            ImGui.PushID(
                component.GetHashCode());

            string displayName = ComponentMetadataRegistry.DisplayName(component.GetType());
            ImGuiTreeNodeFlags flags = ReferenceEquals(component, _selectedComponent)
                ? ImGuiTreeNodeFlags.Selected
                : ImGuiTreeNodeFlags.None;
            bool open = ImGui.CollapsingHeader($"{displayName}##header", flags);
            if (ImGui.IsItemClicked()) _selectedComponent = component;

            bool enabled =
                component.Enabled;

            ImGui.Indent();
            if (ImGui.Checkbox(
                    "Enabled",
                    ref enabled))
            {
                component.Enabled =
                    enabled;

                MarkDirty();
            }

            ImGui.SameLine();

            if (ImGui.SmallButton(
                    "Remove"))
            {
                selected.RemoveComponent(
                    component);

                MarkDirty();

                ImGui.PopID();

                break;
            }

            if (open) DrawKnownComponentProperties(component);
            ImGui.Unindent();

            ImGui.PopID();
        }
    }

    private void DrawKnownComponentProperties(Component component)
    {
        ComponentPropertyRenderer.Draw(component, PropertyEditorContext.Blueprint, _showAdvanced,
            BeginPropertyEdit, MarkDirty, CommitPropertyEdit, _project);
    }

    // ========================================================
    // EVENT MODULES
    // ========================================================

    private void DrawEventModules(
        GameObject selected)
    {
        ImGui.SeparatorText(
            "EVENT MODULES");

        EventModuleComponent? runner =
            selected.GetComponent<EventModuleComponent>();

        if (runner ==
                null ||
            runner.Modules.Count ==
                0)
        {
            ImGui.TextDisabled(
                "No Event Modules attached.");
        }
        else
        {
            foreach (AssetReference reference
                     in runner.Modules.ToArray())
            {
                ImGui.PushID(
                    reference.Guid !=
                        Guid.Empty
                        ? reference.Guid.GetHashCode()
                        : reference.GetHashCode());

                ImGui.Text(
                    ResolveEventModuleName(
                        reference));

                ImGui.SameLine();

                if (ImGui.SmallButton(
                        "Remove"))
                {
                    runner.RemoveModule(
                        reference);

                    if (runner.Modules.Count ==
                        0)
                    {
                        selected.RemoveComponent(
                            runner);
                    }

                    MarkDirty();

                    ImGui.PopID();

                    break;
                }

                ImGui.PopID();
            }
        }

        string preview =
            "Attach Event Module...";

        ImGui.SetNextItemWidth(
            -1.0f);

        if (ImGui.BeginCombo(
                "##AttachEventModule",
                preview))
        {
            AssetRecord[] modules =
                _project!.AssetDatabase.Assets
                    .Where(
                        asset =>
                            asset.Type ==
                            AssetType.EventModule)
                    .OrderBy(
                        asset =>
                            asset.ProjectPath,
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray();

            foreach (AssetRecord moduleAsset
                     in modules)
            {
                string displayName =
                    Path.GetFileNameWithoutExtension(
                        moduleAsset.ProjectPath);

                bool alreadyAttached =
                    runner?.Modules.Any(
                        reference =>
                            reference.Guid ==
                            moduleAsset.Guid) ==
                    true;

                ImGui.BeginDisabled(
                    alreadyAttached);

                if (ImGui.Selectable(
                        $"{displayName}##bp-event:{moduleAsset.Guid}"))
                {
                    AttachEventModule(
                        selected,
                        moduleAsset);
                }

                ImGui.EndDisabled();

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(
                        alreadyAttached
                            ? "Already attached."
                            : moduleAsset.ProjectPath);
                }
            }

            ImGui.EndCombo();
        }
    }

    private void AttachEventModule(
        GameObject selected,
        AssetRecord asset)
    {
        EventModuleDefinition definition =
            new EventModuleSerializer()
                .Load(
                    asset.FullPath);

        var reference =
            new AssetReference(
                asset.Guid,
                asset.ProjectPath);

        EventModuleComponent runner =
            selected.GetComponent<EventModuleComponent>()
            ?? selected.AddComponent(
                new EventModuleComponent());

        runner.AddResolvedModule(
            reference,
            definition);

        MarkDirty();
    }

    private string ResolveEventModuleName(
        AssetReference reference)
    {
        AssetRecord? asset =
            _project?.AssetDatabase.Resolve(
                reference);

        return asset !=
            null
                ? Path.GetFileNameWithoutExtension(
                    asset.ProjectPath)
                : reference.CachedProjectPath ??
                  reference.Guid.ToString();
    }

    // ========================================================
    // SINGLE ADD ENTRY POINT
    // ========================================================

    private void DrawBlueprintAddMenu()
    {
        if (!ImGui.BeginPopup("Blueprint Add Menu")) return;
        GameObject? selected = GetSelectedPreviewObject();
        GameObject? root = _preview!.GameObjects.FirstOrDefault(item => item.Parent == null);

        if (selected != null && ImGui.BeginMenu("Component"))
        {
            ImGui.InputTextWithHint("##BlueprintAddComponentSearch", "Search components...", ref _addComponentSearch, 96);
            ComponentAddMenu.Draw(selected, _addComponentSearch, (component, _) =>
            {
                Camera3D? previousActive = component is Camera3D ? selected.Scene?.ActiveCamera : null;
                Component added = BlueprintAuthoringService.AddComponent(selected, component);
                if (added is Camera3D camera)
                {
                    _cameraActivationPrompt.Begin(CameraActivationPrompt.Create(camera, previousActive));
                }
                MarkDirty();
            });
            ImGui.EndMenu();
        }

        if (selected != null && ImGui.BeginMenu("Child Object"))
        {
            if (ImGui.MenuItem("Empty")) AddChildToSelected();
            if (ImGui.MenuItem("Camera"))
            {
                Camera3D? previousActive = selected.Scene?.ActiveCamera;
                GameObject cameraObject = _preview.CreateGameObject("Camera");
                cameraObject.SetParent(selected, false);
                Camera3D camera = cameraObject.AddComponent(new Camera3D());
                _selectedPreviewObjectId = cameraObject.Id;
                _selectedComponent = null;
                _cameraActivationPrompt.Begin(CameraActivationPrompt.Create(camera, previousActive));
                MarkDirty();
            }
            if (ImGui.MenuItem("Light"))
            {
                GameObject light = _preview.CreateGameObject("Directional Light");
                light.SetParent(selected, false);
                light.AddComponent(new DirectionalLight());
                _selectedPreviewObjectId = light.Id;
                _selectedComponent = null;
                MarkDirty();
            }
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Model..."))
        {
            AssetRecord[] models = _project!.AssetDatabase.Assets
                .Where(asset => asset.Type == AssetType.Model3D)
                .OrderBy(asset => asset.ProjectPath, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (models.Length == 0) ImGui.MenuItem("No model assets available", string.Empty, false, false);
            foreach (AssetRecord model in models)
            {
                string extension = Path.GetExtension(model.FullPath).ToLowerInvariant();
                bool supported = extension is ".fbx" or ".glb" or ".gltf" or ".obj";
                if (ImGui.MenuItem(Path.GetFileNameWithoutExtension(model.ProjectPath), string.Empty, false, supported))
                    AddModelAsset(model);
            }
            ImGui.EndMenu();
        }

        if (root != null && ImGui.BeginMenu("Setup"))
        {
            if (ImGui.MenuItem("Third Person Character")) SetupThirdPersonCharacter(root);
            ImGui.EndMenu();
        }
        ImGui.EndPopup();
    }

    private void SetupThirdPersonCharacter(GameObject root)
    {
        Camera3D? previousActive = root.Scene?.ActiveCamera;
        CameraBoom3D boom = BlueprintAuthoringService.SetupThirdPersonCharacter(root, _project!.Assets);
        Camera3D? camera = _preview!.FindGameObject(boom.CameraObjectId)?.GetComponent<Camera3D>();
        if (camera != null && previousActive != null && !ReferenceEquals(previousActive, camera))
        {
            _cameraActivationPrompt.Begin(CameraActivationPrompt.Create(camera, previousActive));
        }
        _selectedPreviewObjectId = root.Id;
        _selectedComponent = null;
        RefreshPreviewBoundsFromScene();
        MarkDirty();
    }

    private void DrawCameraActivationPrompt()
    {
        if (_cameraActivationPrompt.ConsumeOpenRequest())
        {
            ImGui.OpenPopup("Active Game Camera##BlueprintCameraPrompt");
        }
        if (_cameraActivationPrompt.Request is not { } request) return;
        if (!ImGui.BeginPopupModal("Active Game Camera##BlueprintCameraPrompt", ImGuiWindowFlags.AlwaysAutoResize))
        {
            _cameraActivationPrompt.RecoverWhenNotVisible();
            return;
        }
        _cameraActivationPrompt.MarkVisible();

        if (request.Kind == CameraActivationPromptKind.UseAsFirstCamera)
            ImGui.TextWrapped("Use this Camera as the Active Game Camera?");
        else
        {
            ImGui.TextUnformatted($"Active Game Camera: {request.PreviousActiveCamera?.GameObject.Name ?? "None"}");
            ImGui.TextWrapped("Make this Camera active instead?");
        }

        string accept = request.Kind == CameraActivationPromptKind.UseAsFirstCamera ? "Yes" : "Make Active";
        string reject = request.Kind == CameraActivationPromptKind.UseAsFirstCamera ? "No" : "Keep Current";
        if (ImGui.Button(accept))
        {
            CameraActivationPrompt.Apply(_preview, request, true);
            MarkDirty();
            _cameraActivationPrompt.Reset();
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button(reject))
        {
            _cameraActivationPrompt.Reset();
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    // ========================================================
    // HIERARCHY EDITING
    // ========================================================

    private void AddChildToSelected()
    {
        GameObject? parent =
            GetSelectedPreviewObject();

        if (parent ==
            null)
        {
            return;
        }

        GameObject child =
            _preview!.CreateGameObject(
                "GameObject");

        child.SetParent(
            parent,
            false);

        _selectedPreviewObjectId =
            child.Id;

        MarkDirty();
    }

    private void DeleteSelectedObject()
    {
        GameObject? selected =
            GetSelectedPreviewObject();

        if (selected ==
                null ||
            selected.Parent ==
                null)
        {
            return;
        }

        GameObject parent =
            selected.Parent;

        _preview!.DestroyGameObject(
            selected);

        _selectedPreviewObjectId =
            parent.Id;

        MarkDirty();
    }

    // ========================================================
    // MODEL ASSETS
    // ========================================================

    private void AddModelAsset(
        AssetRecord asset)
    {
        if (_project ==
                null ||
            _preview ==
                null)
        {
            return;
        }

        string extension =
            Path.GetExtension(
                asset.FullPath)
            .ToLowerInvariant();

        GameObject? container =
            null;

        try
        {
            var reference =
                new AssetReference(
                    asset.Guid,
                    asset.ProjectPath);

            /*
             * Load and validate the model before creating any Blueprint
             * GameObjects. If import fails, the Blueprint hierarchy remains
             * untouched instead of being left half-created.
             */
            ModelAsset model =
                _project.Assets.LoadModel(
                    reference);

            container =
                _preview.CreateGameObject(
                    model.Name);

            GameObject? selected = GetSelectedPreviewObject();
            GameObject? blueprintRoot = _preview.GameObjects.FirstOrDefault(item => item.Parent == null);
            GameObject visualParent = _blueprint!.Type == BlueprintType.Character && blueprintRoot != null
                ? BlueprintAuthoringService.EnsureVisualRoot(blueprintRoot)
                : selected ?? blueprintRoot ?? container;
            if (!ReferenceEquals(visualParent, container)) container.SetParent(visualParent, false);

            ModelScaleAnalysis scaleAnalysis =
                ModelImportScaleUtility.Analyze(
                    asset,
                    model);

            float appliedScale =
                scaleAnalysis.AppliedScale;

            if (_blueprint.Type == BlueprintType.Character && visualParent.Name.Equals("Visual", StringComparison.OrdinalIgnoreCase))
            {
                visualParent.Transform.LocalScale = Vector3.One * appliedScale;
                container.Transform.LocalScale = Vector3.One;
            }
            else container.Transform.LocalScale = Vector3.One * appliedScale;

            container.AddComponent(
                new ModelHierarchyInstance
                {
                    Model =
                        reference,

                    AppliedImportScale =
                        appliedScale
                });

            var objects =
                new Dictionary<string, GameObject>();

            foreach (ImportedNode node
                     in model.Nodes)
            {
                GameObject gameObject =
                    _preview.CreateGameObject(
                        node.Name);

                objects[node.Key] =
                    gameObject;
            }

            foreach (ImportedNode node
                     in model.Nodes)
            {
                GameObject gameObject =
                    objects[node.Key];

                GameObject parent =
                    node.ParentKey !=
                        null &&
                    objects.TryGetValue(
                        node.ParentKey,
                        out GameObject? nodeParent)
                        ? nodeParent
                        : container;

                gameObject.SetParent(
                    parent,
                    false);

                ApplyLocalTransform(
                    gameObject,
                    node.LocalTransform);

                for (int index =
                         0;
                     index <
                     node.MeshKeys.Count;
                     index++)
                {
                    string meshKey =
                        node.MeshKeys[index];

                    ImportedMesh importedMesh =
                        model.Meshes.First(
                            mesh =>
                                mesh.Key ==
                                meshKey);

                    GameObject meshObject =
                        index ==
                            0 &&
                        node.MeshKeys.Count ==
                            1
                            ? gameObject
                            : CreateMeshChild(
                                gameObject,
                                importedMesh.Name);

                    var renderer =
                        new MeshRenderer
                        {
                            MeshReference =
                                new ModelMeshReference(
                                    reference,
                                    meshKey),

                            Mesh =
                                _project.Assets.GetModelMesh(
                                    reference,
                                    meshKey)
                        };

                    if (importedMesh.MaterialKey !=
                        null)
                    {
                        renderer.MaterialReference =
                            new ModelMaterialReference(
                                reference,
                                importedMesh.MaterialKey);

                        renderer.Material =
                            _project.Assets.GetModelMaterial(
                                reference,
                                importedMesh.MaterialKey);
                    }

                    meshObject.AddComponent(
                        renderer);
                }
            }

            UpdatePreviewBounds(
                model,
                objects);

            FramePreviewBounds();

            _selectedPreviewObjectId =
                container.Id;

            string scaleNote =
                scaleAnalysis.Normalized
                    ? $" | {scaleAnalysis.Summary}"
                    : string.Empty;

            _statusMessage =
                $"Added {Path.GetFileName(asset.ProjectPath)} ({model.Meshes.Count} mesh(es)){scaleNote}";

            _statusIsError =
                false;

            MarkDirty();

            WriteDebugDump(
                $"Model added: {asset.ProjectPath}",
                model,
                asset);
        }
        catch (Exception exception)
        {
            if (container !=
                null)
            {
                _preview.DestroyGameObject(
                    container);
            }

            _statusMessage =
                $"Could not add model: {exception.Message}";

            _statusIsError =
                true;

            WriteDebugDump(
                $"Model add failed: {asset.ProjectPath}\n{exception}");
        }
    }

    private void UpdatePreviewBounds(
        ModelAsset model,
        IReadOnlyDictionary<string, GameObject> objects)
    {
        Vector3 minimum =
            new(
                float.PositiveInfinity,
                float.PositiveInfinity,
                float.PositiveInfinity);

        Vector3 maximum =
            new(
                float.NegativeInfinity,
                float.NegativeInfinity,
                float.NegativeInfinity);

        bool foundVertex =
            false;

        Dictionary<string, ImportedMesh> meshes =
            model.Meshes
                .ToDictionary(
                    mesh =>
                        mesh.Key,
                    StringComparer.Ordinal);

        foreach (ImportedNode node
                 in model.Nodes)
        {
            if (!objects.TryGetValue(
                    node.Key,
                    out GameObject? gameObject))
            {
                continue;
            }

            Matrix4x4 world =
                gameObject.Transform.WorldMatrix;

            foreach (string meshKey
                     in node.MeshKeys)
            {
                if (!meshes.TryGetValue(
                        meshKey,
                        out ImportedMesh? mesh))
                {
                    continue;
                }

                float[] vertices =
                    mesh.Vertices;

                for (int index =
                         0;
                     index +
                     2 <
                     vertices.Length;
                     index +=
                     8)
                {
                    Vector3 local =
                        new(
                            vertices[index],
                            vertices[index + 1],
                            vertices[index + 2]);

                    Vector3 worldPosition =
                        Vector3.Transform(
                            local,
                            world);

                    if (!float.IsFinite(
                            worldPosition.X) ||
                        !float.IsFinite(
                            worldPosition.Y) ||
                        !float.IsFinite(
                            worldPosition.Z))
                    {
                        continue;
                    }

                    minimum =
                        Vector3.Min(
                            minimum,
                            worldPosition);

                    maximum =
                        Vector3.Max(
                            maximum,
                            worldPosition);

                    foundVertex =
                        true;
                }
            }
        }

        if (!foundVertex)
        {
            _previewBoundsMin =
                null;

            _previewBoundsMax =
                null;

            return;
        }

        _previewBoundsMin =
            minimum;

        _previewBoundsMax =
            maximum;
    }

    private void FramePreviewBounds()
    {
        if (!_previewBoundsMin.HasValue ||
            !_previewBoundsMax.HasValue)
        {
            return;
        }

        Vector3 minimum =
            _previewBoundsMin.Value;

        Vector3 maximum =
            _previewBoundsMax.Value;

        Vector3 center =
            (
                minimum +
                maximum
            ) *
            0.5f;

        Vector3 size =
            maximum -
            minimum;

        float largestDimension =
            Math.Max(
                Math.Max(
                    MathF.Abs(
                        size.X),
                    MathF.Abs(
                        size.Y)),
                MathF.Abs(
                    size.Z));

        if (!float.IsFinite(
                largestDimension) ||
            largestDimension <
                0.001f)
        {
            largestDimension =
                1.0f;
        }

        _camera.Yaw =
            -135.0f;

        _camera.Pitch =
            -18.0f;

        /*
         * Frame the model from its actual bounds instead of forcing a
         * two-unit minimum camera distance. FBX assets often arrive with
         * import scales such as 0.01/0.012, so a valid character can be only
         * a few centimeters wide in engine units. The old 2.0f minimum made
         * those models effectively invisible even though they were rendering.
         *
         * Fit a bounding sphere to the camera FOV and keep the nearest point
         * safely in front of EditorCamera3D's 0.05 near plane.
         */
        float radius =
            Math.Max(
                size.Length() *
                0.5f,
                largestDimension *
                0.5f);

        float halfFovRadians =
            _camera.FieldOfView *
            MathF.PI /
            360.0f;

        float fitDistance =
            radius /
            Math.Max(
                MathF.Tan(
                    halfFovRadians),
                0.01f);

        float distance =
            Math.Max(
                fitDistance *
                1.35f,
                radius +
                0.065f);

        _camera.Position =
            center -
            _camera.Forward *
            distance;
    }

    private Vector3 GetPreviewFocus()
    {
        if (_previewBoundsMin.HasValue &&
            _previewBoundsMax.HasValue)
        {
            return (
                _previewBoundsMin.Value +
                _previewBoundsMax.Value
            ) *
            0.5f;
        }

        return GetSelectedPreviewObject()?
            .Transform
            .WorldPosition ??
            Vector3.Zero;
    }

    private GameObject CreateMeshChild(
        GameObject parent,
        string name)
    {
        GameObject child =
            _preview!.CreateGameObject(
                name);

        child.SetParent(
            parent,
            false);

        return child;
    }

    private static void ApplyLocalTransform(
        GameObject gameObject,
        Matrix4x4 matrix)
    {
        if (!Matrix4x4.Decompose(
                matrix,
                out Vector3 scale,
                out Quaternion rotation,
                out Vector3 translation))
        {
            return;
        }

        gameObject.Transform.LocalPosition =
            translation;

        gameObject.Transform.LocalRotation =
            rotation;

        gameObject.Transform.LocalScale =
            scale;
    }

    // ========================================================
    // MODEL REIMPORT / NORMALIZATION
    // ========================================================

    private void NormalizeExistingModelScales()
    {
        if (_project ==
                null ||
            _preview ==
                null)
        {
            return;
        }

        int changed =
            0;

        foreach (ModelHierarchyInstance instance
                 in _preview.GameObjects
                     .SelectMany(
                         gameObject =>
                             gameObject.Components
                                 .OfType<ModelHierarchyInstance>()))
        {
            if (!_project.AssetDatabase.TryGetAsset(
                    instance.Model.Guid,
                    out AssetRecord? asset) ||
                asset ==
                    null ||
                asset.Type !=
                    AssetType.Model3D)
            {
                continue;
            }

            var reference =
                new AssetReference(
                    asset.Guid,
                    asset.ProjectPath);

            ModelAsset model =
                _project.Assets.LoadModel(
                    reference);

            ModelScaleAnalysis analysis =
                ModelImportScaleUtility.Analyze(
                    asset,
                    model);

            Vector3 desired =
                Vector3.One *
                analysis.AppliedScale;

            GameObject container =
                instance.GameObject;

            if (Vector3.DistanceSquared(
                    container.Transform.LocalScale,
                    desired) <
                0.0000001f)
            {
                continue;
            }

            container.Transform.LocalScale =
                desired;

            instance.AppliedImportScale =
                analysis.AppliedScale;

            changed++;
        }

        RefreshPreviewBoundsFromScene();

        FramePreviewBounds();

        if (changed >
            0)
        {
            MarkDirty();

            _statusMessage =
                $"Normalized {changed} model hierarchy(s). Save Blueprint to keep the correction.";
        }
        else
        {
            _statusMessage =
                "No model scale changes were needed.";
        }

        _statusIsError =
            false;
    }

    private void ReimportReferencedModels()
    {
        if (_project ==
                null ||
            _preview ==
                null)
        {
            return;
        }

        Guid[] modelGuids =
            _preview.GameObjects
                .SelectMany(
                    gameObject =>
                        gameObject.Components
                            .OfType<MeshRenderer>())
                .Select(
                    renderer =>
                        renderer.MeshReference?
                            .Model
                            .Guid ??
                        Guid.Empty)
                .Where(
                    guid =>
                        guid !=
                        Guid.Empty)
                .Distinct()
                .ToArray();

        int refreshed =
            0;

        foreach (Guid guid
                 in modelGuids)
        {
            _project.Assets.ReimportModel(
                guid);

            refreshed++;
        }

        RefreshPreviewBoundsFromScene();

        FramePreviewBounds();

        _statusMessage =
            refreshed ==
                0
                ? "No referenced models found."
                : $"Reimported {refreshed} model asset(s).";

        _statusIsError =
            false;
    }

    // ========================================================
    // MODEL DEBUG DUMP
    // ========================================================

    private void WriteDebugDump(
        string reason,
        ModelAsset? importedModel = null,
        AssetRecord? importedAsset = null)
    {
        if (_project ==
                null ||
            _preview ==
                null)
        {
            return;
        }

        try
        {
            _lastDebugDumpPath =
                BlueprintModelDebugLog.Write(
                    _project,
                    _preview,
                    _blueprint,
                    _asset,
                    _camera,
                    _previewBoundsMin,
                    _previewBoundsMax,
                    reason,
                    importedModel,
                    importedAsset);

            _statusMessage =
                $"Debug dump written: {Path.GetFileName(_lastDebugDumpPath)}";

            _statusIsError =
                false;
        }
        catch (Exception exception)
        {
            _statusMessage =
                $"Debug dump failed: {exception.Message}";

            _statusIsError =
                true;
        }
    }

    // ========================================================
    // SAVE / LOAD
    // ========================================================

    private void SaveBlueprint()
    {
        EvictDeletedAsset();
        if (_project ==
                null ||
            _preview ==
                null ||
            _blueprint ==
                null ||
            _asset ==
                null)
        {
            return;
        }

        SceneData sceneData =
            _project.Scenes.Serialize(
                _preview);

        GameObject? rootObject =
            _preview.GameObjects
                .FirstOrDefault(
                    gameObject =>
                        gameObject.Parent ==
                        null);

        if (rootObject ==
            null)
        {
            return;
        }

        GameObjectData? rootData =
            sceneData.GameObjects
                .FirstOrDefault(
                    data =>
                        data.Id ==
                        rootObject.Id);

        if (rootData ==
            null)
        {
            return;
        }

        rootData.ParentId =
            null;

        _blueprint.Name =
            rootObject.Name;

        _blueprint.Root =
            rootData;

        _blueprint.Children =
            sceneData.GameObjects
                .Where(
                    data =>
                        data.Id !=
                        rootObject.Id)
                .ToList();

        _blueprint.Variables =
            rootData.Variables
                .Select(
                    variable =>
                        new VariableData
                        {
                            Name =
                                variable.Name,

                            Value =
                                variable.Value.Clone()
                        })
                .ToList();

        EventModuleComponent? rootRunner =
            rootObject.GetComponent<EventModuleComponent>();

        _blueprint.EventModules =
            rootRunner?.Modules
                .Where(
                    reference =>
                        reference.Guid !=
                        Guid.Empty)
                .Select(
                    reference =>
                        reference.Guid)
                .Distinct()
                .ToList()
            ?? new List<Guid>();

        new BlueprintSerializer()
            .Save(
                _blueprint,
                _asset.FullPath);

        _project.AssetDatabase.Scan();
        if (EditorState.Active is { } editorState)
        {
            Guid? selectedObjectId = editorState.SelectedObject?.Id;
            BlueprintInstanceSynchronizer.Propagate(
                _project,
                editorState.EditorScene,
                new AssetReference(_asset.Guid, _asset.ProjectPath));
            if (selectedObjectId.HasValue) editorState.SelectedObject = editorState.EditorScene.FindGameObject(selectedObjectId.Value);
            editorState.MarkDirty();
        }

        _project.AssetDatabase.RequestRefresh();

        _statusMessage =
            "Blueprint saved.";

        _statusIsError =
            false;

        _dirty =
            false;
    }

    private void RefreshPreviewBoundsFromScene()
    {
        if (_project ==
                null ||
            _preview ==
                null)
        {
            return;
        }

        Vector3 minimum =
            new(
                float.PositiveInfinity,
                float.PositiveInfinity,
                float.PositiveInfinity);

        Vector3 maximum =
            new(
                float.NegativeInfinity,
                float.NegativeInfinity,
                float.NegativeInfinity);

        bool foundVertex =
            false;

        foreach (GameObject gameObject
                 in _preview.GameObjects)
        {
            foreach (MeshRenderer renderer
                     in gameObject.Components
                         .OfType<MeshRenderer>())
            {
                ModelMeshReference? meshReference =
                    renderer.MeshReference;

                if (meshReference ==
                    null)
                {
                    continue;
                }

                try
                {
                    ModelAsset model =
                        _project.Assets.LoadModel(
                            meshReference.Model);

                    ImportedMesh? mesh =
                        model.Meshes
                            .FirstOrDefault(
                                item =>
                                    item.Key ==
                                    meshReference.SubAssetKey);

                    if (mesh ==
                        null)
                    {
                        continue;
                    }

                    Matrix4x4 world =
                        gameObject.Transform.WorldMatrix;

                    float[] vertices =
                        mesh.Vertices;

                    for (int index =
                             0;
                         index +
                         2 <
                         vertices.Length;
                         index +=
                         8)
                    {
                        Vector3 worldPosition =
                            Vector3.Transform(
                                new Vector3(
                                    vertices[index],
                                    vertices[index + 1],
                                    vertices[index + 2]),
                                world);

                        if (!float.IsFinite(
                                worldPosition.X) ||
                            !float.IsFinite(
                                worldPosition.Y) ||
                            !float.IsFinite(
                                worldPosition.Z))
                        {
                            continue;
                        }

                        minimum =
                            Vector3.Min(
                                minimum,
                                worldPosition);

                        maximum =
                            Vector3.Max(
                                maximum,
                                worldPosition);

                        foundVertex =
                            true;
                    }
                }
                catch
                {
                    /*
                     * A missing/broken model reference should not prevent the
                     * Blueprint editor opening. The Inspector will still show
                     * the component and asset reference for repair.
                     */
                }
            }
        }

        if (!foundVertex)
        {
            _previewBoundsMin =
                null;

            _previewBoundsMax =
                null;

            return;
        }

        _previewBoundsMin =
            minimum;

        _previewBoundsMax =
            maximum;
    }

    private void RebuildPreview()
    {
        _previewBoundsMin =
            null;

        _previewBoundsMax =
            null;

        if (_project ==
                null ||
            _blueprint ==
                null)
        {
            return;
        }

        var objects =
            new List<GameObjectData>
            {
                _blueprint.Root
            };

        objects.AddRange(
            _blueprint.Children);

        _preview =
            _project.Scenes.Deserialize(
                new SceneData
                {
                    Name =
                        _blueprint.Name +
                        " Preview",

                    SceneId =
                        Guid.NewGuid(),

                    GameObjects =
                        objects
                });

        GameObject? firstRoot =
            _preview.GameObjects
                .FirstOrDefault(
                    gameObject =>
                        gameObject.Parent ==
                        null);

        _selectedPreviewObjectId =
            firstRoot?.Id ??
            Guid.Empty;

        RefreshPreviewBoundsFromScene();

        FramePreviewBounds();

        WriteDebugDump(
            "Blueprint preview rebuilt");
    }

    private GameObject? GetSelectedPreviewObject()
    {
        if (_preview ==
            null)
        {
            return null;
        }

        if (_selectedPreviewObjectId !=
            Guid.Empty)
        {
            GameObject? selected =
                _preview.FindGameObject(
                    _selectedPreviewObjectId);

            if (selected !=
                null)
            {
                return selected;
            }
        }

        GameObject? root =
            _preview.GameObjects
                .FirstOrDefault(
                    gameObject =>
                        gameObject.Parent ==
                        null);

        if (root !=
            null)
        {
            _selectedPreviewObjectId =
                root.Id;
        }

        return root;
    }

    private void MarkDirty()
    {
        _dirty =
            true;
    }

    public void Dispose()
    {
        if (_project != null) _project.AssetDatabase.DatabaseChanged -= EvictDeletedAsset;
        _framebuffer.Dispose();
    }
}
