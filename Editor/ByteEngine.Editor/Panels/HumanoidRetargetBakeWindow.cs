using System.Numerics;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;

using ImGuiNET;

namespace ByteEngine.Editor.Panels;

/// <summary>
/// Performance-first C9.5 retarget/bake authoring popup.
///
/// The preview deliberately avoids SceneFramebuffer, RenderWorld, physics,
/// shadows and post effects. It samples the temporary target-skeleton clip on
/// the CPU and draws a small animated skeleton directly through ImGui.
///
/// Nothing is registered/persisted until the user explicitly clicks Bake To
/// Character. Build Preview uses HumanoidRetargetRuntime.BuildClip, which is a
/// non-destructive temporary result.
/// </summary>
internal static class HumanoidRetargetBakeWindow
{
    private const string WindowId =
        "Retarget Animation To Character###HumanoidRetargetBake";

    private const float PreviewSampleInterval =
        1.0f / 30.0f;

    private static EditorProjectContext? _project;
    private static AssetReference _profileReference =
        AssetReference.Empty;
    private static AssetReference _targetReference =
        AssetReference.Empty;
    private static AssetReference _sourceReference =
        AssetReference.Empty;

    private static AnimationProfile? _profile;
    private static ModelAsset? _targetModel;
    private static ModelAsset? _sourceModel;
    private static ImportedAnimation? _sourceAnimation;
    private static ImportedAnimation? _previewAnimation;

    private static string _sourceClip =
        string.Empty;
    private static string _sourceSearch =
        string.Empty;
    private static string _clipSearch =
        string.Empty;
    private static string _outputName =
        string.Empty;
    private static string _status =
        string.Empty;
    private static bool _statusIsError;

    private static IReadOnlyList<string> _sourceClips =
        Array.Empty<string>();
    private static IReadOnlyList<ModelOwnedAnimationSummary> _bakedAnimations =
        Array.Empty<ModelOwnedAnimationSummary>();

    private static bool _focusNextDraw;
    private static bool _visible;
    private static bool _replaceExistingBaked;
    private static int _lastDrawFrame =
        -1;
    private static string _pendingRemoveAnimation =
        string.Empty;
    private static bool _conflictDirty =
        true;
    private static string _conflictName =
        string.Empty;
    private static ModelOwnedAnimationConflictKind _cachedConflict =
        ModelOwnedAnimationConflictKind.None;

    private static bool _playing =
        true;
    private static bool _loop =
        true;
    private static float _playbackSpeed =
        1.0f;
    private static float _previewTime;
    private static float _lastSampledPreviewTime =
        float.NaN;

    private static ImportedNode[] _nodes =
        Array.Empty<ImportedNode>();
    private static int[] _parentIndices =
        Array.Empty<int>();
    private static Vector3[] _basePositions =
        Array.Empty<Vector3>();
    private static Quaternion[] _baseRotations =
        Array.Empty<Quaternion>();
    private static Vector3[] _baseScales =
        Array.Empty<Vector3>();
    private static int[] _boneNodeIndices =
        Array.Empty<int>();
    private static Matrix4x4[] _locals =
        Array.Empty<Matrix4x4>();
    private static ImportedAnimationChannel?[] _previewChannels =
        Array.Empty<ImportedAnimationChannel?>();
    private static Matrix4x4[] _globals =
        Array.Empty<Matrix4x4>();
    private static bool[] _globalReady =
        Array.Empty<bool>();
    private static bool[] _globalResolving =
        Array.Empty<bool>();
    private static Vector3[] _bonePositions =
        Array.Empty<Vector3>();

    private static Vector3 _previewCenter =
        Vector3.Zero;
    private static float _previewRadius =
        1.0f;
    private static float _yaw =
        -90.0f;
    private static float _pitch =
        -8.0f;
    private static float _distance =
        3.0f;

    public static void Open(
        AssetReference profileReference,
        EditorProjectContext project)
    {
        ArgumentNullException.ThrowIfNull(
            profileReference);

        ArgumentNullException.ThrowIfNull(
            project);

        _project =
            project;

        _profileReference =
            new AssetReference(
                profileReference.Guid,
                profileReference.CachedProjectPath);

        _status =
            string.Empty;

        _statusIsError =
            false;

        _previewAnimation =
            null;

        _sourceAnimation =
            null;

        _sourceModel =
            null;

        _sourceClips =
            Array.Empty<string>();

        _sourceClip =
            string.Empty;

        _sourceSearch =
            string.Empty;

        _clipSearch =
            string.Empty;

        _outputName =
            string.Empty;

        _replaceExistingBaked =
            false;

        _pendingRemoveAnimation =
            string.Empty;

        _conflictDirty =
            true;

        _conflictName =
            string.Empty;

        _cachedConflict =
            ModelOwnedAnimationConflictKind.None;

        _previewTime =
            0.0f;

        _lastSampledPreviewTime =
            float.NaN;

        _playing =
            true;

        try
        {
            _profile =
                project.Assets.LoadAnimationProfile(
                    _profileReference);

            _profile.Normalize();

            _targetReference =
                _profile.Rig.ReferenceModel ??
                AssetReference.Empty;

            if (_targetReference.IsEmpty)
            {
                throw new InvalidOperationException(
                    "The Animation Profile has no Reference Model. Assign the character model first.");
            }

            _targetModel =
                project.Assets.LoadModel(
                    _targetReference);

            PreparePreviewRig();

            /*
             * Existing C9 profiles may already have a source model assigned.
             * Use it only as a convenient initial editor selection. Baking does
             * not persist or depend on this source reference.
             */
            RefreshBakedAnimationCache();

            AssetReference legacySource =
                _profile.Rig.AnimationSourceModel ??
                AssetReference.Empty;

            if (!legacySource.IsEmpty &&
                !SameAsset(
                    legacySource,
                    _targetReference))
            {
                SelectSource(
                    legacySource);
            }
        }
        catch (Exception exception)
        {
            _profile =
                null;

            _targetModel =
                null;

            _targetReference =
                AssetReference.Empty;

            SetStatus(
                exception.Message,
                true);
        }

        _focusNextDraw =
            true;

        _visible =
            true;
    }

