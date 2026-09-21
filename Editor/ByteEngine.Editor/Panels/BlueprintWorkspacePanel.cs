using System.Numerics;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Classification;
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
    private readonly EditorDocumentManager _documents;

    public BlueprintWorkspacePanel()
        : this(new EditorDocumentManager())
    {
    }

    public BlueprintWorkspacePanel(EditorDocumentManager documents)
    {
        _documents = documents;
    }
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
    private Guid _addComponentTargetId = Guid.Empty;
    private readonly BlueprintTransformGizmo3D _transformGizmo = new();
    private readonly CameraActivationPromptState _cameraActivationPrompt = new();

    private bool _open;
    private bool _focusNextDraw;
    private bool _openUnsavedPopup;
    private EditorDocumentId? _registeredDocumentId;
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
        _addComponentTargetId = Guid.Empty;
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

        if (_asset?.Guid == asset.Guid && _open)
        {
            _focusNextDraw = true;
            if (_registeredDocumentId.HasValue) _documents.Activate(_registeredDocumentId.Value);
            return;
        }

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

        RegisterDocument();
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

        if (_focusNextDraw)
        {
            ImGui.SetNextWindowFocus();
            _focusNextDraw = false;
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

        bool open = true;
        bool visible = ImGui.Begin(
            $"Blueprint: {_blueprint.Name}{dirtyMarker}###BlueprintWorkspace",
            ref open,
            ImGuiWindowFlags.NoScrollbar |
            ImGuiWindowFlags.NoScrollWithMouse |
            (_transformGizmo.OwnsMouse ? ImGuiWindowFlags.NoMove : ImGuiWindowFlags.None));

        if (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) &&
            _registeredDocumentId.HasValue)
            _documents.Activate(_registeredDocumentId.Value);

        if (!open) RequestClose();
        if (!visible)
        {
            ImGui.End();
            DrawUnsavedPopup();
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
        DrawUnsavedPopup();
    }

    // ========================================================
    // TOOLBAR
    // ========================================================

    private void DrawToolbar()
    {
        EditorUi.BeginToolbar("##BlueprintCommandBar");

        ImGui.BeginDisabled(!_dirty);
        if (_dirty)
            EditorUi.PrimaryButton("Save", SaveBlueprint);
        else
            EditorUi.ToolbarButton("Save", "Save Blueprint");
        ImGui.EndDisabled();

        EditorUi.ToolbarSeparator();
        if (EditorUi.PrimaryButton("+ Add")) ImGui.OpenPopup("Blueprint Add Menu");
        DrawBlueprintAddMenu();

        EditorUi.ToolbarSeparator();
        ImGui.BeginDisabled(_propertyUndo.Count == 0);
        if (EditorUi.ToolbarButton("Undo", "Undo property edit")) RestorePropertyEdit(false);
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(_propertyRedo.Count == 0);
        if (EditorUi.ToolbarButton("Redo", "Redo property edit")) RestorePropertyEdit(true);
        ImGui.EndDisabled();

        EditorUi.ToolbarSeparator();
        _transformGizmo.DrawToolbar();

        EditorUi.ToolbarSeparator();
        ImGui.BeginDisabled(!_previewBoundsMin.HasValue || !_previewBoundsMax.HasValue);
        if (EditorUi.ToolbarButton("Frame", "Frame Blueprint bounds")) FramePreviewBounds();
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (EditorUi.ToolbarButton("...", "More Blueprint actions"))
            ImGui.OpenPopup("More##BlueprintToolbarMore");
        if (ImGui.BeginPopup("More##BlueprintToolbarMore"))
        {
            bool canDelete = GetSelectedPreviewObject()?.Parent != null;
            ImGui.BeginDisabled(!canDelete);
            if (ImGui.MenuItem("Delete Object##BlueprintDelete")) DeleteSelectedObject();
            ImGui.EndDisabled();
            ImGui.Separator();
            if (ImGui.MenuItem("Normalize Models##BlueprintNormalize")) NormalizeExistingModelScales();
            if (ImGui.MenuItem("Reimport Models##BlueprintReimport")) ReimportReferencedModels();
            if (ImGui.MenuItem("Write Debug Dump##BlueprintDebug"))
                WriteDebugDump("Manual Blueprint debug dump");
            ImGui.Separator();
            if (ImGui.MenuItem("Close")) RequestClose();
            if (_registeredDocumentId.HasValue)
            {
                if (ImGui.MenuItem("Close Others"))
                    _documents.RequestCloseOthers(_registeredDocumentId.Value);
                if (ImGui.MenuItem("Close All")) _documents.RequestCloseAll();
            }
            ImGui.EndPopup();
        }

        if (_dirty)
        {
            ImGui.SameLine();
            EditorUi.StatusBadge("UNSAVED", EditorStatusKind.Warning);
        }
        if (!string.IsNullOrWhiteSpace(_statusMessage))
        {
            ImGui.SameLine();
            EditorUi.StatusBadge(_statusMessage, _statusIsError ? EditorStatusKind.Error : EditorStatusKind.Success);
        }

        EditorUi.EndToolbar();
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

        EditorUi.SectionHeader("Objects");

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

        if (EditorUi.SectionHeader("Blueprint"))
        {
            int type = (int)_blueprint!.Type;
            EditorUi.PropertyRow("Type", () =>
            {
                if (ImGui.Combo("##BlueprintType", ref type, "Generic Object\0Character\0"))
                {
                    _blueprint.Type = (BlueprintType)type;
                    MarkDirty();
                }
            });
        }

        if (EditorUi.SectionHeader("Advanced", defaultOpen: false))
        {
            EditorUi.LabelValue("Source", _asset!.ProjectPath);
            EditorUi.LabelValue("GUID", _asset.Guid.ToString());
        }

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

            Guid? assetId = AssetDragDrop.Accept();
            if (assetId.HasValue &&
                _project!.AssetDatabase.TryGetAsset(assetId.Value, out AssetRecord? asset) &&
                asset?.Type == AssetType.Model3D)
            {
                _selectedPreviewObjectId = gameObject.Id;
                AddModelAsset(asset);
            }
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

        if (ImGui.BeginDragDropTarget())
        {
            Guid? assetId = AssetDragDrop.Accept();
            if (assetId.HasValue &&
                _project!.AssetDatabase.TryGetAsset(assetId.Value, out AssetRecord? asset) &&
                asset?.Type == AssetType.Model3D)
            {
                AddModelAsset(asset);
            }

            ImGui.EndDragDropTarget();
        }

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

            if (ImGui.IsMouseDragging(ImGuiMouseButton.Middle))
            {
                Vector2 delta = io.MouseDelta;
                Vector3 focus = GetPreviewFocus();
                float distance = Vector3.Distance(_camera.Position, focus);
                float panScale = Math.Max(distance * 0.0018f, 0.0025f);
                Vector3 cameraUp = Vector3.Normalize(Vector3.Cross(_camera.Right, _camera.Forward));
                _camera.Position += (-_camera.Right * delta.X + cameraUp * delta.Y) * panScale;
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

        ImGui.TextColored(EditorTheme.TextSecondary, "DETAILS");

        GameObject? selected =
            GetSelectedPreviewObject();

        if (selected ==
            null)
        {
            EditorUi.EmptyState("Nothing selected",
                "Select an object in the Blueprint to edit its properties.");

            ImGui.EndChild();

            return;
        }

        DrawObjectHeader(
            selected);

        DrawClassification(
            selected);

        DrawAttachment(selected);

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
        if (!EditorUi.SectionHeader("GameObject")) return;

        string name = selected.Name;
        EditorUi.PropertyRow("Name", () =>
        {
            if (ImGui.InputText("##BlueprintObjectName", ref name, 256))
            {
                selected.Name = string.IsNullOrWhiteSpace(name) ? "GameObject" : name;
                if (selected.Parent == null) _blueprint!.Name = selected.Name;
                MarkDirty();
            }
        });

        bool active = selected.Active;
        EditorUi.PropertyRow("Active", () =>
        {
            if (ImGui.Checkbox("##BlueprintObjectActive", ref active))
            {
                selected.Active = active;
                MarkDirty();
            }
        });

        EditorUi.LabelValue("Parent", selected.Parent == null ? "Blueprint Root" : selected.Parent.Name);
    }

    private void DrawClassification(
        GameObject selected)
    {
        if (_project ==
            null)
        {
            return;
        }

        ClassificationSettings settings =
            _project.Project.Classification;

        ImGui.SeparatorText(
            "CLASSIFICATION");

        ImGui.TextUnformatted(
            "Tags");

        Guid[] assignedTags =
            selected.Tags.ToArray();

        if (assignedTags.Length ==
            0)
        {
            ImGui.TextDisabled(
                "None");
        }

        foreach (Guid tagId
                 in assignedTags)
        {
            TagDefinition? tag =
                settings.FindTag(
                    tagId);

            if (tag ==
                null)
            {
                ImGui.TextColored(
                    new Vector4(
                        1.0f,
                        0.65f,
                        0.20f,
                        1.0f),
                    $"Missing Tag: {tagId}");
            }
            else
            {
                ImGui.TextUnformatted(
                    tag.Name);
            }

            ImGui.SameLine();

            if (ImGui.SmallButton(
                    $"x##BlueprintRemoveTag:{tagId}"))
            {
                ApplyClassificationChange(
                    () =>
                        selected.RemoveTag(
                            tagId));
            }
        }

        ImGui.SetNextItemWidth(
            -1.0f);

        if (ImGui.BeginCombo(
                "##BlueprintAddTag",
                "+ Add Tag"))
        {
            TagDefinition[] available =
                settings.Tags
                    .Where(
                        tag =>
                            !selected.HasTag(
                                tag.Id))
                    .OrderBy(
                        tag =>
                            tag.Name,
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray();

            if (available.Length ==
                0)
            {
                ImGui.TextDisabled(
                    "No unassigned Tags.");
            }

            foreach (TagDefinition tag
                     in available)
            {
                if (ImGui.Selectable(
                        $"{tag.Name}##BlueprintAddTag:{tag.Id}"))
                {
                    Guid tagId =
                        tag.Id;

                    ApplyClassificationChange(
                        () =>
                            selected.AddTag(
                                tagId));
                }
            }

            ImGui.EndCombo();
        }

        int layer =
            selected.Layer;

        if (ClassificationPickers.DrawLayer(
                "Layer",
                settings,
                ref layer))
        {
            int selectedLayer =
                layer;

            ApplyClassificationChange(
                () =>
                    selected.Layer =
                        selectedLayer);
        }
    }

    private void ApplyClassificationChange(
        Action change)
    {
        BeginPropertyEdit();

        change();

        MarkDirty();

        CommitPropertyEdit();
    }

    private void DrawAttachment(GameObject selected)
    {
        if (selected.Parent == null || !EditorUi.SectionHeader("Attachment")) return;
        GameObject parent = selected.Parent;
        EditorUi.LabelValue("Parent", parent.Name);
        IReadOnlyList<SkeletalSocketDefinition> sockets = SkeletalSocketResolver.GetSockets(parent);
        bool socketExists = sockets.Any(item => string.Equals(item.Name, selected.ParentSocket, StringComparison.OrdinalIgnoreCase));
        string preview = string.IsNullOrWhiteSpace(selected.ParentSocket) ? "None" : socketExists ? selected.ParentSocket : selected.ParentSocket + " (Missing)";
        if (ImGui.BeginCombo("Parent Socket", preview))
        {
            if (ImGui.Selectable("None", string.IsNullOrWhiteSpace(selected.ParentSocket)))
            {
                selected.ParentSocket = string.Empty; MarkDirty();
            }
            foreach (SkeletalSocketDefinition socket in sockets)
            {
                if (ImGui.Selectable($"{socket.Name}##{socket.Id}", string.Equals(socket.Name, selected.ParentSocket, StringComparison.OrdinalIgnoreCase)))
                {
                    if (!SkeletalAttachmentService.AttachToSocket(selected, parent, socket.Name, selected.AttachmentLocationRule, selected.AttachmentRotationRule, selected.AttachmentScaleRule)) selected.ParentSocket = socket.Name;
                    MarkDirty();
                }
            }
            ImGui.EndCombo();
        }
        AttachmentTransformRule locationRule=selected.AttachmentLocationRule, rotationRule=selected.AttachmentRotationRule, scaleRule=selected.AttachmentScaleRule;
        bool changed = DrawAttachmentRule("Location Rule", ref locationRule);
        changed |= DrawAttachmentRule("Rotation Rule", ref rotationRule);
        changed |= DrawAttachmentRule("Scale Rule", ref scaleRule);
        selected.AttachmentLocationRule=locationRule; selected.AttachmentRotationRule=rotationRule; selected.AttachmentScaleRule=scaleRule;
        Vector3 offsetPosition=selected.AttachmentPosition, offsetScale=selected.AttachmentScale;
        Vector3 offsetRotation=QuaternionToEuler(selected.AttachmentRotation);
        if(ImGui.DragFloat3("Socket Position Offset",ref offsetPosition,.01f)){selected.AttachmentPosition=offsetPosition;changed=true;}
        if(ImGui.DragFloat3("Socket Rotation Offset",ref offsetRotation,.25f)){selected.AttachmentRotation=Quaternion.CreateFromYawPitchRoll(offsetRotation.Y*MathF.PI/180f,offsetRotation.X*MathF.PI/180f,offsetRotation.Z*MathF.PI/180f);changed=true;}
        if(ImGui.DragFloat3("Socket Scale Offset",ref offsetScale,.01f,.0001f,1000f)){selected.AttachmentScale=offsetScale;changed=true;}
        if(changed){SkeletalAttachmentService.Apply(selected);MarkDirty();}
    }

    private static bool DrawAttachmentRule(string label, ref AttachmentTransformRule rule)
    {
        bool changed=false;
        if(ImGui.BeginCombo(label, rule switch { AttachmentTransformRule.SnapToTarget=>"Snap To Target",AttachmentTransformRule.KeepWorld=>"Keep World",_=>"Keep Relative" }))
        {
            foreach(AttachmentTransformRule candidate in Enum.GetValues<AttachmentTransformRule>()) if(ImGui.Selectable(candidate switch { AttachmentTransformRule.SnapToTarget=>"Snap To Target",AttachmentTransformRule.KeepWorld=>"Keep World",_=>"Keep Relative" },candidate==rule)){rule=candidate;changed=true;}
            ImGui.EndCombo();
        }
        return changed;
    }

    private static Vector3 QuaternionToEuler(Quaternion q)
    {
        q=Quaternion.Normalize(q); float x=MathF.Asin(Math.Clamp(2f*(q.W*q.X-q.Y*q.Z),-1f,1f));
        float y=MathF.Atan2(2f*(q.W*q.Y+q.X*q.Z),1f-2f*(q.X*q.X+q.Y*q.Y));
        float z=MathF.Atan2(2f*(q.W*q.Z+q.X*q.Y),1f-2f*(q.X*q.X+q.Z*q.Z));
        return new(x*180f/MathF.PI,y*180f/MathF.PI,z*180f/MathF.PI);
    }
    private void DrawTransform(
        GameObject selected)
    {
        if (!EditorUi.SectionHeader("Transform")) return;

        Vector3 position = selected.Transform.LocalPosition;
        EditorUi.PropertyRow("Position", () =>
        {
            if (ImGui.DragFloat3("##BlueprintPosition", ref position, 0.05f))
            {
                selected.Transform.LocalPosition = position;
                MarkDirty();
            }
        });

        Vector3 rotation = selected.Transform.EulerAngles;
        EditorUi.PropertyRow("Rotation", () =>
        {
            if (ImGui.DragFloat3("##BlueprintRotation", ref rotation, 0.25f))
            {
                selected.Transform.EulerAngles = rotation;
                MarkDirty();
            }
        });

        Vector3 scale = selected.Transform.LocalScale;
        EditorUi.PropertyRow("Scale", () =>
        {
            if (ImGui.DragFloat3("##BlueprintScale", ref scale, 0.02f, 0.001f, 10000.0f))
            {
                selected.Transform.LocalScale = Vector3.Max(scale, new Vector3(0.001f));
                MarkDirty();
            }
        });
    }

    private void DrawComponents(
        GameObject selected)
    {
        if (!EditorUi.SectionHeader("Components")) return;
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

        if (EditorUi.SecondaryButton($"+ Add Component to \"{selected.Name}\""))
        {
            _addComponentTargetId = selected.Id;
            _addComponentSearch = string.Empty;
            ImGui.OpenPopup("BlueprintAddComponentPopup");
        }

        DrawComponentAddPopup();
    }

    private void DrawComponentAddPopup()
    {
        if (!ImGui.BeginPopup("BlueprintAddComponentPopup"))
        {
            _addComponentTargetId = Guid.Empty;
            return;
        }

        GameObject? target = _addComponentTargetId == Guid.Empty
            ? null
            : _preview?.FindGameObject(_addComponentTargetId);

        if (target == null)
        {
            ImGui.TextDisabled("The target object is no longer available.");
        }
        else
        {
            ImGui.TextDisabled("ADD COMPONENT TO");
            ImGui.TextUnformatted(target.Name);
            ImGui.Separator();
            ImGui.InputTextWithHint(
                "##BlueprintInspectorAddComponentSearch",
                "Search components...",
                ref _addComponentSearch,
                96);
            ComponentAddMenu.Draw(target, _addComponentSearch, (component, _) =>
            {
                Camera3D? previousActive = component is Camera3D ? target.Scene?.ActiveCamera : null;
                Component added = BlueprintAuthoringService.AddComponent(target, component);
                if (added is Camera3D camera)
                    _cameraActivationPrompt.Begin(CameraActivationPrompt.Create(camera, previousActive));
                MarkDirty();
            });
        }

        ImGui.EndPopup();
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
        if (EditorUi.PrimaryButton(accept))
        {
            CameraActivationPrompt.Apply(_preview, request, true);
            MarkDirty();
            _cameraActivationPrompt.Reset();
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (EditorUi.SecondaryButton(reject))
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

    private void RegisterDocument()
    {
        if (_asset == null) return;
        EditorDocumentId id = new(EditorDocumentType.Blueprint, _asset.Guid.ToString("N"));
        if (_registeredDocumentId.HasValue && _registeredDocumentId.Value != id)
            _documents.Unregister(_registeredDocumentId.Value);
        _registeredDocumentId = id;
        _documents.RegisterOrFocus(new EditorDocument(
            id,
            $"Blueprint: {_blueprint?.Name ?? Path.GetFileNameWithoutExtension(_asset.ProjectPath)}",
            () => { _open = true; _focusNextDraw = true; },
            RequestClose,
            () => { SaveBlueprint(); return !_dirty; },
            DiscardBlueprint,
            () => _dirty));
    }

    private void RequestClose()
    {
        if (_dirty) _openUnsavedPopup = true;
        else CloseNow();
    }

    private void CloseNow()
    {
        _open = false;
        _cameraActivationPrompt.Reset();
        if (_registeredDocumentId.HasValue)
        {
            _documents.Unregister(_registeredDocumentId.Value);
            _registeredDocumentId = null;
        }
    }

    private void DiscardBlueprint()
    {
        if (_asset == null) return;
        _blueprint = new BlueprintSerializer().Load(_asset.FullPath);
        RebuildPreview();
        _propertyUndo.Clear();
        _propertyRedo.Clear();
        _dirty = false;
    }

    private void DrawUnsavedPopup()
    {
        if (_openUnsavedPopup)
        {
            ImGui.OpenPopup("Unsaved Blueprint");
            _openUnsavedPopup = false;
        }
        bool open = true;
        if (!ImGui.BeginPopupModal("Unsaved Blueprint", ref open,
                ImGuiWindowFlags.AlwaysAutoResize)) return;
        ImGui.TextColored(EditorTheme.Text, "The Blueprint has unsaved changes.");
        EditorUi.MutedText("Save before closing?");
        ImGui.Spacing();
        if (EditorUi.PrimaryButton("Save"))
        {
            SaveBlueprint();
            if (!_dirty) { CloseNow(); ImGui.CloseCurrentPopup(); }
        }
        ImGui.SameLine();
        if (EditorUi.DestructiveButton("Discard"))
        {
            DiscardBlueprint();
            CloseNow();
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (EditorUi.SecondaryButton("Cancel")) ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
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
