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

    private string _sourceRigSearch = string.Empty;

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
                "Import Settings..."))
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

        _sourceRigSearch = string.Empty;
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

        ImGui.TextDisabled("FBX units and axes are converted automatically; glTF uses metres. Import Scale is an advanced override for incorrectly authored files.");
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
                "Humanoid mapping requires a skeleton hierarchy.");
            return;
        }

        if (_draft.RigType !=
            AnimationRigType.Humanoid)
        {
            ImGui.Spacing();
            ImGui.TextDisabled(
                "Generic preserves the source skeleton exactly as authored.");
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
                "Assign the model this animation was authored for. Native preview requires matching bone names.");
        }

        AssetReference importerRigReference =
            _draft.AnimationSourceRigModel;

        if (DrawModelAssetPicker(
                project,
                sourceHasReadyEmbeddedRig ? "Source Model (optional)" : "Assign Source Model",
                "##ImporterSourceRig",
                ref importerRigReference,
                ref _sourceRigSearch,
                "Auto (embedded hierarchy)",
                allowNone:
                    true))
        {
            _draft.AnimationSourceRigModel =
                importerRigReference;

            MarkDirty();
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
            ModelAsset refreshed = project.Assets.ReimportModel(asset.Guid);
            if (EditorState.Active is { } state)
            {
                foreach (ModelHierarchyInstance instance in state.EditorScene.GameObjects
                             .SelectMany(item => item.Components.OfType<ModelHierarchyInstance>())
                             .Where(item => item.Model.Guid == asset.Guid))
                    if (EditorSceneCommands.RefreshImportSpace(instance.GameObject, refreshed))
                        state.MarkDirty();
            }

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