    public static void Draw()
    {
        int frame =
            ImGui.GetFrameCount();

        if (_lastDrawFrame ==
            frame)
        {
            return;
        }

        _lastDrawFrame =
            frame;

        if (!_visible ||
            _project ==
                null)
        {
            return;
        }

        if (_focusNextDraw)
        {
            ImGui.SetNextWindowFocus();

            _focusNextDraw =
                false;
        }

        ImGui.SetNextWindowSize(
            new Vector2(
                1040.0f,
                690.0f),
            ImGuiCond.FirstUseEver);

        bool keepOpen =
            true;

        if (!ImGui.Begin(
                WindowId,
                ref keepOpen,
                ImGuiWindowFlags.NoCollapse))
        {
            ImGui.End();

            if (!keepOpen)
            {
                Close();
            }

            return;
        }

        float available =
            ImGui.GetContentRegionAvail().X;

        bool stacked = available < 760.0f;
        float leftWidth = stacked
            ? available
            : Math.Clamp(available * 0.38f, 320.0f, 430.0f);

        ImGui.BeginChild("##RetargetControls",
            new Vector2(leftWidth, stacked ? Math.Max(ImGui.GetContentRegionAvail().Y * 0.52f, 330.0f) : 0.0f),
            ImGuiChildFlags.Borders);
        DrawControls();
        ImGui.EndChild();

        if (!stacked) ImGui.SameLine();
        else ImGui.Spacing();

        ImGui.BeginChild("##RetargetPreview", Vector2.Zero, ImGuiChildFlags.Borders);
        DrawPreview();
        ImGui.EndChild();
        ImGui.End();

        if (!keepOpen)
        {
            Close();
        }
    }

    private static void DrawControls()
    {
        if (_project ==
            null)
        {
            return;
        }

        EditorUi.StatusBadge("TEMPORARY", EditorStatusKind.Neutral);
        ImGui.SameLine();
        EditorUi.MutedText("Nothing is written until Bake To Character.");

        ImGui.SeparatorText(
            "TARGET OWNER");

        AssetRecord? targetAsset =
            _targetReference.IsEmpty
                ? null
                : _project.AssetDatabase.Resolve(
                    _targetReference);

        ImGui.Text(
            targetAsset?.ProjectPath ??
            "No Reference Model");

        if (_targetModel !=
            null)
        {
            DrawRigStatus(
                "Target",
                _targetModel);

            ImGui.TextDisabled(
                $"Owned animations visible to engine: {_targetModel.Animations.Count}");
        }

        ImGui.SeparatorText(
            "SOURCE");

        DrawSourceModelPicker();

        if (_sourceModel !=
            null)
        {
            DrawRigStatus(
                "Source",
                _sourceModel);
        }

        DrawSourceClipPicker();

        ImGui.SetNextItemWidth(
            -1.0f);

        if (ImGui.InputText(
                "Bake Name",
                ref _outputName,
                128))
        {
            _replaceExistingBaked =
                false;

            _conflictDirty =
                true;
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "The name that will appear under the target model's existing animation drop-down arrow.");
        }

        ImGui.Spacing();

        bool canBuild =
            _targetModel !=
                null &&
            _sourceModel !=
                null &&
            _sourceAnimation !=
                null &&
            !string.IsNullOrWhiteSpace(
                _sourceClip);

        ImGui.BeginDisabled(
            !canBuild);

        if (EditorUi.SecondaryButton("Build Preview", size: new Vector2(-1.0f, 34.0f)))
        {
            BuildPreview();
        }

        ImGui.EndDisabled();

        if (_previewAnimation ==
            null)
        {
            EditorUi.EmptyState("No preview built", "Choose a source animation, then Build Preview.");
        }
        else
        {
            EditorUi.StatusBadge("PREVIEW READY", EditorStatusKind.Success);

            ImGui.TextDisabled(
                "Visual approval is still required before baking.");
        }

        if (!string.IsNullOrWhiteSpace(
                _status))
        {
            EditorUi.StatusBadge(_statusIsError ? "ERROR" : "SUCCESS",
                _statusIsError ? EditorStatusKind.Error : EditorStatusKind.Success);
            ImGui.SameLine();
            ImGui.TextWrapped(_status);
        }

        ImGui.SeparatorText(
            "BAKE");

        ModelOwnedAnimationConflictKind conflict =
            GetCurrentConflict();

        bool importedConflict =
            conflict ==
            ModelOwnedAnimationConflictKind.ImportedAnimation;

        bool bakedConflict =
            conflict ==
            ModelOwnedAnimationConflictKind.BakedAnimation;

        if (importedConflict)
        {
            ImGui.TextColored(
                new Vector4(
                    1.0f,
                    0.60f,
                    0.22f,
                    1.0f),
                "That name belongs to an animation embedded in the source model. Choose another name; imported clips are protected.");
        }
        else if (bakedConflict)
        {
            ImGui.TextColored(
                new Vector4(
                    1.0f,
                    0.68f,
                    0.25f,
                    1.0f),
                "A baked animation with this name already exists.");

            ImGui.Checkbox(
                "Replace existing baked animation",
                ref _replaceExistingBaked);
        }

        bool canBake =
            _previewAnimation !=
                null &&
            _targetModel !=
                null &&
            _sourceModel !=
                null &&
            _sourceAnimation !=
                null &&
            !string.IsNullOrWhiteSpace(
                _outputName) &&
            !importedConflict &&
            (!bakedConflict ||
             _replaceExistingBaked);

        ImGui.BeginDisabled(
            !canBake);

        if (EditorUi.PrimaryButton("Bake To Character", size: new Vector2(-1.0f, 38.0f)))
        {
            Bake();
        }

