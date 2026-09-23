using System.Numerics;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;

using ImGuiNET;

namespace ByteEngine.Editor.Panels;

/// <summary>
/// Proper Apply/Revert importer surface for FBX/glTF model + animation assets.
///
/// The importer intentionally exposes only settings that are wired to actual
/// ByteEngine behavior. Mesh, rig and embedded animation controls live together
/// because one FBX/glTF file can contain any combination of them.
/// </summary>
internal sealed class ModelImporterInspector
{
    private Guid _assetGuid;
    private HumanoidMappingResult? _mappingAnalysis;
    private ModelAsset? _analysisModel;

    private ModelImporterSettings _draft =
        new();

    private bool _open;
    private bool _dirty;

    private string _status =
        string.Empty;

    private bool _statusIsError;

    private string _boneSearch =
        string.Empty;

    private string _retargetSourceSearch =
        string.Empty;

    private string _retargetRigSearch =
        string.Empty;

    private string _retargetTargetSearch =
        string.Empty;

    private AssetReference _retargetSourceReference =
        AssetReference.Empty;

    private AssetReference _retargetSourceRigReference =
        AssetReference.Empty;

    private AssetReference _retargetTargetReference =
        AssetReference.Empty;

    private string _retargetClipKey =
        string.Empty;

    private string _retargetOutputName =
        string.Empty;

    private ImportedAnimation? _retargetPreview;

    private string _retargetStatus =
        string.Empty;

    private bool _retargetStatusIsError;

    private bool _replaceExistingRetarget;

    private string _lastSelectedAnimationKey =
        string.Empty;

    /// <summary>
    /// Draws the launcher beside the animation preview and, when requested, the
    /// full importer window. Returns true after Apply successfully reimports the
    /// asset so the caller can discard any preview resources tied to the old
    /// ModelAsset instance.
    /// </summary>
    public bool Draw(
        EditorProjectContext project,
        AssetRecord asset,
        ModelAsset model,
        ImportedAnimation? selectedAnimation)
    {
        ArgumentNullException.ThrowIfNull(
            project);

        ArgumentNullException.ThrowIfNull(
            asset);

        ArgumentNullException.ThrowIfNull(
            model);

        EnsureDraft(
            asset,
            model);

        if (ImGui.Button(
                "Import Settings & Retarget (Experimental)..."))
        {
            _open =
                true;
        }

        ImGui.SameLine();

        if (model.RigType ==
            AnimationRigType.Humanoid)
        {
            EditorUi.StatusBadge(
                model.HumanoidRigWasAutoDetected
                    ? "HUMANOID / AUTO"
                    : "HUMANOID",
                EditorStatusKind.Success);
        }
        else
        {
            EditorUi.StatusBadge(
                "GENERIC",
                EditorStatusKind.Neutral);
        }

        if (!_open)
        {
            return false;
        }

        return DrawWindow(
            project,
            asset,
            model,
            selectedAnimation);
    }

    private void EnsureDraft(
        AssetRecord asset,
        ModelAsset model)
    {
        if (_assetGuid == asset.Guid)
        {
            if (!ReferenceEquals(_analysisModel, model))
            {
                _analysisModel = model;
                _mappingAnalysis = HumanoidSkeletonAnalyzer.Analyze(model.Skeleton);
            }
            return;
        }

        _assetGuid = asset.Guid;
        _analysisModel = model;
        _mappingAnalysis = HumanoidSkeletonAnalyzer.Analyze(model.Skeleton);

        _draft =
            asset.Metadata.ModelImporter.Clone();

        /*
         * Auto detection happens during model construction and intentionally
         * updates the in-memory metadata. Mirror the effective result into the
         * draft so the user immediately sees what ByteEngine actually detected.
         */
        if (_draft.AutoDetectHumanoidRig &&
            model.RigType ==
                AnimationRigType.Humanoid)
        {
            _draft.RigType =
                AnimationRigType.Humanoid;

            _draft.HumanoidMapping =
                model.HumanoidMapping.Clone();
        }

        _dirty =
            false;

        _status =
            string.Empty;

        _statusIsError =
            false;

        _boneSearch =
            string.Empty;

        _retargetSourceSearch =
            string.Empty;

        _retargetRigSearch =
            string.Empty;

        _retargetTargetSearch =
            string.Empty;

        _retargetSourceReference =
            new AssetReference(
                asset.Guid,
                asset.ProjectPath);

        _retargetSourceRigReference =
            _draft.AnimationSourceRigModel;

        _retargetTargetReference =
            _draft.DefaultRetargetTargetModel;

        ImportedAnimation? initialClip =
            model.Animations.FirstOrDefault();

        _retargetClipKey =
            initialClip?.Key ??
            string.Empty;

        _retargetOutputName =
            initialClip?.Name ??
            string.Empty;

        _retargetPreview =
            null;

        _retargetStatus =
            string.Empty;

        _retargetStatusIsError =
            false;

        _replaceExistingRetarget =
            false;

        _lastSelectedAnimationKey =
            string.Empty;
    }

