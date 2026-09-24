using System.Numerics;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;

using ImGuiNET;

namespace ByteEngine.Editor.Panels;

/// <summary>
/// Dedicated authoring workspace for .byteanim assets.
///
/// Profile changes stay in memory until Save. Humanoid bone configuration is
/// delegated to a dedicated rig window instead of expanding the profile into a
/// long wall of bone dropdowns.
/// </summary>
internal sealed class AnimationProfileWorkspacePanel : IDisposable
{
    private readonly EditorDocumentManager _documents;

    public AnimationProfileWorkspacePanel(EditorDocumentManager documents)
    {
        _documents = documents;
    }
    private readonly HumanoidRigConfiguratorPanel _humanoidConfigurator =
        new();
    private readonly SkeletalSocketPreview _socketPreview = new();

    private AssetRecord? _asset;
    private EditorProjectContext? _project;
    private AnimationProfile? _profile;

    private bool _visible;
    private bool _focusNextDraw;
    private bool _dirty;
    private Guid _socketModelGuid;
    private Guid _selectedSocketId;
    private string _selectedSocketBone = string.Empty;
    private List<SkeletalSocketDefinition> _socketDrafts = new();
    private bool _openUnsavedPopup;
    private EditorDocumentId? _registeredDocumentId;
    private AssetRecord? _pendingOpenAsset;
    private EditorProjectContext? _pendingOpenProject;
    private bool _pendingClose;

    private string? _loadError;

    private Guid _clipCacheModelGuid;
    private string? _clipCacheModelPath;
    private IReadOnlyList<string> _clipCache =
        Array.Empty<string>();
    private bool _clipCacheValid;

    private string _clipSearch =
        string.Empty;

    private string _assetSearch =
        string.Empty;

    public void Open(
        AssetRecord asset,
        EditorProjectContext project,
        EditorLog log)
    {
        if (asset.Type !=
            AssetType.AnimationProfile)
        {
            return;
        }
        if (_asset?.Guid == asset.Guid && _visible)
        {
            _focusNextDraw = true;
            if (_registeredDocumentId.HasValue) _documents.Activate(_registeredDocumentId.Value);
            return;
        }
        if (_dirty)
        {
            _pendingOpenAsset = asset;
            _pendingOpenProject = project;
            _pendingClose = false;
            _openUnsavedPopup = true;
            return;
        }

        _asset =
            asset;

        _project =
            project;

        _visible =
            true;

        _focusNextDraw =
            true;

        _dirty =
            false;

        _loadError =
            null;

        InvalidateClipCache();

        try
        {
            _profile =
                AnimationProfileSerializer.Load(
                    asset.FullPath);
            RegisterDocument(log);

        }
        catch (Exception exception)
        {
            _profile =
                null;

            _loadError =
                exception.Message;

            log.Error(
                $"Could not open Animation Profile '{asset.ProjectPath}': {exception.Message}");
        }
    }

    public void Draw(
        EditorLog log,
        Renderer2D renderer,
        Renderer3D renderer3D,
        int windowWidth,
        int windowHeight)
    {
        if (_visible &&
            _asset != null &&
            _project != null)
        {
            DrawWorkspace(log, renderer, renderer3D, windowWidth, windowHeight);
        }

        Guid? configureRequest =
            HumanoidRigConfiguratorRequest.Consume();

        if (configureRequest.HasValue &&
            _project != null &&
            _project.AssetDatabase.TryGetAsset(
                configureRequest.Value,
                out AssetRecord? modelAsset) &&
            modelAsset?.Type ==
                AssetType.Model3D)
        {
            _humanoidConfigurator.Open(
                modelAsset,
                _project,
                log);
        }

        _humanoidConfigurator.Draw(
            log);
        DrawUnsavedPopup(log);

    }

    private void DrawWorkspace(EditorLog log, Renderer2D renderer, Renderer3D renderer3D, int windowWidth, int windowHeight)
    {
        if (_asset == null ||
            _project == null)
        {
            return;
        }

        if (_focusNextDraw)
        {
            ImGui.SetNextWindowFocus();

            _focusNextDraw =
                false;
        }

        string name =
            Path.GetFileNameWithoutExtension(
                _asset.ProjectPath);

        bool open =
            true;

        bool visible =
            ImGui.Begin(
                $"Animation Profile: {name}{(_dirty ? " *" : string.Empty)}###AnimationProfileWorkspace",
                ref open,
                ImGuiWindowFlags.MenuBar);

        if (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) &&
            _registeredDocumentId.HasValue)
            _documents.Activate(_registeredDocumentId.Value);

        if (visible)
        {
            DrawMenuBar(
                log);

            DrawCommandBar(log);

            if (_profile ==
                null)
            {
                ImGui.TextColored(
                    new Vector4(
                        1.0f,
                        0.38f,
                        0.32f,
                        1.0f),
                    "Animation Profile could not be loaded.");

                if (!string.IsNullOrWhiteSpace(
                        _loadError))
                {
                    ImGui.TextWrapped(
                        _loadError);
                }

                if (ImGui.Button(
                        "Retry Load"))
                {
                    Reload(
                        log);
                }
            }
            else
            {
                DrawProfileTabs(renderer, renderer3D, windowWidth, windowHeight);
            }
        }

        ImGui.End();