        ImGui.EndDisabled();

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Only this button persists the temporary result. A failed or rejected preview leaves the target model untouched.");
        }

        DrawBakedAnimationManagement();

        DrawLegacySourceMigration();

        ImGui.Spacing();

        if (EditorUi.SecondaryButton("Close"))
        {
            Close();
        }
    }

    private static void DrawBakedAnimationManagement()
    {
        if (_targetModel ==
            null)
        {
            return;
        }

        ImGui.Spacing();
        ImGui.SeparatorText(
            "RETARGETED ANIMATIONS ON TARGET");

        if (_bakedAnimations.Count ==
            0)
        {
            ImGui.TextDisabled(
                "No baked retargeted animations belong to this character yet.");

            return;
        }

        ImGui.TextDisabled(
            "Only retargeted/baked animations can be removed here. Native FBX animations are protected.");

        string remove =
            string.Empty;

        foreach (ModelOwnedAnimationSummary animation
                 in _bakedAnimations)
        {
            string animationName =
                animation.Name;

            ImGui.PushID(
                $"OwnedAnimation:{animationName}");

            ImGui.TextUnformatted(
                animationName);

            ModelOwnedAnimationProvenance provenance =
                animation.Provenance;

            if (!string.IsNullOrWhiteSpace(
                    provenance.SourceProjectPath))
            {
                ImGui.TextDisabled(
                    $"Source: {provenance.SourceProjectPath} / {provenance.SourceClipName}");
            }

            ImGui.SameLine();

            bool confirming =
                string.Equals(
                    _pendingRemoveAnimation,
                    animationName,
                    StringComparison.OrdinalIgnoreCase);

            if (!confirming)
            {
                if (ImGui.SmallButton(
                        "Remove..."))
                {
                    _pendingRemoveAnimation =
                        animationName;
                }
            }
            else
            {
                ImGui.TextColored(
                    new Vector4(
                        1.0f,
                        0.48f,
                        0.30f,
                        1.0f),
                    "Remove this animation?");

                ImGui.SameLine();

                if (ImGui.SmallButton(
                        "Confirm"))
                {
                    remove =
                        animationName;
                }

                ImGui.SameLine();

                if (ImGui.SmallButton(
                        "Cancel"))
                {
                    _pendingRemoveAnimation =
                        string.Empty;
                }
            }

            ImGui.Separator();
            ImGui.PopID();
        }

        if (string.IsNullOrWhiteSpace(
                remove) ||
            _project ==
                null)
        {
            return;
        }

        try
        {
            if (ModelOwnedAnimationStore.RemoveBakedAnimation(
                    _project.Assets.ProjectRoot,
                    _targetModel,
                    remove))
            {
                _pendingRemoveAnimation =
                    string.Empty;

                _conflictDirty =
                    true;

                RefreshBakedAnimationCache();

                SetStatus(
                    $"Removed retargeted animation '{remove}' from '{_targetModel.Name}'.",
                    false);
            }
        }
        catch (Exception exception)
        {
            SetStatus(
                exception.Message,
                true);
        }
    }

    private static void DrawLegacySourceMigration()
    {
        if (_project ==
                null ||
            _profileReference.IsEmpty ||
            _profile ==
                null)
        {
            return;
        }

        AssetReference legacySource =
            _profile.Rig.AnimationSourceModel ??
            AssetReference.Empty;

        if (legacySource.IsEmpty)
        {
            return;
        }

        ImGui.Spacing();
        ImGui.Separator();

        ImGui.TextColored(
            new Vector4(
                1.0f,
                0.68f,
                0.25f,
                1.0f),
            "Legacy runtime source still assigned");

        ImGui.TextWrapped(
            "Keep it while you still need unbaked source clips. After all required animations have been baked onto the target character, switch the profile to target-owned animations so its pickers use the character's own animation list.");

        ImGui.TextDisabled(
            "Save/close any open Animation Profile workspace before switching to avoid overwriting unsaved profile edits.");

        if (ImGui.Button(
                "Switch Profile To Target-Owned Animations",
                new Vector2(
                    -1.0f,
                    34.0f)))
        {
            try
            {
                AssetRecord? profileAsset =
                    _project.AssetDatabase.Resolve(
                        _profileReference);

                if (profileAsset ==
                        null ||
                    profileAsset.Type !=
                        AssetType.AnimationProfile)
                {
                    throw new InvalidOperationException(
                        "Animation Profile asset could not be resolved.");
                }

                _profile.Rig.AnimationSourceModel =
                    AssetReference.Empty;

                _profile.Normalize();

                AnimationProfileSerializer.Save(
                    profileAsset.FullPath,
                    _profile);

                _project.Assets.ReloadAnimationProfile(
                    _profileReference);

                SetStatus(
                    "Profile switched to target-owned animations. Locomotion/action pickers now resolve from the Reference Model and its baked clips.",
                    false);
            }
            catch (Exception exception)
            {
                SetStatus(
                    exception.Message,
                    true);
            }
        }
    }

    private static void DrawSourceModelPicker()
    {
        if (_project ==
            null)
        {
            return;
        }

        AssetRecord? current =
            _sourceReference.IsEmpty
                ? null
                : _project.AssetDatabase.Resolve(
                    _sourceReference);

        string preview =
            current?.ProjectPath ??
            "Choose source model...";

        if (!ImGui.BeginCombo(
                "Source Model",
                preview))
        {
            return;
        }

        if (ImGui.IsWindowAppearing())
        {
            _sourceSearch =
                string.Empty;

            ImGui.SetKeyboardFocusHere();
        }

        ImGui.InputTextWithHint(
            "##RetargetSourceSearch",
            "Search model assets...",
            ref _sourceSearch,
            128);

        foreach (AssetRecord asset
                 in _project.AssetDatabase.Assets
                     .Where(
                         candidate =>
                             candidate.Type ==
                                 AssetType.Model3D &&
                             candidate.Guid !=
                                 _targetReference.Guid)
                     .OrderBy(
                         candidate =>
                             candidate.ProjectPath,
                         StringComparer.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(
                    _sourceSearch) &&
                !asset.ProjectPath.Contains(
                    _sourceSearch,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool selected =
                asset.Guid ==
                _sourceReference.Guid;

            if (ImGui.Selectable(
                    asset.ProjectPath,
                    selected))
            {
                SelectSource(
                    new AssetReference(
                        asset.Guid,
                        asset.ProjectPath));
            }

            if (selected)
            {
                ImGui.SetItemDefaultFocus();
            }
        }

        ImGui.EndCombo();
    }

    private static void DrawSourceClipPicker()
    {
        string preview =
            string.IsNullOrWhiteSpace(
                _sourceClip)
                ? "Choose animation..."
                : _sourceClip;

        ImGui.BeginDisabled(
            _sourceModel ==
                null ||
            _sourceClips.Count ==
                0);

        if (ImGui.BeginCombo(
                "Source Animation",
                preview))
        {
            if (ImGui.IsWindowAppearing())
            {
                _clipSearch =
                    string.Empty;

                ImGui.SetKeyboardFocusHere();
            }

            ImGui.InputTextWithHint(
                "##RetargetClipSearch",
                "Search animations...",
                ref _clipSearch,
                128);

            foreach (string clip
                     in _sourceClips)
            {
                if (!string.IsNullOrWhiteSpace(
                        _clipSearch) &&
                    !clip.Contains(
                        _clipSearch,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                bool selected =
                    string.Equals(
                        clip,
                        _sourceClip,
                        StringComparison.OrdinalIgnoreCase);

                if (ImGui.Selectable(
                        clip,
                        selected))
                {
                    SelectSourceClip(
                        clip);
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        ImGui.EndDisabled();

        if (_sourceModel !=
                null &&
            _sourceClips.Count ==
                0)
        {
            ImGui.TextDisabled(
                "The selected source model has no animation clips.");
        }
    }

    private static void DrawRigStatus(
        string label,
        ModelAsset model)
    {
        bool ready =
            false;

        int errorCount =
            0;

        if (model.Skeleton !=
            null)
        {
            HumanoidRigDiagnosticReport diagnostics =
                HumanoidRigDiagnostics.Analyze(
                    model.Skeleton,
                    model.HumanoidMapping);

            HumanoidRigValidationResult validation =
                HumanoidRigMapper.Validate(
                    model.Skeleton,
                    model.HumanoidMapping);

            errorCount =
                diagnostics.Errors.Count;

            ready =
                model.RigType ==
                    AnimationRigType.Humanoid &&
                model.ReferenceHumanoidPose?.IsReady ==
                    true &&
                validation.IsReady &&
                errorCount ==
                    0;
        }

        ImGui.TextColored(
            ready
                ? new Vector4(
                    0.35f,
                    0.86f,
                    0.48f,
                    1.0f)
                : new Vector4(
                    1.0f,
                    0.58f,
                    0.24f,
                    1.0f),
            ready
                ? $"{label}: Humanoid ready ({model.HumanoidMapping.MappedCount} mapped bones)"
                : $"{label}: Humanoid setup needs attention ({errorCount} hierarchy error(s))");
    }

    private static void SelectSource(
        AssetReference reference)
    {
        if (_project ==
            null)
        {
            return;
        }

        _sourceReference =
            reference;

        _sourceModel =
            null;

        _sourceAnimation =
            null;

        _sourceClip =
            string.Empty;

        _outputName =
            string.Empty;

        _sourceClips =
            Array.Empty<string>();

        ClearPreview();

        try
        {
            _sourceModel =
                _project.Assets.LoadModel(
                    reference);

            _sourceClips =
                _sourceModel.Animations
                    .Select(
                        animation =>
                            animation.Name)
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

            SetStatus(
                $"Source loaded: {_sourceClips.Count} animation(s).",
                false);
        }
        catch (Exception exception)
        {
            SetStatus(
                exception.Message,
                true);
        }
    }

    private static void SelectSourceClip(
        string clip)
    {
        _sourceClip =
            clip;

        _outputName =
            clip;

        _sourceAnimation =
            _sourceModel?.Animations.FirstOrDefault(
                animation =>
                    string.Equals(
                        animation.Name,
                        clip,
                        StringComparison.OrdinalIgnoreCase));

        _replaceExistingBaked =
            false;

        _conflictDirty =
            true;

        ClearPreview();
    }

    private static void BuildPreview()
    {
        if (_project ==
                null ||
            _targetModel ==
                null ||
            _sourceModel ==
                null ||
            _sourceAnimation ==
                null)
        {
            return;
        }

        try
        {
            string previewName =
                string.IsNullOrWhiteSpace(
                    _outputName)
                    ? _sourceAnimation.Name
                    : _outputName.Trim();

            _previewAnimation =
                HumanoidRetargetRuntime.BuildClip(
                    _project.Assets,
                    _sourceReference,
                    _sourceClip,
                    _targetReference,
                    previewName);

            _previewTime =
                0.0f;

            _lastSampledPreviewTime =
                float.NaN;

            _playing =
                true;

            BindPreviewChannels();
            UpdateAnimatedSkeleton(
                0.0f);
            _lastSampledPreviewTime =
                0.0f;
            FramePreview();

            SetStatus(
                "Temporary retarget built successfully. Inspect the preview before baking.",
                false);
        }
        catch (Exception exception)
        {
            _previewAnimation =
                null;

            SetStatus(
                exception.Message,
                true);
        }
    }

    private static void Bake()
    {
        if (_project ==
                null ||
            _targetModel ==
                null ||
            _sourceModel ==
                null ||
            _sourceAnimation ==
                null ||
            _previewAnimation ==
                null)
        {
            return;
        }

        try
        {
            AssetRecord? sourceAsset =
                _project.AssetDatabase.Resolve(
                    _sourceReference);

            ModelOwnedAnimationBakeResult result =
                ModelOwnedAnimationStore.Bake(
                    _project.Assets.ProjectRoot,
                    _targetModel,
                    _previewAnimation,
                    _outputName,
                    _sourceModel.Guid,
                    sourceAsset?.ProjectPath,
                    _sourceAnimation.Key,
                    _sourceAnimation.Name,
                    _replaceExistingBaked);

            _replaceExistingBaked =
                false;

            _conflictDirty =
                true;

            RefreshBakedAnimationCache();

            SetStatus(
                result.Replaced
                    ? $"Replaced baked animation '{result.Animation.Name}' on {_targetModel.Name}."
                    : $"Baked '{result.Animation.Name}' onto {_targetModel.Name}. It is now available under the model's existing animation drop-down.",
                false);
        }
        catch (Exception exception)
        {
            SetStatus(
                exception.Message,
                true);
        }
    }

    private static ModelOwnedAnimationConflictKind GetCurrentConflict()
    {
        if (_project ==
                null ||
            _targetModel ==
                null ||
            string.IsNullOrWhiteSpace(
                _outputName))
        {
            return
                ModelOwnedAnimationConflictKind.None;
        }

        string normalizedName =
            _outputName.Trim();

        if (!_conflictDirty &&
            string.Equals(
                _conflictName,
                normalizedName,
                StringComparison.Ordinal))
        {
            return _cachedConflict;
        }

        try
        {
            _cachedConflict =
                ModelOwnedAnimationStore.GetConflict(
                    _project.Assets.ProjectRoot,
                    _targetModel,
                    normalizedName);
        }
        catch
        {
            _cachedConflict =
                ModelOwnedAnimationConflictKind.None;
        }

        _conflictName =
            normalizedName;

        _conflictDirty =
            false;

        return _cachedConflict;
    }

    private static void DrawPreview()
    {
        ImGui.TextColored(
            EditorTheme.AccentHover,
            "LIGHTWEIGHT RETARGET PREVIEW");

        ImGui.TextDisabled(
            "CPU skeleton preview - no framebuffer, scene physics, shadows or post-processing");

        ImGui.Separator();

        if (_previewAnimation ==
                null ||
            _targetModel?.Skeleton ==
                null ||
            _bonePositions.Length ==
                0)
        {
            Vector2 region =
                ImGui.GetContentRegionAvail();

            ImGui.SetCursorPosY(
                ImGui.GetCursorPosY() +
                Math.Max(
                    40.0f,
                    region.Y *
                    0.30f));

            ImGui.TextWrapped(
                "Choose a source animation and build the temporary preview. Nothing is added to the target character until Bake To Character is clicked.");

            return;
        }

        if (EditorUi.BeginToolbar("##RetargetPreviewToolbar"))
        {
            if (EditorUi.ToolbarButton(_playing ? "Pause" : "Play", "Play or pause the temporary preview"))
                _playing = !_playing;
            ImGui.SameLine();
            if (EditorUi.ToolbarButton("Restart", "Restart preview playback"))
            {
                _previewTime = 0.0f;
                _lastSampledPreviewTime = float.NaN;
            }
            ImGui.SameLine();
            if (EditorUi.ToolbarToggle("Loop", _loop, "Loop preview playback")) _loop = !_loop;
            EditorUi.ToolbarSeparator();
            ImGui.SetNextItemWidth(115.0f);
            ImGui.DragFloat("##PreviewSpeed", ref _playbackSpeed, 0.02f, 0.05f, 3.0f, "%.2fx");
            EditorUi.Tooltip("Preview playback speed");
            EditorUi.ToolbarSeparator();
            if (EditorUi.ToolbarButton("Frame", "Frame the preview")) FramePreview();
            EditorUi.EndToolbar();
        }
        float duration =
            Math.Max(
                _previewAnimation.Duration,
                0.0001f);

        if (_playing)
        {
            float delta =
                Math.Clamp(
                    ImGui.GetIO().DeltaTime,
                    0.0f,
                    0.1f) *
                Math.Clamp(
                    _playbackSpeed,
                    0.05f,
                    3.0f);

            _previewTime +=
                delta;

            if (_previewTime >
                duration)
            {
                _previewTime =
                    _loop
                        ? _previewTime %
                          duration
                        : duration;

                if (!_loop)
                {
                    _playing =
                        false;
                }
            }
        }

        float timeline =
            Math.Clamp(
                _previewTime,
                0.0f,
                duration);

        ImGui.SetNextItemWidth(
            -1.0f);

        if (ImGui.SliderFloat(
                "##RetargetPreviewTime",
                ref timeline,
                0.0f,
                duration,
                $"{timeline:0.00} / {duration:0.00}s"))
        {
            _previewTime =
                timeline;

            _lastSampledPreviewTime =
                float.NaN;

            _playing =
                false;
        }

        if (!float.IsFinite(
                _lastSampledPreviewTime) ||
            MathF.Abs(
                _lastSampledPreviewTime -
                    _previewTime) >=
            PreviewSampleInterval ||
            !_playing &&
            MathF.Abs(
                _lastSampledPreviewTime -
                    _previewTime) >
            0.00001f)
        {
            UpdateAnimatedSkeleton(
                _previewTime);

            _lastSampledPreviewTime =
                _previewTime;
        }

        Vector2 available =
            ImGui.GetContentRegionAvail();

        Vector2 canvasSize =
            new(
                Math.Max(
                    available.X,
                    320.0f),
                Math.Max(
                    available.Y,
                    360.0f));

        ImGui.InvisibleButton(
            "##RetargetSkeletonCanvas",
            canvasSize,
            ImGuiButtonFlags.MouseButtonLeft);

        Vector2 minimum =
            ImGui.GetItemRectMin();

        Vector2 maximum =
            ImGui.GetItemRectMax();

        ImDrawListPtr draw =
            ImGui.GetWindowDrawList();

        uint background =
            ImGui.GetColorU32(
                new Vector4(
                    0.055f,
                    0.060f,
                    0.070f,
                    1.0f));

        uint grid =
            ImGui.GetColorU32(
                new Vector4(
                    0.16f,
                    0.17f,
                    0.19f,
                    1.0f));

        uint bone =
            ImGui.GetColorU32(
                new Vector4(
                    0.58f,
                    0.76f,
                    1.0f,
                    1.0f));

        uint joint =
            ImGui.GetColorU32(
                new Vector4(
                    0.92f,
                    0.94f,
                    0.98f,
                    1.0f));

        draw.AddRectFilled(
            minimum,
            maximum,
            background);

        DrawPreviewGrid(
            draw,
            minimum,
            maximum,
            grid);

        bool hovered =
            ImGui.IsItemHovered();

        if (hovered &&
            ImGui.IsMouseDragging(
                ImGuiMouseButton.Left))
        {
            Vector2 delta =
                ImGui.GetIO().MouseDelta;

            _yaw +=
                delta.X *
                0.45f;

            _pitch =
                Math.Clamp(
                    _pitch +
                        delta.Y *
                        0.35f,
                    -80.0f,
                    80.0f);
        }

        if (hovered &&
            MathF.Abs(
                ImGui.GetIO().MouseWheel) >
            0.001f)
        {
            _distance =
                Math.Clamp(
                    _distance *
                    MathF.Pow(
                        0.90f,
                        ImGui.GetIO().MouseWheel),
                    Math.Max(
                        _previewRadius *
                        1.25f,
                        0.1f),
                    Math.Max(
                        _previewRadius *
                        12.0f,
                        2.0f));
        }

        Matrix4x4 viewProjection =
            BuildPreviewViewProjection(
                minimum,
                maximum);

        SkeletonAsset skeleton =
            _targetModel.Skeleton;

        for (int index = 0;
             index <
                skeleton.Bones.Count &&
             index <
                _bonePositions.Length;
             index++)
        {
            int parent =
                skeleton.Bones[index].ParentIndex;

            if (parent <
                    0 ||
                parent >=
                    _bonePositions.Length)
            {
                continue;
            }

            if (!TryProject(
                    _bonePositions[parent],
                    minimum,
                    maximum,
                    viewProjection,
                    out Vector2 from) ||
                !TryProject(
                    _bonePositions[index],
                    minimum,
                    maximum,
                    viewProjection,
                    out Vector2 to))
            {
                continue;
            }

            draw.AddLine(
                from,
                to,
                bone,
                2.0f);
        }

        Vector2 mouse =
            ImGui.GetIO().MousePos;

        int hoveredBone =
            -1;

        float hoveredDistanceSquared =
            64.0f;

        for (int index = 0;
             index <
                _bonePositions.Length;
             index++)
        {
            if (!TryProject(
                    _bonePositions[index],
                    minimum,
                    maximum,
                    viewProjection,
                    out Vector2 point))
            {
                continue;
            }

            draw.AddCircleFilled(
                point,
                2.5f,
                joint);

            if (hovered)
            {
                float distanceSquared =
                    Vector2.DistanceSquared(
                        mouse,
                        point);

                if (distanceSquared <
                    hoveredDistanceSquared)
                {
                    hoveredDistanceSquared =
                        distanceSquared;

                    hoveredBone =
                        index;
                }
            }
        }

        if (hoveredBone >=
                0 &&
            hoveredBone <
                skeleton.Bones.Count &&
            TryProject(
                _bonePositions[hoveredBone],
                minimum,
                maximum,
                viewProjection,
                out Vector2 labelPoint))
        {
            string label =
                skeleton.Bones[hoveredBone].Name;

            Vector2 labelSize =
                ImGui.CalcTextSize(
                    label);

            Vector2 labelMin =
                labelPoint +
                new Vector2(
                    8.0f,
                    -labelSize.Y -
                    5.0f);

            draw.AddRectFilled(
                labelMin -
                    new Vector2(
                        4.0f,
                        2.0f),
                labelMin +
                    labelSize +
                    new Vector2(
                        4.0f,
                        2.0f),
                ImGui.GetColorU32(
                    new Vector4(
                        0.04f,
                        0.04f,
                        0.05f,
                        0.92f)));

            draw.AddText(
                labelMin,
                joint,
                label);
        }

        draw.AddText(
            minimum +
                new Vector2(
                    10.0f,
                    10.0f),
            ImGui.GetColorU32(
                new Vector4(
                    0.72f,
                    0.74f,
                    0.78f,
                    1.0f)),
            "Drag: orbit   Wheel: zoom");
    }

    private static void DrawPreviewGrid(
        ImDrawListPtr draw,
        Vector2 minimum,
        Vector2 maximum,
        uint color)
    {
        float midY =
            maximum.Y -
            38.0f;

        draw.AddLine(
            new Vector2(
                minimum.X,
                midY),
            new Vector2(
                maximum.X,
                midY),
            color,
            1.0f);
    }

    private static Matrix4x4 BuildPreviewViewProjection(
        Vector2 minimum,
        Vector2 maximum)
    {
        float yaw =
            _yaw *
            MathF.PI /
            180.0f;

        float pitch =
            _pitch *
            MathF.PI /
            180.0f;

        Vector3 orbit =
            new(
                MathF.Cos(pitch) *
                    MathF.Cos(yaw),
                MathF.Sin(pitch),
                MathF.Cos(pitch) *
                    MathF.Sin(yaw));

        Vector3 camera =
            _previewCenter +
            orbit *
            Math.Max(
                _distance,
                0.1f);

        Matrix4x4 view =
            Matrix4x4.CreateLookAt(
                camera,
                _previewCenter,
                Vector3.UnitY);

        float width =
            Math.Max(
                maximum.X -
                    minimum.X,
                1.0f);

        float height =
            Math.Max(
                maximum.Y -
                    minimum.Y,
                1.0f);

        Matrix4x4 projection =
            Matrix4x4.CreatePerspectiveFieldOfView(
                42.0f *
                    MathF.PI /
                    180.0f,
                width /
                    height,
                0.01f,
                Math.Max(
                    _distance +
                        _previewRadius *
                        10.0f,
                    100.0f));

        return view *
               projection;
    }

    private static bool TryProject(
        Vector3 point,
        Vector2 minimum,
        Vector2 maximum,
        Matrix4x4 viewProjection,
        out Vector2 screen)
    {
        float width =
            Math.Max(
                maximum.X -
                    minimum.X,
                1.0f);

        float height =
            Math.Max(
                maximum.Y -
                    minimum.Y,
                1.0f);

        Vector4 clip =
            Vector4.Transform(
                new Vector4(
                    point,
                    1.0f),
                viewProjection);

        if (!float.IsFinite(
                clip.W) ||
            clip.W <=
                0.00001f)
        {
            screen =
                default;

            return false;
        }

        float x =
            clip.X /
            clip.W;

        float y =
            clip.Y /
            clip.W;

        if (!float.IsFinite(x) ||
            !float.IsFinite(y))
        {
            screen =
                default;

            return false;
        }

        screen =
            new Vector2(
                minimum.X +
                    (x *
                         0.5f +
                     0.5f) *
                    width,
                minimum.Y +
                    (1.0f -
                     (y *
                          0.5f +
                      0.5f)) *
                    height);

        return true;
    }

    private static void PreparePreviewRig()
    {
        if (_targetModel?.Skeleton ==
            null)
        {
            _nodes =
                Array.Empty<ImportedNode>();

            _bonePositions =
                Array.Empty<Vector3>();

            return;
        }

        _nodes =
            _targetModel.Nodes.ToArray();

        _parentIndices =
            new int[
                _nodes.Length];

        Array.Fill(
            _parentIndices,
            -1);

        _basePositions =
            new Vector3[
                _nodes.Length];

        _baseRotations =
            new Quaternion[
                _nodes.Length];

        _baseScales =
            new Vector3[
                _nodes.Length];

        var byKey =
            _nodes
                .Select(
                    (node, index) =>
                        (node.Key, index))
                .ToDictionary(
                    item =>
                        item.Key,
                    item =>
                        item.index,
                    StringComparer.Ordinal);

        var byName =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        for (int index = 0;
             index <
                _nodes.Length;
             index++)
        {
            ImportedNode node =
                _nodes[index];

            byName.TryAdd(
                node.Name,
                index);

            if (node.ParentKey !=
                    null &&
                byKey.TryGetValue(
                    node.ParentKey,
                    out int parent))
            {
                _parentIndices[index] =
                    parent;
            }

            if (!Matrix4x4.Decompose(
                    node.LocalTransform,
                    out Vector3 scale,
                    out Quaternion rotation,
                    out Vector3 translation))
            {
                scale =
                    Vector3.One;

                rotation =
                    Quaternion.Identity;

                translation =
                    node.LocalTransform.Translation;
            }

            _basePositions[index] =
                translation;

            _baseRotations[index] =
                NormalizeSafe(
                    rotation);

            _baseScales[index] =
                SanitizeScale(
                    scale);
        }

        SkeletonAsset skeleton =
            _targetModel.Skeleton;

        _boneNodeIndices =
            new int[
                skeleton.Bones.Count];

        Array.Fill(
            _boneNodeIndices,
            -1);

        for (int index = 0;
             index <
                skeleton.Bones.Count;
             index++)
        {
            if (byName.TryGetValue(
                    skeleton.Bones[index].Name,
                    out int nodeIndex))
            {
                _boneNodeIndices[index] =
                    nodeIndex;
            }
        }

        _locals =
            new Matrix4x4[
                _nodes.Length];

        _previewChannels =
            new ImportedAnimationChannel?[
                _nodes.Length];

        _globals =
            new Matrix4x4[
                _nodes.Length];

        _globalReady =
            new bool[
                _nodes.Length];

        _globalResolving =
            new bool[
                _nodes.Length];

        _bonePositions =
            new Vector3[
                skeleton.Bones.Count];

        UpdateAnimatedSkeleton(
            0.0f);

        FramePreview();
    }

    private static void BindPreviewChannels()
    {
        if (_previewChannels.Length !=
            _nodes.Length)
        {
            _previewChannels =
                new ImportedAnimationChannel?[
                    _nodes.Length];
        }

        Array.Clear(
            _previewChannels,
            0,
            _previewChannels.Length);

        if (_previewAnimation ==
            null)
        {
            return;
        }

        var byName =
            _previewAnimation.Channels
                .Where(
                    channel =>
                        !string.IsNullOrWhiteSpace(
                            channel.NodeName))
                .GroupBy(
                    channel =>
                        channel.NodeName,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group.First(),
                    StringComparer.OrdinalIgnoreCase);

        for (int index = 0;
             index <
                _nodes.Length;
             index++)
        {
            if (byName.TryGetValue(
                    _nodes[index].Name,
                    out ImportedAnimationChannel? channel))
            {
                _previewChannels[index] =
                    channel;
            }
        }
    }

    private static void UpdateAnimatedSkeleton(
        float time)
    {
        if (_targetModel?.Skeleton ==
                null ||
            _nodes.Length ==
                0)
        {
            return;
        }

        for (int index = 0;
             index <
                _nodes.Length;
             index++)
        {
            ImportedNode node =
                _nodes[index];

            ImportedAnimationChannel? channel =
                index <
                        _previewChannels.Length
                    ? _previewChannels[index]
                    : null;

            Vector3 position =
                AnimationPoseSampler.Sample(
                    channel?.Translation,
                    time,
                    _basePositions[index]);

            Quaternion rotation =
                AnimationPoseSampler.Sample(
                    channel?.Rotation,
                    time,
                    _baseRotations[index]);

            Vector3 scale =
                AnimationPoseSampler.Sample(
                    channel?.Scale,
                    time,
                    _baseScales[index]);

            scale =
                SanitizeScale(
                    scale);

            _locals[index] =
                Matrix4x4.CreateScale(
                    scale) *
                Matrix4x4.CreateFromQuaternion(
                    rotation) *
                Matrix4x4.CreateTranslation(
                    position);
        }

        Array.Clear(
            _globalReady,
            0,
            _globalReady.Length);

        Array.Clear(
            _globalResolving,
            0,
            _globalResolving.Length);

        for (int index = 0;
             index <
                _nodes.Length;
             index++)
        {
            ResolveGlobal(
                index,
                _locals);
        }

        for (int boneIndex = 0;
             boneIndex <
                _boneNodeIndices.Length;
             boneIndex++)
        {
            int nodeIndex =
                _boneNodeIndices[boneIndex];

            _bonePositions[boneIndex] =
                nodeIndex >=
                        0 &&
                    nodeIndex <
                        _globals.Length
                    ? _globals[nodeIndex].Translation
                    : Vector3.Zero;
        }
    }

    private static Matrix4x4 ResolveGlobal(
        int index,
        Matrix4x4[] locals)
    {
        if (_globalReady[index])
        {
            return _globals[index];
        }

        if (_globalResolving[index])
        {
            /* Malformed cyclic hierarchies must not crash the editor preview. */
            _globals[index] =
                locals[index];

            _globalReady[index] =
                true;

            return _globals[index];
        }

        _globalResolving[index] =
            true;

        int parent =
            _parentIndices[index];

        _globals[index] =
            parent >=
                    0 &&
                parent <
                    locals.Length
                ? locals[index] *
                  ResolveGlobal(
                      parent,
                      locals)
                : locals[index];

        _globalResolving[index] =
            false;

        _globalReady[index] =
            true;

        return _globals[index];
    }

    private static void FramePreview()
    {
        if (_bonePositions.Length ==
            0)
        {
            _previewCenter =
                Vector3.Zero;

            _previewRadius =
                1.0f;

            _distance =
                3.0f;

            return;
        }

        Vector3 minimum =
            new(float.PositiveInfinity);

        Vector3 maximum =
            new(float.NegativeInfinity);

        bool any =
            false;

        foreach (Vector3 point
                 in _bonePositions)
        {
            if (!IsFinite(
                    point))
            {
                continue;
            }

            minimum =
                Vector3.Min(
                    minimum,
                    point);

            maximum =
                Vector3.Max(
                    maximum,
                    point);

            any =
                true;
        }

        if (!any)
        {
            _previewCenter =
                Vector3.Zero;

            _previewRadius =
                1.0f;
        }
        else
        {
            _previewCenter =
                (minimum +
                 maximum) *
                0.5f;

            _previewRadius =
                Math.Max(
                    Vector3.Distance(
                        minimum,
                        maximum) *
                    0.5f,
                    0.1f);
        }

        _distance =
            Math.Max(
                _previewRadius *
                2.8f,
                0.8f);
    }

    private static void ClearPreview()
    {
        _previewAnimation =
            null;

        if (_previewChannels.Length >
            0)
        {
            Array.Clear(
                _previewChannels,
                0,
                _previewChannels.Length);
        }

        _previewTime =
            0.0f;

        _lastSampledPreviewTime =
            float.NaN;

        _replaceExistingBaked =
            false;
    }

    private static void RefreshBakedAnimationCache()
    {
        if (_project ==
                null ||
            _targetModel ==
                null)
        {
            _bakedAnimations =
                Array.Empty<ModelOwnedAnimationSummary>();

            return;
        }

        try
        {
            _bakedAnimations =
                ModelOwnedAnimationStore.GetBakedAnimationSummaries(
                    _project.Assets.ProjectRoot,
                    _targetModel.Guid);
        }
        catch
        {
            _bakedAnimations =
                Array.Empty<ModelOwnedAnimationSummary>();
        }
    }

    private static void SetStatus(
        string message,
        bool error)
    {
        _status =
            message;

        _statusIsError =
            error;
    }

    private static void Close()
    {
        _visible =
            false;

        _focusNextDraw =
            false;

        _project =
            null;

        _profile =
            null;

        _previewAnimation =
            null;

        _sourceAnimation =
            null;

        _sourceModel =
            null;

        _targetModel =
            null;

        _sourceClips =
            Array.Empty<string>();

        _bakedAnimations =
            Array.Empty<ModelOwnedAnimationSummary>();

        _pendingRemoveAnimation =
            string.Empty;
    }

    private static bool SameAsset(
        AssetReference left,
        AssetReference right)
    {
        if (left.Guid !=
                Guid.Empty &&
            right.Guid !=
                Guid.Empty)
        {
            return left.Guid ==
                right.Guid;
        }

        return string.Equals(
            left.CachedProjectPath,
            right.CachedProjectPath,
            StringComparison.OrdinalIgnoreCase);
    }

    private static Quaternion NormalizeSafe(
        Quaternion value)
    {
        return value.LengthSquared() >
                0.000001f
            ? Quaternion.Normalize(
                value)
            : Quaternion.Identity;
    }

    private static Vector3 SanitizeScale(
        Vector3 value)
    {
        return
            new Vector3(
                float.IsFinite(
                    value.X)
                    ? value.X
                    : 1.0f,
                float.IsFinite(
                    value.Y)
                    ? value.Y
                    : 1.0f,
                float.IsFinite(
                    value.Z)
                    ? value.Z
                    : 1.0f);
    }

    private static bool IsFinite(
        Vector3 value)
    {
        return
            float.IsFinite(
                value.X) &&
            float.IsFinite(
                value.Y) &&
            float.IsFinite(
                value.Z);
    }
}