    private bool DrawWindow(
        EditorProjectContext project,
        AssetRecord asset,
        ModelAsset model,
        ImportedAnimation? selectedAnimation)
    {
        bool open =
            _open;

        string displayName =
            Path.GetFileName(
                asset.ProjectPath);

        ImGui.SetNextWindowSize(
            new Vector2(
                720.0f,
                650.0f),
            ImGuiCond.FirstUseEver);

        bool visible =
            ImGui.Begin(
                $"Import Settings: {displayName}{(_dirty ? " *" : string.Empty)}###ModelAnimationImporter",
                ref open,
                ImGuiWindowFlags.NoCollapse);

        _open =
            open;

        bool reimported =
            false;

        if (visible)
        {
            DrawSummary(
                asset,
                model,
                selectedAnimation);

            if (ImGui.BeginTabBar(
                    "##ModelImporterTabs"))
            {
                if (ImGui.BeginTabItem(
                        "Model"))
                {
                    DrawModelTab(
                        model);

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem(
                        "Rig"))
                {
                    DrawRigTab(
                        model);

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem(
                        "Animation"))
                {
                    DrawAnimationTab(
                        project,
                        asset,
                        model,
                        selectedAnimation);

                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }

            ImGui.Separator();

            if (!string.IsNullOrWhiteSpace(
                    _status))
            {
                EditorUi.StatusBadge(
                    _statusIsError
                        ? "ERROR"
                        : "APPLIED",
                    _statusIsError
                        ? EditorStatusKind.Error
                        : EditorStatusKind.Success);

                ImGui.SameLine();

                ImGui.TextWrapped(
                    _status);
            }

            ImGui.BeginDisabled(
                !_dirty);

            if (EditorUi.PrimaryButton(
                    "Apply & Reimport",
                    size:
                        new Vector2(
                            155.0f,
                            32.0f)))
            {
                reimported =
                    Apply(
                        project,
                        asset);
            }

            ImGui.EndDisabled();

            ImGui.SameLine();

            if (EditorUi.SecondaryButton(
                    "Revert",
                    size:
                        new Vector2(
                            90.0f,
                            32.0f)))
            {
                Revert(
                    asset,
                    model);
            }
        }

        ImGui.End();

        return reimported;
    }

    private static void DrawSummary(
        AssetRecord asset,
        ModelAsset model,
        ImportedAnimation? selectedAnimation)
    {
        bool animationSource =
            model.Animations.Count >
                0 &&
            model.Meshes.Count ==
                0;

        EditorUi.StatusBadge(
            animationSource
                ? "ANIMATION SOURCE"
                : model.Animations.Count >
                    0
                    ? "MODEL + ANIMATION"
                    : "MODEL",
            animationSource
                ? EditorStatusKind.Success
                : EditorStatusKind.Neutral);

        ImGui.SameLine();

        ImGui.TextUnformatted(
            Path.GetFileName(
                asset.ProjectPath));

        ImGui.TextDisabled(
            asset.ProjectPath);

        if (selectedAnimation !=
            null)
        {
            ImGui.TextDisabled(
                $"Selected clip: {selectedAnimation.Name}  ({selectedAnimation.Duration:0.###} s)");
        }

        ImGui.Separator();
    }

    private void DrawModelTab(
        ModelAsset model)
    {
        bool hasMeshes =
            model.Meshes.Count >
            0;

        ImGui.TextWrapped(
            "Geometry/material settings affect the source asset when Apply & Reimport is pressed.");

        float scale =
            _draft.ImportScale;

        if (ImGui.DragFloat(
                "Import Scale",
                ref scale,
                0.01f,
                0.0001f,
                1000.0f,
                "%.4f"))
        {
            _draft.ImportScale =
                Math.Max(
                    scale,
                    0.0001f);

            MarkDirty();
        }

        ImGui.BeginDisabled(
            !hasMeshes);

        bool generateNormals =
            _draft.GenerateNormals;

        if (ImGui.Checkbox(
                "Generate Normals",
                ref generateNormals))
        {
            _draft.GenerateNormals =
                generateNormals;

            MarkDirty();
        }

        bool embeddedMaterials =
            _draft.PreferEmbeddedMaterials;

        if (ImGui.Checkbox(
                "Prefer Embedded Materials",
                ref embeddedMaterials))
        {
            _draft.PreferEmbeddedMaterials =
                embeddedMaterials;

            MarkDirty();
        }

        ImGui.EndDisabled();

        if (!hasMeshes)
        {
            ImGui.TextDisabled(
                "This is an animation/skeleton source with no render mesh; geometry and material options are not used.");
        }

        ImGui.SeparatorText(
            "SOURCE SUMMARY");

        EditorUi.LabelValue(
            "Meshes",
            model.Meshes.Count.ToString());

        EditorUi.LabelValue(
            "Materials",
            model.Materials.Count.ToString());

        EditorUi.LabelValue(
            "Skeleton",
            model.Skeleton?.Name ??
            "None");

        EditorUi.LabelValue(
            "Bones",
            (model.Skeleton?.Bones.Count ??
             0).ToString());
    }

    private void DrawRigTab(
        ModelAsset model)
    {
        ImGui.TextWrapped(
            "Auto is the default for animation imports. It promotes a source to Humanoid only when ByteEngine can map and validate the complete required human skeleton.");

        int rigMode =
            _draft.AutoDetectHumanoidRig
                ? 0
                : _draft.RigType ==
                    AnimationRigType.Humanoid
                    ? 2
                    : 1;

        string[] rigModes =
        {
            "Auto",
            "Generic",
            "Humanoid"
        };

        if (ImGui.Combo(
                "Rig Mode",
                ref rigMode,
                rigModes,
                rigModes.Length))
        {
            switch (rigMode)
            {
                case 0:
                    _draft.AutoDetectHumanoidRig =
                        true;

                    ApplyAutomaticMapping(
                        model);

                    break;

                case 2:
                    _draft.AutoDetectHumanoidRig =
                        false;

                    _draft.RigType =
                        AnimationRigType.Humanoid;

                    if (_draft.HumanoidMapping.MappedCount ==
                            0 &&
                        model.Skeleton !=
                            null)
                    {
                        _draft.HumanoidMapping =
                            HumanoidRigMapper.AutoMap(
                                model.Skeleton);
                    }

                    break;

                default:
                    _draft.AutoDetectHumanoidRig =
                        false;

                    _draft.RigType =
                        AnimationRigType.Generic;

                    break;
            }

            MarkDirty();
        }

        if (_draft.AutoDetectHumanoidRig)
        {
            EditorUi.StatusBadge(
                _draft.RigType ==
                    AnimationRigType.Humanoid
                    ? "AUTO: HUMANOID"
                    : "AUTO: GENERIC",
                _draft.RigType ==
                    AnimationRigType.Humanoid
                    ? EditorStatusKind.Success
                    : EditorStatusKind.Neutral);

            ImGui.SameLine();

            ImGui.TextDisabled(
                "Switch to explicit Humanoid if you want to manually edit bone mapping.");
        }

        if (model.Skeleton ==
            null)
        {
            ImGui.Spacing();
            EditorUi.StatusBadge(
                "NO SKELETON",
                EditorStatusKind.Warning);
            ImGui.SameLine();
            ImGui.TextWrapped(
                "Experimental Humanoid retargeting requires a skeleton hierarchy.");
            return;
        }

        if (_draft.RigType !=
            AnimationRigType.Humanoid)
        {
            ImGui.Spacing();
            ImGui.TextDisabled(
                "Generic preserves the source skeleton exactly as authored and does not participate in experimental Humanoid retargeting.");
            return;
        }

        ImGui.Spacing();

        HumanoidRigValidationResult validation =
            HumanoidRigMapper.Validate(
                model.Skeleton,
                _draft.HumanoidMapping);

        HumanoidRigDiagnosticReport diagnostics =
            HumanoidRigDiagnostics.Analyze(
                model.Skeleton,
                _draft.HumanoidMapping);

        bool ready =
            validation.IsReady &&
            diagnostics.Errors.Count ==
                0;

        EditorUi.StatusBadge(
            ready
                ? "HUMANOID READY"
                : "NEEDS ATTENTION",
            ready
                ? EditorStatusKind.Success
                : EditorStatusKind.Warning);

        ImGui.SameLine();

        ImGui.TextDisabled($"{validation.RequiredMappedCount}/{HumanoidBoneCatalog.Required.Count} required, {validation.MappedBoneCount} total mapped");
        if (_mappingAnalysis?.IsHumanoid == true)
        {
            ImGui.TextDisabled($"Geometry auto-map confidence: {_mappingAnalysis.OverallConfidence:P0}");
            if (_mappingAnalysis.NeedsReview.Count > 0)
                ImGui.TextWrapped("Review: " + string.Join(", ", _mappingAnalysis.NeedsReview));
        }

        foreach (string error
                 in diagnostics.Errors)
        {
            EditorUi.StatusBadge(
                "ERROR",
                EditorStatusKind.Error);
            ImGui.SameLine();
            ImGui.TextWrapped(
                error);
        }

        foreach (string warning
                 in diagnostics.Warnings)
        {
            EditorUi.StatusBadge(
                "WARNING",
                EditorStatusKind.Warning);
            ImGui.SameLine();
            ImGui.TextWrapped(
                warning);
        }

        ImGui.BeginDisabled(
            _draft.AutoDetectHumanoidRig);

        if (ImGui.Button(
                "Auto Map Bones"))
        {
            _draft.HumanoidMapping =
                HumanoidRigMapper.AutoMap(
                    model.Skeleton);

            MarkDirty();
        }

        if (ImGui.TreeNode(
                "Advanced Bone Mapping"))
        {
            ImGui.InputTextWithHint(
                "##ImporterBoneSearch",
                "Filter humanoid slots...",
                ref _boneSearch,
                96);

            string[] sourceBones =
                model.Skeleton.Bones
                    .Select(
                        bone =>
                            bone.Name)
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

            foreach (HumanoidBone semantic
                     in Enum.GetValues<HumanoidBone>())
            {
                string display =
                    ByteEngine.Editor.HumanoidRigAuthoring.DisplayName(
                        semantic);

                if (!string.IsNullOrWhiteSpace(
                        _boneSearch) &&
                    !display.Contains(
                        _boneSearch,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? mapped =
                    _draft.HumanoidMapping.GetBoneName(
                        semantic);

                string label =
                    HumanoidBoneCatalog.IsRequired(
                        semantic)
                        ? $"{display} *"
                        : display;

                if (!ImGui.BeginCombo(
                        label,
                        mapped ??
                        "None"))
                {
                    continue;
                }

                if (ImGui.Selectable(
                        "None",
                        mapped ==
                            null))
                {
                    _draft.HumanoidMapping.ClearBone(
                        semantic);

                    MarkDirty();
                }

                foreach (string boneName
                         in sourceBones)
                {
                    bool selected =
                        string.Equals(
                            mapped,
                            boneName,
                            StringComparison.OrdinalIgnoreCase);

                    if (ImGui.Selectable(
                            boneName,
                            selected))
                    {
                        ByteEngine.Editor.HumanoidRigAuthoring.ClearDuplicateAssignment(
                            _draft.HumanoidMapping,
                            semantic,
                            boneName);

                        _draft.HumanoidMapping.SetBone(
                            semantic,
                            boneName);

                        MarkDirty();
                    }

                    if (selected)
                    {
                        ImGui.SetItemDefaultFocus();
                    }
                }

                ImGui.EndCombo();
            }

            ImGui.TreePop();
        }

        ImGui.EndDisabled();
    }

    private void DrawAnimationTab(
        EditorProjectContext project,
        AssetRecord asset,
        ModelAsset model,
        ImportedAnimation? selectedAnimation)
    {
        ImGui.TextWrapped(
            model.Meshes.Count == 0
                ? "Animation-only FBX: no render mesh is needed. Use its embedded Humanoid hierarchy, or assign the character model this clip was authored for."
                : "Import embedded clips and choose a source rig only when the file does not contain a usable Humanoid hierarchy.");

        bool importAnimations =
            _draft.ImportAnimations;

        if (ImGui.Checkbox(
                "Import Animations",
                ref importAnimations))
        {
            _draft.ImportAnimations =
                importAnimations;

            MarkDirty();
            InvalidateRetargetPreview();
        }

        if (ImGui.CollapsingHeader("Advanced animation settings"))
        {
            ImGui.BeginDisabled(
                !_draft.ImportAnimations);

            float samples =
                _draft.RetargetSamplesPerSecond;

            if (ImGui.DragFloat(
                    "Retarget Sample Rate (Experimental)",
                    ref samples,
                    1.0f,
                    15.0f,
                    240.0f,
                    "%.0f fps"))
            {
                _draft.RetargetSamplesPerSecond =
                    Math.Clamp(
                        samples,
                        15.0f,
                        240.0f);

                MarkDirty();
                InvalidateRetargetPreview();
            }

            int rootMotionSource =
                (int)_draft.RootMotionSource;

            string[] rootMotionNames =
            {
                "Automatic",
                "Skeleton Root",
                "Hips / Pelvis"
            };

            if (ImGui.Combo(
                    "Root Motion Source",
                    ref rootMotionSource,
                    rootMotionNames,
                    rootMotionNames.Length))
            {
                _draft.RootMotionSource =
                    (AnimationRootMotionSource)rootMotionSource;

                MarkDirty();
                InvalidateRetargetPreview();
            }

            ImGui.TextDisabled(
                _draft.RootMotionSource switch
                {
                    AnimationRootMotionSource.SkeletonRoot =>
                        "Use only explicit skeleton-root travel; never generate root motion from the pelvis.",

                    AnimationRootMotionSource.Hips =>
                        "Generate the target root trajectory from Hips/Pelvis travel.",

                    _ =>
                        "Prefer explicit root travel; fall back to Hips/Pelvis when the source has no useful root track."
                });

            ImGui.EndDisabled();
        }

        if (!_draft.ImportAnimations)
        {
            EditorUi.StatusBadge(
                "ANIMATIONS DISABLED",
                EditorStatusKind.Warning);
            ImGui.SameLine();
            ImGui.TextWrapped(
                "Apply will remove embedded clips from ByteEngine's imported ModelAsset until this option is enabled again.");
        }

        ImGui.SeparatorText(
            "SOURCE RIG ASSIGNMENT");

        bool sourceHasReadyEmbeddedRig =
            IsHumanoidReady(
                model);

        if (sourceHasReadyEmbeddedRig)
        {
            EditorUi.StatusBadge(
                "EMBEDDED HUMANOID RIG READY",
                EditorStatusKind.Success);
            ImGui.SameLine();
            ImGui.TextWrapped(
                model.Meshes.Count == 0
                    ? "No render mesh, but this FBX contains a complete Humanoid hierarchy. Source Model can stay on Auto."
                    : "The embedded Humanoid rig is ready. No source-model assignment is needed.");
        }
        else
        {
            EditorUi.StatusBadge(
                model.Skeleton == null
                    ? "NO EMBEDDED SKELETON"
                    : "EMBEDDED RIG NOT READY",
                EditorStatusKind.Warning);
            ImGui.SameLine();
            ImGui.TextWrapped(
                "Assign the Humanoid model/rig this animation was authored for. That model supplies the missing skeleton, semantic map and reference pose.");
        }

        AssetReference importerRigReference =
            _draft.AnimationSourceRigModel;

        if (DrawModelAssetPicker(
                project,
                sourceHasReadyEmbeddedRig ? "Source Model (optional)" : "Assign Source Model",
                "##ImporterSourceRig",
                ref importerRigReference,
                ref _retargetRigSearch,
                "Auto (embedded hierarchy)",
                allowNone:
                    true))
        {
            _draft.AnimationSourceRigModel =
                importerRigReference;

            if (SameReference(
                    _retargetSourceReference,
                    new AssetReference(
                        asset.Guid,
                        asset.ProjectPath)))
            {
                _retargetSourceRigReference =
                    importerRigReference;
            }

            MarkDirty();
            InvalidateRetargetPreview();
        }

        if (ImGui.CollapsingHeader("Advanced target default"))
        {
            AssetReference importerTargetReference =
                _draft.DefaultRetargetTargetModel;

            if (DrawModelAssetPicker(
                    project,
                    "Default Retarget Target (Experimental)",
                    "##ImporterDefaultTarget",
                    ref importerTargetReference,
                    ref _retargetTargetSearch,
                    "None",
                    allowNone:
                        true))
            {
                _draft.DefaultRetargetTargetModel =
                    importerTargetReference;

                if (SameReference(
                        _retargetSourceReference,
                        new AssetReference(
                            asset.Guid,
                            asset.ProjectPath)))
                {
                    _retargetTargetReference =
                        importerTargetReference;
                }

                MarkDirty();
                InvalidateRetargetPreview();
            }

        }

        if (!sourceHasReadyEmbeddedRig)
            ImGui.TextDisabled("A clip without a usable embedded hierarchy needs a compatible Source Model; render mesh data is not required.");

        ImGui.SeparatorText(
            "AVAILABLE CLIPS");

        if (model.Animations.Count ==
            0)
        {
            ImGui.TextDisabled(
                _draft.ImportAnimations
                    ? "No embedded animation clips were found."
                    : "Animation import is disabled.");
        }
        else
        {
            foreach (ImportedAnimation clip
                     in model.Animations)
            {
                bool selected =
                    selectedAnimation !=
                        null &&
                    string.Equals(
                        clip.Key,
                        selectedAnimation.Key,
                        StringComparison.Ordinal);

                if (selected)
                {
                    EditorUi.StatusBadge(
                        "SELECTED",
                        EditorStatusKind.Success);
                    ImGui.SameLine();
                }

                int keyframes =
                    clip.Channels.Sum(
                        channel =>
                            (channel.Translation?.Keys.Count ??
                             0) +
                            (channel.Rotation?.Keys.Count ??
                             0) +
                            (channel.Scale?.Keys.Count ??
                             0));

                ImGui.TextUnformatted(
                    $"{clip.Name}   {clip.Duration:0.###} s   {clip.Channels.Count} channels   {keyframes} keys");
            }
        }

        if (selectedAnimation !=
                null &&
            SameReference(
                _retargetSourceReference,
                new AssetReference(
                    asset.Guid,
                    asset.ProjectPath)) &&
            !string.Equals(
                _lastSelectedAnimationKey,
                selectedAnimation.Key,
                StringComparison.Ordinal))
        {
            _lastSelectedAnimationKey =
                selectedAnimation.Key;

            _retargetClipKey =
                selectedAnimation.Key;

            _retargetOutputName =
                selectedAnimation.Name;

            InvalidateRetargetPreview();
        }

        if (ImGui.CollapsingHeader("Retarget to another character (Experimental)"))
            DrawUniversalRetargetSection(
                project,
                asset,
                model,
                selectedAnimation);
    }

    private void DrawUniversalRetargetSection(
        EditorProjectContext project,
        AssetRecord inspectedAsset,
        ModelAsset inspectedModel,
        ImportedAnimation? selectedAnimation)
    {
        ImGui.SeparatorText(
            "UNIVERSAL HUMANOID RETARGET (EXPERIMENTAL)");
        EditorUi.StatusBadge("EXPERIMENTAL", EditorStatusKind.Warning);

        ImGui.TextWrapped(
            "ByteEngine now treats the animation asset, source rig and target model as separate choices. This mirrors the useful part of Unity's Avatar/HumanPose workflow without requiring an export through Unity first.");

        AssetReference inspectedReference =
            new(
                inspectedAsset.Guid,
                inspectedAsset.ProjectPath);

        if (EditorUi.SecondaryButton(
                "Use This Asset As Source"))
        {
            _retargetSourceReference =
                inspectedReference;

            _retargetSourceRigReference =
                _draft.AnimationSourceRigModel;

            if (!_draft.DefaultRetargetTargetModel.IsEmpty)
            {
                _retargetTargetReference =
                    _draft.DefaultRetargetTargetModel;
            }

            ImportedAnimation? initial =
                selectedAnimation ??
                inspectedModel.Animations.FirstOrDefault();

            _retargetClipKey =
                initial?.Key ??
                string.Empty;

            _retargetOutputName =
                initial?.Name ??
                string.Empty;

            InvalidateRetargetPreview();
        }

        ImGui.SameLine();

        if (EditorUi.SecondaryButton(
                "Use This Asset As Target"))
        {
            _retargetTargetReference =
                inspectedReference;

            if (SameReference(
                    _retargetSourceReference,
                    inspectedReference))
            {
                _retargetSourceReference =
                    AssetReference.Empty;

                _retargetSourceRigReference =
                    AssetReference.Empty;

                _retargetClipKey =
                    string.Empty;

                _retargetOutputName =
                    string.Empty;
            }

            InvalidateRetargetPreview();
        }

        ImGui.Spacing();

        AssetReference sourceReference =
            _retargetSourceReference;

        if (DrawModelAssetPicker(
                project,
                "Animation Source",
                "##UniversalAnimationSource",
                ref sourceReference,
                ref _retargetSourceSearch,
                "Choose animation asset...",
                allowNone:
                    false))
        {
            _retargetSourceReference =
                sourceReference;

            InitializeRetargetSource(
                project,
                sourceReference);
        }

        ModelAsset? sourceModel =
            TryLoadModel(
                project,
                _retargetSourceReference,
                out string? sourceError);

        if (sourceModel ==
                null)
        {
            if (!string.IsNullOrWhiteSpace(
                    sourceError))
            {
                EditorUi.StatusBadge(
                    "SOURCE ERROR",
                    EditorStatusKind.Error);
                ImGui.SameLine();
                ImGui.TextWrapped(
                    sourceError);
            }

            return;
        }

        if (sourceModel.Animations.Count ==
            0)
        {
            EditorUi.StatusBadge(
                "NO ANIMATION CLIPS",
                EditorStatusKind.Warning);
            ImGui.SameLine();
            ImGui.TextWrapped(
                "Choose another Animation Source. The source may be meshless, but it must contain animation tracks.");
        }

        AssetReference sourceRigReference =
            _retargetSourceRigReference;

        if (DrawModelAssetPicker(
                project,
                "Source Rig",
                "##UniversalSourceRig",
                ref sourceRigReference,
                ref _retargetRigSearch,
                "Auto / source asset",
                allowNone:
                    true))
        {
            _retargetSourceRigReference =
                sourceRigReference;

            if (SameReference(
                    _retargetSourceReference,
                    inspectedReference))
            {
                _draft.AnimationSourceRigModel =
                    sourceRigReference;

                MarkDirty();
            }

            InvalidateRetargetPreview();
        }

        string resolvedRigLabel =
            ResolveRigLabel(
                project,
                sourceModel,
                _retargetSourceRigReference);

        EditorUi.LabelValue(
            "Rig Provider",
            resolvedRigLabel);

        AssetReference targetReference =
            _retargetTargetReference;

        if (DrawModelAssetPicker(
                project,
                "Target Model",
                "##UniversalTargetModel",
                ref targetReference,
                ref _retargetTargetSearch,
                "Choose target model...",
                allowNone:
                    false))
        {
            _retargetTargetReference =
                targetReference;

            if (SameReference(
                    _retargetSourceReference,
                    inspectedReference))
            {
                _draft.DefaultRetargetTargetModel =
                    targetReference;

                MarkDirty();
            }

            InvalidateRetargetPreview();
        }

        ImportedAnimation? sourceClip =
            ResolveRetargetClip(
                sourceModel);

        string clipPreview =
            sourceClip?.Name ??
            "Choose clip...";

        ImGui.BeginDisabled(
            sourceModel.Animations.Count ==
            0);

        if (ImGui.BeginCombo(
                "Source Clip",
                clipPreview))
        {
            foreach (ImportedAnimation clip
                     in sourceModel.Animations)
            {
                bool selected =
                    string.Equals(
                        clip.Key,
                        _retargetClipKey,
                        StringComparison.Ordinal);

                if (ImGui.Selectable(
                        clip.Name,
                        selected))
                {
                    _retargetClipKey =
                        clip.Key;

                    _retargetOutputName =
                        clip.Name;

                    InvalidateRetargetPreview();
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        ImGui.EndDisabled();

        if (sourceClip !=
            null)
        {
            EditorUi.LabelValue(
                "Source Duration",
                $"{sourceClip.Duration:0.###} s");

            EditorUi.LabelValue(
                "Source Channels",
                sourceClip.Channels.Count.ToString());
        }

        string outputName =
            _retargetOutputName;

        if (ImGui.InputText(
                "Output Name",
                ref outputName,
                128))
        {
            _retargetOutputName =
                outputName;

            _replaceExistingRetarget =
                false;

            InvalidateRetargetPreview();
        }

        bool canBuild =
            sourceClip !=
                null &&
            !_retargetTargetReference.IsEmpty &&
            !string.IsNullOrWhiteSpace(
                _retargetOutputName);

        ImGui.BeginDisabled(
            !canBuild);

        if (EditorUi.SecondaryButton(
                "Build Retarget (Experimental)",
                size:
                    new Vector2(
                        -1.0f,
                        34.0f)))
        {
            BuildRetarget(
                project,
                sourceModel,
                sourceClip!);
        }

        ImGui.EndDisabled();

        if (_retargetPreview !=
            null)
        {
            EditorUi.StatusBadge(
                "RETARGET READY (EXPERIMENTAL)",
                EditorStatusKind.Success);
            ImGui.SameLine();
            ImGui.TextWrapped(
                $"{_retargetPreview.Duration:0.###} s, {_retargetPreview.Channels.Count} target channels. Nothing has been written yet.");
        }

        if (!string.IsNullOrWhiteSpace(
                _retargetStatus))
        {
            EditorUi.StatusBadge(
                _retargetStatusIsError
                    ? "ERROR"
                    : "OK",
                _retargetStatusIsError
                    ? EditorStatusKind.Error
                    : EditorStatusKind.Success);
            ImGui.SameLine();
            ImGui.TextWrapped(
                _retargetStatus);
        }

        if (_retargetPreview ==
                null)
        {
            return;
        }

        ModelAsset? targetModel =
            TryLoadModel(
                project,
                _retargetTargetReference,
                out string? targetError);

        if (targetModel ==
            null)
        {
            EditorUi.StatusBadge(
                "TARGET ERROR",
                EditorStatusKind.Error);
            ImGui.SameLine();
            ImGui.TextWrapped(
                targetError ??
                "Target model could not be loaded.");
            return;
        }

        ModelOwnedAnimationConflictKind conflict =
            ModelOwnedAnimationStore.GetConflict(
                project.Assets.ProjectRoot,
                targetModel,
                _retargetOutputName);

        bool importedConflict =
            conflict ==
            ModelOwnedAnimationConflictKind.ImportedAnimation;

        bool bakedConflict =
            conflict ==
            ModelOwnedAnimationConflictKind.BakedAnimation;

        if (importedConflict)
        {
            EditorUi.StatusBadge(
                "NAME CONFLICT",
                EditorStatusKind.Warning);
            ImGui.SameLine();
            ImGui.TextWrapped(
                "That output name belongs to a native clip inside the target model. Native imported animations are never overwritten.");
        }
        else if (bakedConflict)
        {
            bool replace =
                _replaceExistingRetarget;

            if (ImGui.Checkbox(
                    "Replace existing experimental baked animation",
                    ref replace))
            {
                _replaceExistingRetarget =
                    replace;
            }
        }

        bool canBake =
            !importedConflict &&
            (!bakedConflict ||
             _replaceExistingRetarget);

        ImGui.BeginDisabled(
            !canBake);

        if (EditorUi.PrimaryButton(
                "Bake To Target Model (Experimental)",
                size:
                    new Vector2(
                        -1.0f,
                        38.0f)))
        {
            BakeRetarget(project, sourceModel, sourceClip!, targetModel);
        }

        ImGui.EndDisabled();
    }

    private void InitializeRetargetSource(
        EditorProjectContext project,
        AssetReference sourceReference)
    {
        _retargetSourceRigReference =
            AssetReference.Empty;

        _retargetClipKey =
            string.Empty;

        _retargetOutputName =
            string.Empty;

        ModelAsset? sourceModel =
            TryLoadModel(
                project,
                sourceReference,
                out _);

        if (sourceModel !=
            null)
        {
            _retargetSourceRigReference =
                sourceModel.AnimationSourceRigModel;

            if (_retargetTargetReference.IsEmpty &&
                !sourceModel.DefaultRetargetTargetModel.IsEmpty)
            {
                _retargetTargetReference =
                    sourceModel.DefaultRetargetTargetModel;
            }

            ImportedAnimation? initial =
                sourceModel.Animations.FirstOrDefault();

            _retargetClipKey =
                initial?.Key ??
                string.Empty;

            _retargetOutputName =
                initial?.Name ??
                string.Empty;
        }

        InvalidateRetargetPreview();
    }

    private void BuildRetarget(
        EditorProjectContext project,
        ModelAsset sourceModel,
        ImportedAnimation sourceClip)
    {
        try
        {
            bool currentImporterIsSource =
                _retargetSourceReference.Guid ==
                _assetGuid;

            float samplesPerSecond =
                currentImporterIsSource
                    ? _draft.RetargetSamplesPerSecond
                    : sourceModel.RetargetSamplesPerSecond;

            AnimationRootMotionSource? rootMotionOverride =
                currentImporterIsSource
                    ? _draft.RootMotionSource
                    : null;

            _retargetPreview =
                HumanoidRetargetRuntime.BuildClip(
                    project.Assets,
                    _retargetSourceReference,
                    _retargetSourceRigReference,
                    sourceClip.Name,
                    _retargetTargetReference,
                    _retargetOutputName,
                    samplesPerSecond,
                    rootMotionOverride);

            SetRetargetStatus(
                "Experimental retarget built successfully. Review the target/channel summary, then bake when ready.",
                false);
        }
        catch (Exception exception)
        {
            _retargetPreview =
                null;

            SetRetargetStatus(
                exception.Message,
                true);
        }
    }

    private void BakeRetarget(
        EditorProjectContext project,
        ModelAsset sourceModel,
        ImportedAnimation sourceClip,
        ModelAsset targetModel)
    {
        if (_retargetPreview ==
            null)
        {
            return;
        }

        try
        {
            AssetRecord? sourceAsset =
                project.AssetDatabase.Resolve(
                    _retargetSourceReference);

            ModelOwnedAnimationBakeResult result =
                ModelOwnedAnimationStore.Bake(
                    project.Assets.ProjectRoot,
                    targetModel,
                    _retargetPreview,
                    _retargetOutputName,
                    sourceModel.Guid,
                    sourceAsset?.ProjectPath,
                    sourceClip.Key,
                    sourceClip.Name,
                    _replaceExistingRetarget);

            _replaceExistingRetarget =
                false;

            SetRetargetStatus(
                result.Replaced
                    ? $"Replaced '{result.Animation.Name}' on target '{targetModel.Name}'."
                    : $"Baked '{result.Animation.Name}' onto target '{targetModel.Name}'. It now behaves like a target-owned animation clip.",
                false);
        }
        catch (Exception exception)
        {
            SetRetargetStatus(
                exception.Message,
                true);
        }
    }

    private ImportedAnimation? ResolveRetargetClip(
        ModelAsset sourceModel)
    {
        ImportedAnimation? selected =
            sourceModel.Animations.FirstOrDefault(
                animation =>
                    string.Equals(
                        animation.Key,
                        _retargetClipKey,
                        StringComparison.Ordinal));

        if (selected !=
            null)
        {
            return selected;
        }

        selected =
            sourceModel.Animations.FirstOrDefault();

        if (selected !=
            null)
        {
            _retargetClipKey =
                selected.Key;

            if (string.IsNullOrWhiteSpace(
                    _retargetOutputName))
            {
                _retargetOutputName =
                    selected.Name;
            }
        }

        return selected;
    }

    private static ModelAsset? TryLoadModel(
        EditorProjectContext project,
        AssetReference reference,
        out string? error)
    {
        error =
            null;

        if (reference.IsEmpty)
        {
            return null;
        }

        try
        {
            return project.Assets.LoadModel(
                reference);
        }
        catch (Exception exception)
        {
            error =
                exception.Message;

            return null;
        }
    }

    private static bool IsHumanoidReady(
        ModelAsset model)
    {
        if (model.RigType !=
                AnimationRigType.Humanoid ||
            model.Skeleton ==
                null ||
            model.ReferenceHumanoidPose?.IsReady !=
                true)
        {
            return false;
        }

        HumanoidRigValidationResult validation =
            HumanoidRigMapper.Validate(
                model.Skeleton,
                model.HumanoidMapping);

        if (!validation.IsReady)
        {
            return false;
        }

        HumanoidRigDiagnosticReport diagnostics =
            HumanoidRigDiagnostics.Analyze(
                model.Skeleton,
                model.HumanoidMapping);

        return diagnostics.Errors.Count ==
            0;
    }

    private static string ResolveRigLabel(
        EditorProjectContext project,
        ModelAsset sourceModel,
        AssetReference explicitReference)
    {
        AssetReference effective =
            !explicitReference.IsEmpty
                ? explicitReference
                : sourceModel.AnimationSourceRigModel;

        if (effective.IsEmpty)
        {
            return IsHumanoidReady(
                    sourceModel)
                ? $"Embedded: {sourceModel.Name}"
                : "Embedded source rig is not Humanoid-ready - assign a Source Rig model.";
        }

        AssetRecord? record =
            project.AssetDatabase.Resolve(
                effective);

        return record?.ProjectPath ??
               effective.ToString();
    }

    private bool DrawModelAssetPicker(
        EditorProjectContext project,
        string label,
        string id,
        ref AssetReference reference,
        ref string search,
        string noneLabel,
        bool allowNone)
    {
        AssetRecord? current =
            reference.IsEmpty
                ? null
                : project.AssetDatabase.Resolve(
                    reference);

        string preview =
            current?.ProjectPath ??
            noneLabel;

        bool changed =
            false;

        if (!ImGui.BeginCombo(
                $"{label}{id}",
                preview))
        {
            return false;
        }

        if (ImGui.IsWindowAppearing())
        {
            search =
                string.Empty;

            ImGui.SetKeyboardFocusHere();
        }

        ImGui.InputTextWithHint(
            $"##Search{id}",
            "Search model assets...",
            ref search,
            128);

        if (allowNone &&
            ImGui.Selectable(
                noneLabel,
                reference.IsEmpty))
        {
            reference =
                AssetReference.Empty;

            changed =
                true;
        }

        foreach (AssetRecord candidate
                 in project.AssetDatabase.Assets
                     .Where(
                         candidate =>
                             candidate.Type ==
                                 AssetType.Model3D)
                     .OrderBy(
                         candidate =>
                             candidate.ProjectPath,
                         StringComparer.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(
                    search) &&
                !candidate.ProjectPath.Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool selected =
                candidate.Guid ==
                reference.Guid;

            if (ImGui.Selectable(
                    candidate.ProjectPath,
                    selected))
            {
                reference =
                    new AssetReference(
                        candidate.Guid,
                        candidate.ProjectPath);

                changed =
                    true;
            }

            if (selected)
            {
                ImGui.SetItemDefaultFocus();
            }
        }

        ImGui.EndCombo();

        return changed;
    }

    private static bool SameReference(
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

    private void InvalidateRetargetPreview()
    {
        _retargetPreview =
            null;

        _retargetStatus =
            string.Empty;

        _retargetStatusIsError =
            false;

        _replaceExistingRetarget =
            false;
    }

    private void SetRetargetStatus(
        string message,
        bool isError)
    {
        _retargetStatus =
            message;

        _retargetStatusIsError =
            isError;
    }

    private void ApplyAutomaticMapping(
        ModelAsset model)
    {
        _draft.RigType =
            AnimationRigType.Generic;

        if (model.Skeleton ==
            null)
        {
            return;
        }

        HumanoidBoneMap detected =
            HumanoidRigMapper.AutoMap(
                model.Skeleton);

        HumanoidRigValidationResult validation =
            HumanoidRigMapper.Validate(
                model.Skeleton,
                detected);

        HumanoidRigDiagnosticReport diagnostics =
            HumanoidRigDiagnostics.Analyze(
                model.Skeleton,
                detected);

        if (!validation.IsReady ||
            diagnostics.Errors.Count >
                0)
        {
            return;
        }

        _draft.RigType =
            AnimationRigType.Humanoid;

        _draft.HumanoidMapping =
            detected;
    }

    private bool Apply(
        EditorProjectContext project,
        AssetRecord asset)
    {
        _draft.Normalize();

        ModelImporterSettings previous =
            asset.Metadata.ModelImporter.Clone();

        asset.Metadata.ModelImporter =
            _draft.Clone();

        if (!ByteEngine.Editor.HumanoidRigAuthoring.Persist(
                asset,
                out string? saveError))
        {
            asset.Metadata.ModelImporter =
                previous;

            SetStatus(
                saveError ??
                "Could not save importer metadata.",
                true);

            return false;
        }

        try
        {
            ModelAsset refreshed =
                project.Assets.ReimportModel(
                    asset.Guid);

            _draft =
                asset.Metadata.ModelImporter.Clone();

            if (_draft.AutoDetectHumanoidRig &&
                refreshed.RigType ==
                    AnimationRigType.Humanoid)
            {
                _draft.RigType =
                    AnimationRigType.Humanoid;

                _draft.HumanoidMapping =
                    refreshed.HumanoidMapping.Clone();
            }

            _dirty =
                false;

            if (_retargetSourceReference.Guid ==
                asset.Guid)
            {
                _retargetSourceRigReference =
                    _draft.AnimationSourceRigModel;

                _retargetTargetReference =
                    _draft.DefaultRetargetTargetModel;
            }

            SetStatus(
                $"Reimported '{asset.ProjectPath}' with the new importer settings.",
                false);

            return true;
        }
        catch (Exception exception)
        {
            SetStatus(
                exception.Message,
                true);

            return false;
        }
    }

    private void Revert(
        AssetRecord asset,
        ModelAsset model)
    {
        _draft =
            asset.Metadata.ModelImporter.Clone();

        if (_draft.AutoDetectHumanoidRig &&
            model.RigType ==
                AnimationRigType.Humanoid)
        {
            _draft.RigType =
                AnimationRigType.Humanoid;

            _draft.HumanoidMapping =
                model.HumanoidMapping.Clone();
        }

        _dirty =
            false;

        if (_retargetSourceReference.Guid ==
            asset.Guid)
        {
            _retargetSourceRigReference =
                _draft.AnimationSourceRigModel;

            _retargetTargetReference =
                _draft.DefaultRetargetTargetModel;

            InvalidateRetargetPreview();
        }

        _status =
            string.Empty;

        _statusIsError =
            false;
    }

    private void MarkDirty()
    {
        _dirty =
            true;

        _status =
            string.Empty;

        _statusIsError =
            false;
    }

    private void SetStatus(
        string message,
        bool isError)
    {
        _status =
            message;

        _statusIsError =
            isError;
    }
}