        if (!open)
            RequestClose();
    }

    private void DrawMenuBar(
        EditorLog log)
    {
        if (!ImGui.BeginMenuBar())
        {
            return;
        }

        if (ImGui.BeginMenu("File"))
        {
            if (ImGui.MenuItem("Close")) RequestClose();

            if (_registeredDocumentId.HasValue)
            {
                ImGui.Separator();
                if (ImGui.MenuItem("Close Others"))
                    _documents.RequestCloseOthers(_registeredDocumentId.Value);
                if (ImGui.MenuItem("Close All"))
                    _documents.RequestCloseAll();
            }

            ImGui.EndMenu();
        }

        ImGui.EndMenuBar();
    }
    private void DrawCommandBar(
        EditorLog log)
    {
        if (_asset ==
            null)
        {
            return;
        }

        bool toolbarVisible = EditorUi.BeginToolbar("##AnimationProfileToolbar");

        if (toolbarVisible)
        {
            ImGui.BeginDisabled(!_dirty || _profile == null);
            if (_dirty)
            {
                if (EditorUi.PrimaryButton("Save")) Save(log);
            }
            else
            {
                EditorUi.ToolbarButton("Save", "No unsaved Profile changes", enabled: false);
            }
            ImGui.EndDisabled();
            ImGui.SameLine();
            if (EditorUi.ToolbarButton("Revert", "Reload the last saved Profile")) Reload(log);
            if (_dirty)
            {
                ImGui.SameLine(Math.Max(ImGui.GetCursorPosX(), ImGui.GetWindowWidth() - 105.0f));
                EditorUi.StatusBadge("UNSAVED", EditorStatusKind.Warning);
            }

        }

        EditorUi.EndToolbar();

        if (_dirty)
        {
            EditorUi.StatusBadge("UNSAVED", EditorStatusKind.Warning);
            ImGui.SameLine();
            EditorUi.MutedText("Runtime will continue using the last saved profile.");
        }
        if (ImGui.IsWindowFocused(
                ImGuiFocusedFlags.RootAndChildWindows) &&
            ImGui.GetIO().KeyCtrl &&
            ImGui.IsKeyPressed(
                ImGuiKey.S))
        {
            Save(
                log);
        }

        ImGui.Separator();
    }

    private void DrawProfileTabs(Renderer2D renderer, Renderer3D renderer3D, int windowWidth, int windowHeight)
    {
        if (_profile ==
                null ||
            _project ==
                null ||
            _asset ==
                null)
        {
            return;
        }

        if (!ImGui.BeginTabBar(
                $"AnimationProfileEditorTabs##{_asset.Guid}"))
        {
            return;
        }

        if (ImGui.BeginTabItem(
                "Rig"))
        {
            _dirty |=
                DrawRig();

            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem(
                "Locomotion"))
        {
            _dirty |=
                DrawLocomotion();

            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem(
                "Actions"))
        {
            _dirty |=
                DrawActions();

            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem(
                "Sockets"))
        {
            DrawSockets(renderer, renderer3D, windowWidth, windowHeight);
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem(
                "Layers"))
        {
            DrawLayers();

            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem(
                "Procedural"))
        {
            _dirty |=
                DrawProcedural();

            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem(
                "Debug"))
        {
            DrawDebug();

            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private void DrawSockets(
        Renderer2D renderer,
        Renderer3D renderer3D,
        int windowWidth,
        int windowHeight)
    {
        AssetReference reference =
            _profile?.Rig.ReferenceModel ??
            AssetReference.Empty;

        if (_project ==
                null ||
            reference.IsEmpty)
        {
            ImGui.TextDisabled(
                "Assign a Reference Model on the Rig tab to author sockets.");

            return;
        }

        ModelAsset model;

        try
        {
            model =
                _project.Assets.LoadModel(
                    reference);
        }
        catch (Exception exception)
        {
            ImGui.TextDisabled(
                exception.Message);

            return;
        }

        if (_socketModelGuid !=
            model.Guid)
        {
            _socketModelGuid =
                model.Guid;

            _socketDrafts =
                model.Sockets
                    .Select(item => item.Clone())
                    .ToList();

            _selectedSocketId =
                Guid.Empty;

            _selectedSocketBone =
                string.Empty;

            _socketPreview.Reset();
        }

        if (model.Skeleton ==
            null)
        {
            ImGui.TextDisabled(
                "The Reference Model has no skeleton.");

            return;
        }

        ImGui.Columns(
            3,
            "SocketAuthoringColumns",
            true);

        ImGui.SetColumnWidth(
            0,
            285.0f);

        DrawSocketSkeletonColumn(
            model);

        ImGui.NextColumn();

        ImGui.SeparatorText(
            "3D PREVIEW");

        _socketPreview.Draw(
            _project,
            reference,
            model,
            _socketDrafts,
            ref _selectedSocketId,
            renderer,
            renderer3D,
            windowWidth,
            windowHeight,
            () => PersistSockets(model));

        SkeletalSocketDefinition? viewportSelected =
            _socketDrafts.FirstOrDefault(socket =>
                socket.Id ==
                _selectedSocketId);

        if (viewportSelected !=
            null)
        {
            _selectedSocketBone =
                viewportSelected.BoneName;
        }

        ImGui.NextColumn();

        DrawSocketDetailsColumn(
            model);

        ImGui.Columns(
            1);
    }

    private void DrawSocketSkeletonColumn(
        ModelAsset model)
    {
        ImGui.SeparatorText(
            "SKELETON");

        if (string.IsNullOrWhiteSpace(
                _selectedSocketBone))
        {
            ImGui.TextDisabled(
                "Select a bone, then add a socket.");
        }
        else
        {
            ImGui.TextColored(
                EditorTheme.TextSecondary,
                _selectedSocketBone);
        }

        ImGui.BeginDisabled(
            string.IsNullOrWhiteSpace(
                _selectedSocketBone));

        if (ImGui.Button(
                "+ Add Socket",
                new Vector2(-1.0f, 0.0f)))
        {
            AddSocketToBone(
                model,
                _selectedSocketBone);
        }

        ImGui.EndDisabled();

        ImGui.Separator();

        Vector2 available =
            ImGui.GetContentRegionAvail();

        bool treeVisible =
            ImGui.BeginChild(
                "##SocketSkeletonTree",
                new Vector2(
                    0.0f,
                    Math.Max(
                        available.Y,
                        280.0f)),
                ImGuiChildFlags.Borders);

        if (treeVisible)
        {
            HashSet<int> visited =
                new();

            for (int boneIndex = 0;
                 boneIndex < model.Skeleton!.Bones.Count;
                 boneIndex++)
            {
                int parentIndex =
                    model.Skeleton.Bones[boneIndex]
                        .ParentIndex;

                if (parentIndex >=
                        0 &&
                    parentIndex <
                        model.Skeleton.Bones.Count &&
                    parentIndex !=
                        boneIndex)
                {
                    continue;
                }

                DrawSocketBoneNode(
                    model,
                    boneIndex,
                    visited);
            }

            /*
             * Imported skeletons should form one or more valid trees, but do
             * not hide bones if an importer produced a malformed parent index
             * or cycle. Any node not reached from a normal root is rendered as
             * an additional top-level branch.
             */
            for (int boneIndex = 0;
                 boneIndex < model.Skeleton.Bones.Count;
                 boneIndex++)
            {
                if (!visited.Contains(
                        boneIndex))
                {
                    DrawSocketBoneNode(
                        model,
                        boneIndex,
                        visited);
                }
            }
        }

        ImGui.EndChild();
    }

    private void DrawSocketBoneNode(
        ModelAsset model,
        int boneIndex,
        HashSet<int> visited)
    {
        if (model.Skeleton ==
                null ||
            boneIndex <
                0 ||
            boneIndex >=
                model.Skeleton.Bones.Count ||
            !visited.Add(
                boneIndex))
        {
            return;
        }

        var bone =
            model.Skeleton.Bones[boneIndex];

        bool boneSelected =
            _selectedSocketId ==
                Guid.Empty &&
            string.Equals(
                _selectedSocketBone,
                bone.Name,
                StringComparison.OrdinalIgnoreCase);

        bool hasBoneChildren =
            model.Skeleton.Bones
                .Any(candidate =>
                    candidate.ParentIndex ==
                    boneIndex);

        bool hasSocketChildren =
            _socketDrafts
                .Any(socket =>
                    string.Equals(
                        socket.BoneName,
                        bone.Name,
                        StringComparison.OrdinalIgnoreCase));

        ImGuiTreeNodeFlags flags =
            ImGuiTreeNodeFlags.OpenOnArrow |
            ImGuiTreeNodeFlags.SpanAvailWidth;

        if (boneSelected)
        {
            flags |=
                ImGuiTreeNodeFlags.Selected;
        }

        if (!hasBoneChildren &&
            !hasSocketChildren)
        {
            flags |=
                ImGuiTreeNodeFlags.Leaf;
        }

        if (bone.ParentIndex <
            0)
        {
            flags |=
                ImGuiTreeNodeFlags.DefaultOpen;
        }

        ImGui.PushID(
            boneIndex);

        bool open =
            ImGui.TreeNodeEx(
                bone.Name,
                flags);

        if (ImGui.IsItemClicked(
                ImGuiMouseButton.Left))
        {
            _selectedSocketBone =
                bone.Name;

            _selectedSocketId =
                Guid.Empty;
        }

        if (open)
        {
            foreach (SkeletalSocketDefinition socket
                     in _socketDrafts.Where(item =>
                         string.Equals(
                             item.BoneName,
                             bone.Name,
                             StringComparison.OrdinalIgnoreCase)))
            {
                ImGui.PushID(
                    socket.Id.ToString("N"));

                ImGuiTreeNodeFlags socketFlags =
                    ImGuiTreeNodeFlags.Leaf |
                    ImGuiTreeNodeFlags.NoTreePushOnOpen |
                    ImGuiTreeNodeFlags.SpanAvailWidth;

                if (socket.Id ==
                    _selectedSocketId)
                {
                    socketFlags |=
                        ImGuiTreeNodeFlags.Selected;
                }

                ImGui.TreeNodeEx(
                    $"Socket  {socket.Name}",
                    socketFlags);

                if (ImGui.IsItemClicked(
                        ImGuiMouseButton.Left))
                {
                    _selectedSocketId =
                        socket.Id;

                    _selectedSocketBone =
                        bone.Name;
                }

                ImGui.PopID();
            }

            for (int childIndex = 0;
                 childIndex < model.Skeleton.Bones.Count;
                 childIndex++)
            {
                if (model.Skeleton.Bones[childIndex]
                        .ParentIndex ==
                    boneIndex)
                {
                    DrawSocketBoneNode(
                        model,
                        childIndex,
                        visited);
                }
            }

            ImGui.TreePop();
        }

        ImGui.PopID();
    }

    private void DrawSocketDetailsColumn(
        ModelAsset model)
    {
        ImGui.SeparatorText(
            "SOCKET DETAILS");

        SkeletalSocketDefinition? selected =
            _socketDrafts.FirstOrDefault(item =>
                item.Id ==
                _selectedSocketId);

        if (selected ==
            null)
        {
            if (string.IsNullOrWhiteSpace(
                    _selectedSocketBone))
            {
                ImGui.TextDisabled(
                    "Select a bone or socket from the skeleton tree.");

                ImGui.TextWrapped(
                    "Sockets are named attachment points stored on the skeletal model. Select a bone to create one.");

                return;
            }

            ImGui.TextColored(
                EditorTheme.TextSecondary,
                "SELECTED BONE");

            ImGui.Text(
                _selectedSocketBone);

            ImGui.Spacing();

            if (ImGui.Button(
                    $"+ Add Socket to {_selectedSocketBone}"))
            {
                AddSocketToBone(
                    model,
                    _selectedSocketBone);
            }

            ImGui.Spacing();
            ImGui.TextWrapped(
                "Create a socket here, then use the 3D preview to align a weapon, prop, VFX anchor, or other attachment.");

            return;
        }

        bool changed =
            false;

        string name =
            selected.Name;

        if (ImGui.InputText(
                "Name",
                ref name,
                128) &&
            !string.IsNullOrWhiteSpace(
                name) &&
            !_socketDrafts.Any(item =>
                item.Id !=
                    selected.Id &&
                string.Equals(
                    item.Name,
                    name,
                    StringComparison.OrdinalIgnoreCase)))
        {
            selected.Name =
                name.Trim();

            changed =
                true;
        }

        string previewBone =
            string.IsNullOrWhiteSpace(
                selected.BoneName)
                ? "Select Bone..."
                : selected.BoneName;

        bool boneExists =
            model.Skeleton!.Bones
                .Any(bone =>
                    string.Equals(
                        bone.Name,
                        selected.BoneName,
                        StringComparison.OrdinalIgnoreCase));

        if (ImGui.BeginCombo(
                "Bone",
                boneExists
                    ? previewBone
                    : previewBone + "  Missing"))
        {
            foreach (var bone
                     in model.Skeleton.Bones)
            {
                bool boneSelected =
                    string.Equals(
                        bone.Name,
                        selected.BoneName,
                        StringComparison.OrdinalIgnoreCase);

                if (ImGui.Selectable(
                        bone.Name,
                        boneSelected))
                {
                    selected.BoneName =
                        bone.Name;

                    _selectedSocketBone =
                        bone.Name;

                    changed =
                        true;
                }
            }

            ImGui.EndCombo();
        }

        ImGui.SeparatorText(
            "TRANSFORM");

        Vector3 position =
            selected.PositionOffset;

        Vector3 rotation =
            selected.RotationOffsetDegrees;

        Vector3 scale =
            selected.Scale;

        if (ImGui.DragFloat3(
                "Position",
                ref position,
                0.01f))
        {
            selected.PositionOffset =
                position;

            changed =
                true;
        }

        if (ImGui.DragFloat3(
                "Rotation",
                ref rotation,
                0.25f))
        {
            selected.RotationOffsetDegrees =
                rotation;

            changed =
                true;
        }

        if (ImGui.DragFloat3(
                "Scale",
                ref scale,
                0.01f,
                0.0001f,
                1000.0f))
        {
            selected.Scale =
                Vector3.Max(
                    scale,
                    new Vector3(0.0001f));

            changed =
                true;
        }

        bool inheritBoneScale =
            selected.InheritBoneScale;

        if (ImGui.Checkbox(
                "Inherit Bone Scale",
                ref inheritBoneScale))
        {
            selected.InheritBoneScale =
                inheritBoneScale;

            changed =
                true;
        }

        ImGui.SeparatorText(
            "ALIGNMENT PREVIEW");

        AssetReference preview =
            selected.PreviewAssetGuid.HasValue
                ? new AssetReference(
                    selected.PreviewAssetGuid.Value,
                    selected.PreviewAssetPath)
                : AssetReference.Empty;

        if (DrawAssetPicker(
                "Preview Asset",
                AssetType.Model3D,
                ref preview))
        {
            selected.PreviewAssetGuid =
                preview.IsEmpty
                    ? null
                    : preview.Guid;

            selected.PreviewAssetPath =
                preview.CachedProjectPath;

            changed =
                true;
        }

        ImGui.TextWrapped(
            "Move/rotate the socket in the center viewport until the preview asset fits the animated bone. The preview asset is editor-only.");

        ImGui.Separator();

        if (ImGui.Button(
                "Duplicate"))
        {
            SkeletalSocketDefinition copy =
                selected.Clone(
                    false);

            string baseName =
                selected.Name +
                " Copy";

            copy.Name =
                baseName;

            int suffix =
                2;

            while (_socketDrafts.Any(item =>
                       string.Equals(
                           item.Name,
                           copy.Name,
                           StringComparison.OrdinalIgnoreCase)))
            {
                copy.Name =
                    baseName +
                    suffix++;
            }

            _socketDrafts.Add(
                copy);

            _selectedSocketId =
                copy.Id;

            _selectedSocketBone =
                copy.BoneName;

            changed =
                true;
        }

        ImGui.SameLine();

        if (ImGui.Button(
                "Delete"))
        {
            string formerBone =
                selected.BoneName;

            _socketDrafts.Remove(
                selected);

            _selectedSocketId =
                Guid.Empty;

            _selectedSocketBone =
                formerBone;

            changed =
                true;
        }

        if (changed)
        {
            PersistSockets(
                model);
        }
    }

    private void AddSocketToBone(
        ModelAsset model,
        string boneName)
    {
        if (string.IsNullOrWhiteSpace(
                boneName))
        {
            return;
        }

        string baseName =
            boneName +
            "Socket";

        string name =
            baseName;

        int suffix =
            2;

        while (_socketDrafts.Any(item =>
                   string.Equals(
                       item.Name,
                       name,
                       StringComparison.OrdinalIgnoreCase)))
        {
            name =
                baseName +
                suffix++;
        }

        SkeletalSocketDefinition socket =
            new()
            {
                Name = name,
                BoneName = boneName
            };

        _socketDrafts.Add(
            socket);

        _selectedSocketId =
            socket.Id;

        _selectedSocketBone =
            boneName;

        PersistSockets(
            model);
    }

    private void PersistSockets(ModelAsset model)
    {
        if (_project == null) return;
        try { ModelSocketMetadataStore.Save(_project.ProjectRoot, model.Guid, _socketDrafts); model.ReplaceSockets(_socketDrafts); }
        catch (Exception exception) { _loadError = exception.Message; }
    }
    private bool DrawRig()
    {
        if (_profile ==
                null ||
            _project ==
                null ||
            _asset ==
                null)
        {
            return false;        }

        bool changed =
            false;

        ImGui.SeparatorText(
            "SKELETON RIG");

        AssetReference modelReference =
            _profile.Rig.ReferenceModel ??
            AssetReference.Empty;

        if (DrawAssetPicker(
                "Reference Model",
                AssetType.Model3D,
                ref modelReference))
        {
            _profile.Rig.ReferenceModel =
                modelReference;

            InvalidateClipCache();

            changed =
                true;
        }

        if (modelReference.IsEmpty)
        {
            ImGui.TextWrapped(
                "Choose the character model that defines this profile's skeleton.");

            return changed;
        }

        AssetRecord? modelAsset =
            _project.AssetDatabase.Resolve(
                modelReference);

        if (modelAsset ==
                null ||
            modelAsset.Type !=
                AssetType.Model3D)
        {
            ImGui.TextColored(
                new Vector4(
                    1.0f,
                    0.38f,
                    0.30f,
                    1.0f),
                "Reference Model is missing or is not a 3D model.");

            return changed;
        }

        try
        {
            ModelAsset model =
                _project.Assets.LoadModel(
                    modelReference);

            bool rigMetadataChanged =
                HumanoidRigAuthoring.Draw(
                    modelAsset,
                    model,
                    _project,
                    out AnimationRigType actualRigType);

            if (_profile.Rig.Type !=
                actualRigType)
            {
                _profile.Rig.Type =
                    actualRigType;

                changed =
                    true;
            }

            if (rigMetadataChanged)
            {
                _profile.Rig.Type =
                    actualRigType;
            }

            if (actualRigType ==
                AnimationRigType.Humanoid)
            {
                DrawReferencePoseStatus(
                    model,
                    modelAsset.Metadata.ModelImporter.HumanoidMapping);
            }

            IReadOnlyList<string> clips =
                GetAnimationClipNames();

            ImGui.Spacing();

            ImGui.TextDisabled(
                clips.Count ==
                    0
                    ? "Animation source clips: none"
                    : $"Animation source clips: {clips.Count}");
        }
        catch (Exception exception)
        {
            ImGui.TextColored(
                new Vector4(
                    1.0f,
                    0.38f,
                    0.30f,
                    1.0f),
                "Could not inspect the Reference Model.");

            ImGui.TextWrapped(
                exception.Message);
        }

        return changed;
    }

    private static void DrawReferencePoseStatus(
        ModelAsset model,
        HumanoidBoneMap mapping)
    {
        ImGui.SeparatorText(
            "REFERENCE POSE");

        HumanoidReferencePose pose =
            HumanoidReferencePose.Capture(
                model.Skeleton,
                mapping);

        HumanoidRigDiagnosticReport diagnostics =
            HumanoidRigDiagnostics.Analyze(
                model.Skeleton,
                mapping);

        if (pose.IsReady)
        {
            ImGui.TextColored(
                new Vector4(
                    0.35f,
                    0.86f,
                    0.48f,
                    1.0f),
                "Reference Pose Ready");

            ImGui.TextDisabled(
                $"{pose.CapturedBoneCount} mapped bone(s) captured from the model bind pose.");

            if (!diagnostics.ApproximatelyTPose)
            {
                ImGui.TextColored(
                    new Vector4(
                        1.0f,
                        0.67f,
                        0.25f,
                        1.0f),
                    "Reference pose is not close to a clean T-pose.");
            }

            return;
        }

        ImGui.TextColored(
            new Vector4(
                1.0f,
                0.58f,
                0.24f,
                1.0f),
            "Reference Pose Needs Attention");

        if (pose.MissingRequiredBones.Count >
            0)
        {
            ImGui.TextWrapped(
                $"Missing reference transforms: {string.Join(", ", pose.MissingRequiredBones)}");
        }

        if (pose.InvalidMappedBones.Count >
            0)
        {
            ImGui.TextWrapped(
                $"Invalid inverse-bind transforms: {string.Join(", ", pose.InvalidMappedBones)}");
        }
    }

    private bool DrawLocomotion()
    {
        if (_profile ==
            null)
        {
            return false;
        }

        AnimationLocomotionProfile locomotion =
            _profile.Locomotion;

        bool changed =
            false;

        ImGui.SeparatorText(
            "BASE LOCOMOTION");

        bool enabled =
            locomotion.Enabled;

        if (ImGui.Checkbox(
                "Enabled",
                ref enabled))
        {
            locomotion.Enabled =
                enabled;

            changed =
                true;
        }

        bool automatic =
            locomotion.DriveFromCharacterController;

        if (ImGui.Checkbox(
                "Automatic Speed / State",
                ref automatic))        {
            locomotion.DriveFromCharacterController =
                automatic;

            changed =
                true;
        }

        IReadOnlyList<string> clips =
            GetAnimationClipNames();

        string idle =
            locomotion.Idle;

        if (DrawClipPicker(
                "Idle",
                clips,
                ref idle))
        {
            locomotion.Idle =
                idle;

            changed =
                true;
        }

        string walk =
            locomotion.Walk;

        if (DrawClipPicker(
                "Walk",
                clips,
                ref walk))
        {
            locomotion.Walk =
                walk;

            changed =
                true;
        }

        string run =
            locomotion.Run;

        if (DrawClipPicker(
                "Run",
                clips,
                ref run))
        {
            locomotion.Run =
                run;

            changed =
                true;
        }

        string jump =
            locomotion.Jump;

        if (DrawClipPicker(
                "Jump",
                clips,
                ref jump))
        {
            locomotion.Jump =
                jump;

            changed =
                true;
        }

        string fall =
            locomotion.Fall;

        if (DrawClipPicker(
                "Fall",
                clips,
                ref fall))
        {
            locomotion.Fall =
                fall;

            changed =
                true;
        }

        string land =
            locomotion.Land;

        if (DrawClipPicker(
                "Land",
                clips,
                ref land))
        {
            locomotion.Land =
                land;

            changed =
                true;
        }

        ImGui.SeparatorText("SPEED THRESHOLDS");
        float moveThreshold = locomotion.MoveThreshold;
        if (ImGui.DragFloat("Move Threshold", ref moveThreshold, 0.01f, 0.0f, 1000.0f)) { locomotion.MoveThreshold = Math.Max(moveThreshold, 0.0f); changed = true; }
        float runThreshold =
            locomotion.RunThreshold;

        if (ImGui.DragFloat(
                "Run Threshold",
                ref runThreshold,
                0.05f,
                0.0f,
                1000.0f))
        {
            locomotion.RunThreshold =
                Math.Max(
                    runThreshold,
                    0.0f);

            changed =
                true;
        }

        float stateHysteresis = locomotion.StateHysteresis;
        if (ImGui.DragFloat("State Hysteresis", ref stateHysteresis, 0.01f, 0.0f, 100.0f)) { locomotion.StateHysteresis = Math.Max(stateHysteresis, 0.0f); changed = true; }

        ImGui.SeparatorText("DIRECTIONAL ANIMATION CLIPS");
        bool directional = locomotion.DirectionalMovement;
        if (ImGui.Checkbox("Use Directional Animation Clips", ref directional)) { locomotion.DirectionalMovement = directional; changed = true; }
        ImGui.TextDisabled("Optional. Empty directions use the base Walk or Run clip.");
        ImGui.TextDisabled("Dedicated backward/strafe clips are recommended for fixed camera or aim facing.");

        ImGui.BeginDisabled(!directional);
        string[] directionLabels = { "Walk Forward", "Walk Backward", "Walk Left", "Walk Right", "Run Forward", "Run Backward", "Run Left", "Run Right" };
        string[] directionValues = { locomotion.WalkForward, locomotion.WalkBackward, locomotion.WalkLeft, locomotion.WalkRight, locomotion.RunForward, locomotion.RunBackward, locomotion.RunLeft, locomotion.RunRight };
        for (int index = 0; index < directionLabels.Length; index++)
        {
            string value = directionValues[index];
            string fallbackLabel = index < 4 ? "Use Base Walk" : "Use Base Run";
            if (!DrawClipPicker(directionLabels[index], clips, ref value, fallbackLabel)) continue;
            switch (index)
            {
                case 0: locomotion.WalkForward = value; break;
                case 1: locomotion.WalkBackward = value; break;
                case 2: locomotion.WalkLeft = value; break;
                case 3: locomotion.WalkRight = value; break;
                case 4: locomotion.RunForward = value; break;
                case 5: locomotion.RunBackward = value; break;
                case 6: locomotion.RunLeft = value; break;
                case 7: locomotion.RunRight = value; break;
            }
            changed = true;
        }
        float directionHysteresis = locomotion.DirectionHysteresis;
        if (ImGui.DragFloat("Direction Hysteresis", ref directionHysteresis, 0.01f, 0.0f, 1.0f)) { locomotion.DirectionHysteresis = Math.Max(directionHysteresis, 0.0f); changed = true; }
        ImGui.EndDisabled();

        ImGui.SeparatorText("PLAYBACK MATCHING");
        bool matching = locomotion.MatchPlaybackToSpeed;
        if (ImGui.Checkbox("Match Playback To Speed", ref matching)) { locomotion.MatchPlaybackToSpeed = matching; changed = true; }
        ImGui.BeginDisabled(!matching);
        float walkReference = locomotion.WalkReferenceSpeed; if (ImGui.DragFloat("Walk Reference Speed", ref walkReference, 0.05f, 0.001f, 1000.0f)) { locomotion.WalkReferenceSpeed = Math.Max(walkReference, 0.001f); changed = true; }
        float runReference = locomotion.RunReferenceSpeed; if (ImGui.DragFloat("Run Reference Speed", ref runReference, 0.05f, 0.001f, 1000.0f)) { locomotion.RunReferenceSpeed = Math.Max(runReference, 0.001f); changed = true; }
        float minimumRate = locomotion.MinimumPlaybackRate; if (ImGui.DragFloat("Minimum Rate", ref minimumRate, 0.01f, 0.0f, 10.0f)) { locomotion.MinimumPlaybackRate = Math.Max(minimumRate, 0.0f); changed = true; }
        float maximumRate = locomotion.MaximumPlaybackRate; if (ImGui.DragFloat("Maximum Rate", ref maximumRate, 0.01f, locomotion.MinimumPlaybackRate, 10.0f)) { locomotion.MaximumPlaybackRate = Math.Max(maximumRate, locomotion.MinimumPlaybackRate); changed = true; }
        ImGui.EndDisabled();

        ImGui.SeparatorText("PLAYBACK");
        float transition =
            locomotion.TransitionDuration;

        if (ImGui.DragFloat(
                "Blend Smoothness",
                ref transition,
                0.01f,
                0.0f,
                5.0f))
        {
            locomotion.TransitionDuration =
                Math.Max(
                    transition,
                    0.0f);

            changed =
                true;
        }

        float playbackSpeed =
            locomotion.PlaybackSpeed;

        if (ImGui.DragFloat(
                "Playback Speed",
                ref playbackSpeed,
                0.01f,
                0.0f,
                10.0f))
        {
            locomotion.PlaybackSpeed =
                Math.Max(
                    playbackSpeed,
                    0.0f);

            changed =
                true;
        }

        int rootMotion =
            (int)locomotion.RootMotionMode;

        string[] rootMotionNames =
            Enum.GetNames<RootMotionMode>();

        if (ImGui.Combo(
                "Root Motion",
                ref rootMotion,
                rootMotionNames,
                rootMotionNames.Length))
        {
            locomotion.RootMotionMode =
                (RootMotionMode)rootMotion;

            changed =
                true;
        }

        if (clips.Count ==
            0)
        {
            ImGui.TextWrapped(
                "Choose a Reference Model in RIG. Optionally choose a different Animation Source Model.");
        }

        return changed;
    }

    private bool DrawActions()
    {
        if (_profile ==
            null)
        {
            return false;
        }

        bool changed =
            false;

        IReadOnlyList<string> clips =
            GetAnimationClipNames();

        ImGui.SeparatorText(
            "NAMED ACTIONS");

        if (ImGui.Button(
                "+ Add Action"))
        {
            _profile.Actions.Add(
                new AnimationActionProfile());

            changed =
                true;
        }

        int removeIndex =
            -1;

        for (int index = 0;
             index <
                _profile.Actions.Count;
             index++)
        {
            AnimationActionProfile action =
                _profile.Actions[index];

            ImGui.PushID(
                $"ProfileAction:{index}");

            string title =
                string.IsNullOrWhiteSpace(
                    action.Name)
                    ? $"Action {index + 1}"
                    : action.Name;

            bool open =
                ImGui.TreeNodeEx(
                    $"{title}###ActionNode",
                    ImGuiTreeNodeFlags.DefaultOpen);
            ImGui.SameLine();

            if (ImGui.SmallButton(
                    "Remove"))
            {
                removeIndex =
                    index;
            }

            if (open)
            {
                string actionName =
                    action.Name ??
                    string.Empty;

                if (ImGui.InputText(
                        "Name",
                        ref actionName,
                        128))
                {
                    action.Name =
                        actionName;

                    changed =
                        true;
                }

                string actionClip =
                    action.Clip ??
                    string.Empty;

                if (DrawClipPicker(
                        "Clip",
                        clips,
                        ref actionClip))
                {
                    action.Clip =
                        actionClip;

                    changed =
                        true;
                }

                ImGui.SeparatorText("PLAYBACK");
                float actionSpeed = action.PlaybackSpeed;
                if (ImGui.DragFloat("Playback Speed", ref actionSpeed, 0.01f, 0.0f, 10.0f)) { action.PlaybackSpeed = Math.Max(actionSpeed, 0.0f); changed = true; }
                bool loop =
                    action.Loop;

                if (ImGui.Checkbox(
                        "Loop",
                        ref loop))
                {
                    action.Loop =
                        loop;

                    changed =
                        true;
                }

                float blendIn =
                    action.BlendIn;

                if (ImGui.DragFloat(
                        "Blend In",
                        ref blendIn,
                        0.01f,
                        0.0f,
                        5.0f))
                {
                    action.BlendIn =
                        Math.Max(
                            blendIn,
                            0.0f);

                    changed =
                        true;
                }

                float blendOut =
                    action.BlendOut;

                if (ImGui.DragFloat(
                        "Blend Out",
                        ref blendOut,
                        0.01f,
                        0.0f,
                        5.0f))
                {
                    action.BlendOut =
                        Math.Max(
                            blendOut,
                            0.0f);

                    changed =
                        true;
                }

                ImGui.SeparatorText("INTERRUPT");
                int priority = action.Priority;
                if (ImGui.DragInt("Priority", ref priority, 1.0f)) { action.Priority = priority; changed = true; }
                bool interruptible = action.Interruptible;
                if (ImGui.Checkbox("Interruptible", ref interruptible)) { action.Interruptible = interruptible; changed = true; }

                ImGui.SeparatorText("COMBO");
                string nextAction = action.NextAction ?? string.Empty;
                if (ImGui.BeginCombo("Next Action", string.IsNullOrWhiteSpace(nextAction) ? "None" : nextAction))
                {
                    if (ImGui.Selectable("None", string.IsNullOrWhiteSpace(nextAction))) { action.NextAction = string.Empty; changed = true; }
                    foreach (AnimationActionProfile candidate in _profile.Actions.Where(candidate => !ReferenceEquals(candidate, action) && !string.IsNullOrWhiteSpace(candidate.Name)))
                        if (ImGui.Selectable(candidate.Name, string.Equals(nextAction, candidate.Name, StringComparison.OrdinalIgnoreCase))) { action.NextAction = candidate.Name; changed = true; }
                    ImGui.EndCombo();
                }
                if (!string.IsNullOrWhiteSpace(nextAction) && !_profile.Actions.Any(candidate => string.Equals(candidate.Name, nextAction, StringComparison.OrdinalIgnoreCase)))
                    ImGui.TextColored(new Vector4(1.0f, 0.72f, 0.28f, 1.0f), "Next Action is unresolved; the saved value is preserved.");

                string comboWindow = action.ComboWindow ?? string.Empty;
                IReadOnlyList<string> windows = GetAnimationWindowNames(action.Clip ?? string.Empty);
                if (windows.Count > 0)
                {
                    if (ImGui.BeginCombo("Combo Window", string.IsNullOrWhiteSpace(comboWindow) ? "Any Time" : comboWindow))
                    {
                        if (ImGui.Selectable("Any Time", string.IsNullOrWhiteSpace(comboWindow))) { action.ComboWindow = string.Empty; changed = true; }
                        foreach (string window in windows)
                            if (ImGui.Selectable(window, string.Equals(comboWindow, window, StringComparison.OrdinalIgnoreCase))) { action.ComboWindow = window; changed = true; }
                        ImGui.EndCombo();
                    }
                    if (!string.IsNullOrWhiteSpace(comboWindow) && !windows.Contains(comboWindow, StringComparer.OrdinalIgnoreCase))
                        ImGui.TextColored(new Vector4(1.0f, 0.72f, 0.28f, 1.0f), "Combo Window is unresolved; the saved value is preserved.");
                }
                else if (ImGui.InputText("Combo Window (manual)", ref comboWindow, 128)) { action.ComboWindow = comboWindow; changed = true; }
                ImGui.TreePop();
            }

            ImGui.PopID();
        }

        if (removeIndex >=
            0)
        {
            _profile.Actions.RemoveAt(
                removeIndex);

            changed =
                true;
        }

        if (_profile.Actions.Count ==
            0)
        {
            ImGui.TextDisabled(
                "Actions are named one-shot/override animations. C11 expands this area with sections, combos and event windows.");
        }

        return changed;
    }

    private static void DrawLayers()
    {
        ImGui.SeparatorText(
            "ANIMATION LAYERS");

        ImGui.TextWrapped(
            "Layer definitions and body-region masks arrive in C13. They stay inside the Animation Profile.");

        ImGui.BulletText(
            "Full Body");

        ImGui.BulletText(
            "Upper Body");

        ImGui.BulletText(
            "Lower Body");

        ImGui.BulletText(
            "Arms / Head / Custom regions");
    }

    private bool DrawProcedural()
    {
        if (_profile ==
            null)
        {
            return false;
        }

        AnimationProceduralProfile procedural =
            _profile.Procedural;

        bool changed =
            false;

        ImGui.SeparatorText(
            "PROCEDURAL POSE FEATURES");

        bool aim =
            procedural.AimEnabled;
        if (ImGui.Checkbox(
                "Aim",
                ref aim))
        {
            procedural.AimEnabled =
                aim;

            changed =
                true;
        }

        bool lookAt =
            procedural.LookAtEnabled;

        if (ImGui.Checkbox(
                "Look At",
                ref lookAt))
        {
            procedural.LookAtEnabled =
                lookAt;

            changed =
                true;
        }

        bool footIk =
            procedural.FootIkEnabled;

        if (ImGui.Checkbox(
                "Foot IK",
                ref footIk))
        {
            procedural.FootIkEnabled =
                footIk;

            changed =
                true;
        }

        bool handIk =
            procedural.HandIkEnabled;

        if (ImGui.Checkbox(
                "Hand IK",
                ref handIk))
        {
            procedural.HandIkEnabled =
                handIk;

            changed =
                true;
        }

        ImGui.TextDisabled(
            "Aim and IK solvers arrive in later animation milestones.");

        return changed;
    }

    private void DrawDebug()
    {
        if (_profile ==
                null ||
            _asset ==
                null)
        {
            return;
        }

        IReadOnlyList<string> clips =
            GetAnimationClipNames();

        ImGui.SeparatorText(
            "PROFILE STATUS");

        ImGui.Text(
            $"Version: {_profile.Version}");

        ImGui.Text(
            $"Actions: {_profile.Actions.Count}");

        ImGui.Text(
            $"Animation source clips: {clips.Count}");

        ImGui.Text(
            $"Rig: {_profile.Rig.Type}");

        ImGui.TextDisabled(
            $"GUID: {_asset.Guid}");

        ImGui.TextDisabled(
            _asset.ProjectPath);

        string[] duplicateActions =
            _profile.Actions
                .Where(
                    action =>
                        !string.IsNullOrWhiteSpace(
                            action.Name))
                .GroupBy(
                    action =>
                        action.Name,
                    StringComparer.OrdinalIgnoreCase)
                .Where(
                    group =>
                        group.Count() >
                        1)
                .Select(
                    group =>
                        group.Key)
                .ToArray();

        if (duplicateActions.Length >
            0)
        {
            ImGui.TextColored(
                new Vector4(
                    1.0f,
                    0.67f,
                    0.25f,
                    1.0f),
                $"Duplicate action name(s): {string.Join(", ", duplicateActions)}");
        }
        else
        {
            ImGui.TextDisabled(
                "No duplicate action names detected.");
        }
    }

    private IReadOnlyList<string> GetAnimationClipNames()
    {
        if (_profile ==
                null ||
            _project ==
                null ||
            _profile.Rig.ReferenceModel ==
                null ||
            _profile.Rig.ReferenceModel.IsEmpty)
        {
            InvalidateClipCache();

            return
                Array.Empty<string>();
        }

        AssetReference reference = _profile.Rig.ReferenceModel;

        if (_clipCacheValid &&
            _clipCacheModelGuid ==
                reference.Guid &&
            string.Equals(
                _clipCacheModelPath,
                reference.CachedProjectPath,
                StringComparison.OrdinalIgnoreCase))
        {
            return
                _clipCache;
        }

        try
        {
            ModelAsset model =
                _project.Assets.LoadModel(
                    reference);

            _clipCache =
                model.Animations
                    .Select(
                        animation =>
                            string.IsNullOrWhiteSpace(
                                animation.Name)
                                ? animation.Key
                                : animation.Name)
                    .Where(
                        name =>
                            !string.IsNullOrWhiteSpace(
                                name))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .OrderBy(
                        name =>
                            name,
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray();

            _clipCacheModelGuid =
                reference.Guid;

            _clipCacheModelPath =
                reference.CachedProjectPath;

            _clipCacheValid =
                true;

            return
                _clipCache;
        }
        catch
        {
            InvalidateClipCache();

            return
                Array.Empty<string>();
        }
    }

    private IReadOnlyList<string> GetAnimationWindowNames(string clipName)
    {
        if (_profile == null || _project == null || string.IsNullOrWhiteSpace(clipName)) return Array.Empty<string>();
        AssetReference reference = _profile.Rig.ReferenceModel;
        if (reference.IsEmpty) return Array.Empty<string>();
        try
        {
            ModelAsset model = _project.Assets.LoadModel(reference);
            var animation = model.Animations.FirstOrDefault(candidate => string.Equals(candidate.Name, clipName, StringComparison.OrdinalIgnoreCase) || string.Equals(candidate.Key, clipName, StringComparison.OrdinalIgnoreCase));
            return animation?.Windows.Select(window => window.Name).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>();
        }
        catch { return Array.Empty<string>(); }
    }
    private void InvalidateClipCache()
    {
        _clipCacheModelGuid =
            Guid.Empty;

        _clipCacheModelPath =
            null;

        _clipCache =
            Array.Empty<string>();

        _clipCacheValid =
            false;
    }

    private bool DrawAssetPicker(
        string label,
        AssetType expectedType,
        ref AssetReference reference)
    {
        if (_project ==
            null)
        {
            return false;
        }

        AssetRecord? current =
            reference.IsEmpty
                ? null
                : _project.AssetDatabase.Resolve(
                    reference);

        string preview =
            current?.ProjectPath ??
            reference.CachedProjectPath ??
            "None";

        bool changed =
            false;

        if (ImGui.BeginCombo(
                label,
                preview))
        {
            if (ImGui.IsWindowAppearing())
            {
                _assetSearch =
                    string.Empty;

                ImGui.SetKeyboardFocusHere();
            }

            ImGui.InputTextWithHint(
                "##AssetSearch",
                "Search model assets...",
                ref _assetSearch,
                128);

            if (ImGui.Selectable(
                    "None",
                    reference.IsEmpty))
            {
                reference =
                    AssetReference.Empty;

                changed =
                    true;
            }

            foreach (AssetRecord asset
                     in _project.AssetDatabase.Assets
                         .Where(
                             asset =>
                                 asset.Type ==
                                 expectedType)
                         .OrderBy(
                             asset =>
                                 asset.ProjectPath,
                             StringComparer.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(
                        _assetSearch) &&
                    !asset.ProjectPath.Contains(
                        _assetSearch,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool selected =
                    asset.Guid !=
                        Guid.Empty &&
                    asset.Guid ==
                        reference.Guid;

                if (ImGui.Selectable(
                        asset.ProjectPath,
                        selected))
                {
                    reference =
                        new AssetReference(
                            asset.Guid,
                            asset.ProjectPath);

                    changed =
                        true;
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        if (ImGui.BeginDragDropTarget())
        {
            Guid? id =
                AssetDragDrop.Accept();

            if (id.HasValue &&
                _project.AssetDatabase.TryGetAsset(
                    id.Value,
                    out AssetRecord? dropped) &&
                dropped?.Type ==
                    expectedType)
            {
                reference =
                    new AssetReference(
                        dropped.Guid,
                        dropped.ProjectPath);

                changed =
                    true;
            }

            ImGui.EndDragDropTarget();
        }

        return changed;
    }

    private bool DrawClipPicker(
        string label,
        IReadOnlyList<string> clips,
        ref string value,
        string emptyLabel = "None")
    {
        value ??=
            string.Empty;

        string currentValue =
            value;

        bool exists =
            clips.Any(
                clip =>
                    string.Equals(
                        clip,
                        currentValue,
                        StringComparison.OrdinalIgnoreCase));

        string preview =
            string.IsNullOrWhiteSpace(
                value)
                ? emptyLabel                : exists
                    ? value
                    : $"{value}  ⚠ Missing";

        bool changed =
            false;

        if (!ImGui.BeginCombo(
                label,
                preview))
        {
            return false;
        }

        if (ImGui.IsWindowAppearing())
        {
            _clipSearch =
                string.Empty;

            ImGui.SetKeyboardFocusHere();
        }

        ImGui.InputTextWithHint(
            "##AnimationSearch",
            "Search animations...",
            ref _clipSearch,
            128);

        if (ImGui.Selectable(
                emptyLabel,
                string.IsNullOrWhiteSpace(
                    value)))
        {
            value =
                string.Empty;

            changed =
                true;
        }

        int visibleCount =
            0;

        foreach (string clip
                 in clips)
        {
            if (!string.IsNullOrWhiteSpace(
                    _clipSearch) &&
                !clip.Contains(
                    _clipSearch,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            visibleCount++;

            bool selected =
                string.Equals(
                    clip,
                    value,
                    StringComparison.OrdinalIgnoreCase);

            if (ImGui.Selectable(
                    clip,
                    selected))
            {
                value =
                    clip;

                changed =
                    true;
            }

            if (selected)
            {
                ImGui.SetItemDefaultFocus();
            }
        }

        if (visibleCount ==
                0 &&
            clips.Count >
                0)
        {
            ImGui.TextDisabled(
                "No animations match the search.");
        }

        ImGui.EndCombo();

        return changed;
    }

    private void RegisterDocument(EditorLog log)
    {
        if (_asset == null) return;
        EditorDocumentId id = new(EditorDocumentType.AnimationProfile, _asset.Guid.ToString("N"));
        if (_registeredDocumentId.HasValue && _registeredDocumentId.Value != id)
            _documents.Unregister(_registeredDocumentId.Value);
        _registeredDocumentId = id;
        _documents.RegisterOrFocus(new EditorDocument(
            id,
            $"Animation Profile: {Path.GetFileNameWithoutExtension(_asset.ProjectPath)}",
            () => { _visible = true; _focusNextDraw = true; },
            RequestClose,
            () => { Save(log); return !_dirty; },
            () => Reload(log),
            () => _dirty));
    }

    private void RequestClose()
    {
        if (_dirty)
        {
            _pendingClose = true;
            _pendingOpenAsset = null;
            _pendingOpenProject = null;
            _openUnsavedPopup = true;
        }
        else CloseNow();
    }

    private void CloseNow()
    {
        _visible = false;
        if (_registeredDocumentId.HasValue)
        {
            _documents.Unregister(_registeredDocumentId.Value);
            _registeredDocumentId = null;
        }
    }

    private void DrawUnsavedPopup(EditorLog log)
    {
        if (_openUnsavedPopup)
        {
            ImGui.OpenPopup("Unsaved Animation Profile");
            _openUnsavedPopup = false;
        }
        bool open = true;
        if (!ImGui.BeginPopupModal("Unsaved Animation Profile", ref open,
                ImGuiWindowFlags.AlwaysAutoResize)) return;
        EditorUi.StatusBadge("UNSAVED", EditorStatusKind.Warning);
        ImGui.SameLine();
        EditorUi.MutedText("Save before continuing?");
        if (EditorUi.PrimaryButton("Save"))
        {
            Save(log);
            if (!_dirty) { CompletePendingAction(log); ImGui.CloseCurrentPopup(); }
        }
        ImGui.SameLine();
        if (EditorUi.DestructiveButton("Discard"))
        {
            Reload(log);
            CompletePendingAction(log);
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (EditorUi.SecondaryButton("Cancel"))
        {
            _pendingOpenAsset = null;
            _pendingOpenProject = null;
            _pendingClose = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    private void CompletePendingAction(EditorLog log)
    {
        AssetRecord? asset = _pendingOpenAsset;
        EditorProjectContext? project = _pendingOpenProject;
        bool close = _pendingClose;
        _pendingOpenAsset = null;
        _pendingOpenProject = null;
        _pendingClose = false;
        if (asset != null && project != null) Open(asset, project, log);
        else if (close) CloseNow();
    }

    private void Save(
        EditorLog log)
    {
        if (_profile ==
                null ||
            _asset ==
                null ||
            _project ==
                null)
        {
            return;
        }

        try
        {
            _profile.Normalize();

            AnimationProfileSerializer.Save(
                _asset.FullPath,
                _profile);

            _project.Assets.ReloadAnimationProfile(
                new AssetReference(
                    _asset.Guid,
                    _asset.ProjectPath));

            _dirty =
                false;

            _loadError =
                null;

            log.Info(
                $"Saved Animation Profile '{_asset.ProjectPath}'.");
        }
        catch (Exception exception)
        {
            _loadError =
                exception.Message;

            log.Error(
                $"Could not save Animation Profile '{_asset.ProjectPath}': {exception.Message}");
        }
    }

    public void Dispose() => _socketPreview.Dispose();

    private void Reload(
        EditorLog log)
    {
        if (_asset ==
            null)
        {
            return;
        }

        try
        {
            _profile =
                AnimationProfileSerializer.Load(
                    _asset.FullPath);

            InvalidateClipCache();

            _dirty =
                false;

            _loadError =
                null;
        }
        catch (Exception exception)
        {
            _profile =
                null;

            _loadError =
                exception.Message;

            log.Error(
                $"Could not reload Animation Profile '{_asset.ProjectPath}': {exception.Message}");
        }
    }
}

/// <summary>
/// Lightweight request bridge used by AnimationController's inspector button.
/// The Asset Browser owns the actual workspace window.
/// </summary>
internal static class AnimationProfileWorkspaceRequest
{
    private static AssetReference? _pending;

    public static void Request(
        AssetReference reference)
    {
        if (reference ==
                null ||
            reference.IsEmpty)
        {
            return;
        }

        _pending =
            new AssetReference(
                reference.Guid,
                reference.CachedProjectPath);
    }

    public static AssetReference? Consume()
    {
        AssetReference? pending =
            _pending;

        _pending =
            null;

        return pending;
    }

}
