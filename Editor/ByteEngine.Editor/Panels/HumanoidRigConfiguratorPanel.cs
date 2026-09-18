using System.Numerics;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;

using ImGuiNET;

namespace ByteEngine.Editor.Panels;

/// <summary>
/// Unity-inspired Humanoid rig configuration window.
///
/// The preview intentionally draws a lightweight bind-pose wireframe and
/// skeleton using ImGui instead of creating another full-speed 3D viewport.
/// Bone labels appear only when a joint is hovered, keeping the character
/// readable even on skeletons with many fingers/twist bones.
/// </summary>
internal sealed class HumanoidRigConfiguratorPanel
{
    private const int MaximumPreviewTriangles =
        900;

    private AssetRecord? _asset;
    private EditorProjectContext? _project;
    private ModelAsset? _model;

    private bool _visible;
    private bool _focusNextDraw;
    private bool _dirty;

    private AnimationRigType _rigType =
        AnimationRigType.Generic;

    private HumanoidBoneMap _mapping =
        new();

    private HumanoidBone _selectedSemantic =
        HumanoidBone.Hips;

    private string _semanticSearch =
        string.Empty;

    private string _boneSearch =
        string.Empty;

    private string? _error;

    private readonly List<PreviewLine> _meshLines =
        new();

    private Vector3[] _bonePositions =
        Array.Empty<Vector3>();

    private Vector3 _previewCenter =
        Vector3.Zero;

    private float _previewRadius =
        1.0f;

    private float _yaw =
        -90.0f;

    private float _pitch =
        -5.0f;

    private float _distance =
        3.0f;

    private bool _showMesh =
        true;

    private bool _showSkeleton =
        true;

    private bool _mappedBonesOnly;

    private bool _autoAdvanceRequired =
        true;

    public void Open(
        AssetRecord asset,
        EditorProjectContext project,
        EditorLog log)
    {
        if (asset.Type !=
            AssetType.Model3D)
        {
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

        _error =
            null;

        try
        {
            _model =
                project.Assets.LoadModel(
                    new AssetReference(
                        asset.Guid,
                        asset.ProjectPath));

            asset.Metadata.ModelImporter.Normalize();

            _rigType =
                asset.Metadata.ModelImporter.RigType;

            _mapping =
                asset.Metadata.ModelImporter.HumanoidMapping.Clone();

            BuildPreviewGeometry();
        }
        catch (Exception exception)
        {
            _model =
                null;

            _error =
                exception.Message;

            log.Error(
                $"Could not open Humanoid configurator for '{asset.ProjectPath}': {exception.Message}");
        }
    }

    public void Draw(
        EditorLog log)
    {
        if (!_visible ||
            _asset == null ||
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

        ImGui.SetNextWindowSize(
            new Vector2(
                1100.0f,
                720.0f),
            ImGuiCond.FirstUseEver);

        bool visible =
            ImGui.Begin(
                $"Humanoid Rig: {name}{(_dirty ? " *" : string.Empty)}###HumanoidRigConfigurator",
                ref open,
                ImGuiWindowFlags.MenuBar);

        if (visible)
        {
            DrawMenu(
                log);

            if (_model ==
                null)
            {
                ImGui.TextColored(
                    new Vector4(
                        1.0f,
                        0.35f,
                        0.30f,
                        1.0f),
                    "Rig preview unavailable.");

                if (!string.IsNullOrWhiteSpace(
                        _error))
                {
                    ImGui.TextWrapped(
                        _error);
                }
            }
            else
            {
                DrawContent(
                    log);
            }
        }

        ImGui.End();

        if (!open)
        {
            _visible =
                false;
        }
    }

    private void DrawMenu(
        EditorLog log)
    {
        if (!ImGui.BeginMenuBar())
        {
            return;
        }

        ImGui.BeginDisabled(
            !_dirty);

        if (ImGui.MenuItem(
                "Apply",
                "Ctrl+S"))
        {
            Apply(
                log);
        }

        ImGui.EndDisabled();

        if (ImGui.MenuItem(
                "Revert"))
        {
            Revert(
                log);
        }

        ImGui.Separator();

        if (ImGui.MenuItem(
                "Close"))
        {
            _visible =
                false;
        }

        ImGui.EndMenuBar();
    }

    private void DrawContent(
        EditorLog log)
    {
        if (_asset == null ||
            _project == null ||
            _model == null)
        {
            return;
        }

        ImGui.TextColored(
            EditorTheme.AccentHover,
            "HUMANOID RIG CONFIGURATION");

        ImGui.SameLine();

        ImGui.TextDisabled(
            _dirty
                ? "Unsaved mapping changes"
                : "Applied");

        ImGui.TextDisabled(
            _asset.ProjectPath);

        ImGui.Spacing();

        ImGui.BeginDisabled(
            !_dirty);

        if (ImGui.Button(
                "Apply"))
        {
            Apply(
                log);
        }

        ImGui.EndDisabled();

        ImGui.SameLine();

        if (ImGui.Button(
                "Revert"))
        {
            Revert(
                log);
        }

        ImGui.SameLine();

        if (ImGui.Button(
                "Auto Map"))
        {
            if (_model.Skeleton !=
                null)
            {
                _mapping =
                    HumanoidRigMapper.AutoMap(
                        _model.Skeleton);

                _rigType =
                    AnimationRigType.Humanoid;

                SelectFirstMissingRequired();

                _dirty =
                    true;
            }
        }

        ImGui.SameLine();

        if (ImGui.Button(
                "Clear Mapping"))
        {
            _mapping =
                new HumanoidBoneMap();

            _dirty =
                true;
        }

        ImGui.SameLine();

        if (ImGui.Button(
                "Frame"))
        {
            FramePreview();
        }

        if (ImGui.IsWindowFocused(
                ImGuiFocusedFlags.RootAndChildWindows) &&
            ImGui.GetIO().KeyCtrl &&
            ImGui.IsKeyPressed(
                ImGuiKey.S))
        {
            Apply(
                log);
        }

        int rigType =
            (int)_rigType;

        string[] rigNames =
            Enum.GetNames<AnimationRigType>();

        ImGui.SetNextItemWidth(
            180.0f);

        if (ImGui.Combo(
                "Rig Type",
                ref rigType,
                rigNames,
                rigNames.Length))
        {
            _rigType =
                (AnimationRigType)rigType;

            if (_rigType ==
                    AnimationRigType.Humanoid &&
                _mapping.MappedCount ==
                    0 &&
                _model.Skeleton !=
                    null)
            {
                _mapping =
                    HumanoidRigMapper.AutoMap(
                        _model.Skeleton);

                SelectFirstMissingRequired();
            }

            _dirty =
                true;
        }

        ImGui.Separator();

        float leftWidth =
            Math.Clamp(
                ImGui.GetContentRegionAvail().X *
                0.34f,
                315.0f,
                410.0f);

        ImGui.BeginChild(
            "##HumanoidMappingPanel",
            new Vector2(
                leftWidth,
                0.0f),
            ImGuiChildFlags.Borders);

        DrawMappingPanel();

        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild(
            "##HumanoidPreviewPanel",
            Vector2.Zero,
            ImGuiChildFlags.Borders);

        DrawPreview();

        ImGui.EndChild();
    }

    private void DrawMappingPanel()
    {
        if (_model?.Skeleton ==
            null)
        {
            ImGui.TextWrapped(
                "This model has no skeleton.");

            return;
        }

        HumanoidRigValidationResult validation =
            HumanoidRigMapper.Validate(
                _model.Skeleton,
                _mapping);

        HumanoidRigDiagnosticReport diagnostics =
            HumanoidRigDiagnostics.Analyze(
                _model.Skeleton,
                _mapping);

        bool authoringReady =
            validation.IsReady &&
            diagnostics.Errors.Count ==
                0;

        if (authoringReady)
        {
            ImGui.TextColored(
                new Vector4(
                    0.35f,
                    0.86f,
                    0.48f,
                    1.0f),
                "Humanoid Ready");
        }
        else
        {
            ImGui.TextColored(
                new Vector4(
                    1.0f,
                    0.58f,
                    0.24f,
                    1.0f),
                "Humanoid Needs Attention");
        }

        ImGui.TextDisabled(
            $"{validation.RequiredMappedCount}/{HumanoidBoneCatalog.Required.Count} required | {validation.MappedBoneCount} total mapped");

        if (diagnostics.Errors.Count >
            0)
        {
            foreach (string error
                     in diagnostics.Errors)
            {
                ImGui.TextColored(
                    new Vector4(
                        1.0f,
                        0.38f,
                        0.30f,
                        1.0f),
                    error);
            }
        }

        if (diagnostics.Warnings.Count >
            0)
        {
            foreach (string warning
                     in diagnostics.Warnings)
            {
                ImGui.TextColored(
                    new Vector4(
                        1.0f,
                        0.67f,
                        0.25f,
                        1.0f),
                    warning);
            }
        }
        else if (validation.IsReady)
        {
            ImGui.TextDisabled(
                diagnostics.ApproximatelyTPose
                    ? "Reference pose looks suitable for Humanoid retargeting."
                    : "Reference pose could not be fully classified.");
        }

        ImGui.SeparatorText(
            "BODY MAP");

        ImGui.TextWrapped(
            "1. Click a body joint below.  2. Click the matching joint on the source skeleton.");

        ImGui.TextDisabled(
            "Green = mapped   Red = required missing   Gray = optional   Orange = selected");

        DrawHumanoidBodyMap();

        ImGui.SeparatorText(
            "CURRENT MAPPING");

        string selectedName =
            HumanoidRigAuthoring.DisplayName(
                _selectedSemantic);

        string? current =
            _mapping.GetBoneName(
                _selectedSemantic);

        if (current ==
            null)
        {
            ImGui.TextColored(
                HumanoidBoneCatalog.IsRequired(
                    _selectedSemantic)
                    ? new Vector4(
                        1.0f,
                        0.55f,
                        0.30f,
                        1.0f)
                    : new Vector4(
                        0.78f,
                        0.82f,
                        0.88f,
                        1.0f),
                $"{selectedName}  ->  Not mapped");
        }
        else
        {
            ImGui.TextColored(
                new Vector4(
                    0.35f,
                    0.86f,
                    0.48f,
                    1.0f),
                $"{selectedName}  ->  {current}");
        }

        DrawSearchableSourceBonePicker();

        if (current !=
                null &&
            ImGui.Button(
                "Clear Selected"))
        {
            _mapping.ClearBone(
                _selectedSemantic);

            _dirty =
                true;
        }

        ImGui.SameLine();

        ImGui.Checkbox(
            "Auto-advance required bones",
            ref _autoAdvanceRequired);

        if (ImGui.TreeNode(
                "Advanced Bone List"))
        {
            ImGui.TextDisabled(
                "Use this only when you want precise slot-by-slot editing.");

            ImGui.InputTextWithHint(
                "##SemanticSearch",
                "Search humanoid slots...",
                ref _semanticSearch,
                96);

            DrawSemanticSection(
                "Required",
                HumanoidBoneCatalog.Required);

            HumanoidBone[] optional =
                Enum.GetValues<HumanoidBone>()
                    .Where(
                        bone =>
                            !HumanoidBoneCatalog.IsRequired(
                                bone))
                    .ToArray();

            DrawSemanticSection(
                "Optional",
                optional);

            ImGui.TreePop();
        }
    }

    private void DrawHumanoidBodyMap()
    {
        float width =
            Math.Max(
                ImGui.GetContentRegionAvail().X,
                220.0f);

        float height =
            Math.Clamp(
                width *
                0.95f,
                220.0f,
                310.0f);

        Vector2 origin =
            ImGui.GetCursorScreenPos();

        Vector2 size =
            new(
                width,
                height);

        ImGui.InvisibleButton(
            "##HumanoidBodyMap",
            size);

        bool hovered =
            ImGui.IsItemHovered();

        Vector2 mouse =
            ImGui.GetIO().MousePos;

        ImDrawListPtr drawList =
            ImGui.GetWindowDrawList();

        uint background =
            ImGui.GetColorU32(
                new Vector4(
                    0.065f,
                    0.075f,
                    0.09f,
                    1.0f));

        drawList.AddRectFilled(
            origin,
            origin +
                size,
            background,
            5.0f);

        BodyMapPoint[] points =
            BodyMapPoints();

        Vector2 P(
            HumanoidBone bone)
        {
            BodyMapPoint point =
                points.First(
                    candidate =>
                        candidate.Bone ==
                            bone);

            return
                origin +
                new Vector2(
                    point.X *
                    size.X,
                    point.Y *
                    size.Y);
        }

        uint bodyLine =
            ImGui.GetColorU32(
                new Vector4(
                    0.34f,
                    0.42f,
                    0.52f,
                    0.85f));

        void Link(
            HumanoidBone a,
            HumanoidBone b)
        {
            drawList.AddLine(
                P(a),
                P(b),
                bodyLine,
                2.0f);
        }

        Link(HumanoidBone.Head, HumanoidBone.Neck);
        Link(HumanoidBone.Neck, HumanoidBone.UpperChest);
        Link(HumanoidBone.UpperChest, HumanoidBone.Chest);
        Link(HumanoidBone.Chest, HumanoidBone.Spine);
        Link(HumanoidBone.Spine, HumanoidBone.Hips);

        Link(HumanoidBone.UpperChest, HumanoidBone.LeftShoulder);
        Link(HumanoidBone.LeftShoulder, HumanoidBone.LeftUpperArm);
        Link(HumanoidBone.LeftUpperArm, HumanoidBone.LeftLowerArm);
        Link(HumanoidBone.LeftLowerArm, HumanoidBone.LeftHand);

        Link(HumanoidBone.UpperChest, HumanoidBone.RightShoulder);
        Link(HumanoidBone.RightShoulder, HumanoidBone.RightUpperArm);
        Link(HumanoidBone.RightUpperArm, HumanoidBone.RightLowerArm);
        Link(HumanoidBone.RightLowerArm, HumanoidBone.RightHand);

        Link(HumanoidBone.Hips, HumanoidBone.LeftUpperLeg);
        Link(HumanoidBone.LeftUpperLeg, HumanoidBone.LeftLowerLeg);
        Link(HumanoidBone.LeftLowerLeg, HumanoidBone.LeftFoot);
        Link(HumanoidBone.LeftFoot, HumanoidBone.LeftToes);

        Link(HumanoidBone.Hips, HumanoidBone.RightUpperLeg);
        Link(HumanoidBone.RightUpperLeg, HumanoidBone.RightLowerLeg);
        Link(HumanoidBone.RightLowerLeg, HumanoidBone.RightFoot);
        Link(HumanoidBone.RightFoot, HumanoidBone.RightToes);

        int hoveredIndex =
            -1;

        float bestDistance =
            15.0f;

        for (int index = 0;
             index <
                points.Length;
             index++)
        {
            BodyMapPoint point =
                points[index];

            Vector2 screen =
                origin +
                new Vector2(
                    point.X *
                    size.X,
                    point.Y *
                    size.Y);

            float distance =
                hovered
                    ? Vector2.Distance(
                        mouse,
                        screen)
                    : float.PositiveInfinity;

            if (distance <
                bestDistance)
            {
                bestDistance =
                    distance;

                hoveredIndex =
                    index;
            }
        }

        for (int index = 0;
             index <
                points.Length;
             index++)
        {
            BodyMapPoint point =
                points[index];

            Vector2 screen =
                origin +
                new Vector2(
                    point.X *
                    size.X,
                    point.Y *
                    size.Y);

            bool mapped =
                _mapping.IsMapped(
                    point.Bone);

            bool required =
                HumanoidBoneCatalog.IsRequired(
                    point.Bone);

            bool selected =
                point.Bone ==
                    _selectedSemantic;

            bool pointHovered =
                index ==
                    hoveredIndex;

            uint color =
                ImGui.GetColorU32(
                    pointHovered
                        ? new Vector4(
                            1.0f,
                            0.94f,
                            0.18f,
                            1.0f)
                        : selected
                            ? new Vector4(
                                1.0f,
                                0.55f,
                                0.08f,
                                1.0f)
                            : mapped
                                ? new Vector4(
                                    0.20f,
                                    1.0f,
                                    0.38f,
                                    1.0f)
                                : required
                                    ? new Vector4(
                                        1.0f,
                                        0.32f,
                                        0.25f,
                                        1.0f)
                                    : new Vector4(
                                        0.70f,
                                        0.74f,
                                        0.80f,
                                        1.0f));

            uint outline =
                ImGui.GetColorU32(
                    new Vector4(
                        0.0f,
                        0.0f,
                        0.0f,
                        0.9f));

            float radius =
                selected ||
                pointHovered
                    ? 7.0f
                    : 5.5f;

            drawList.AddCircleFilled(
                screen,
                radius +
                    2.0f,
                outline,
                16);

            drawList.AddCircleFilled(
                screen,
                radius,
                color,
                16);
        }

        if (hoveredIndex >=
                0 &&
            hoveredIndex <
                points.Length)
        {
            HumanoidBone semantic =
                points[hoveredIndex]
                    .Bone;

            string display =
                HumanoidRigAuthoring.DisplayName(
                    semantic);

            string? mapped =
                _mapping.GetBoneName(
                    semantic);

            ImGui.BeginTooltip();

            ImGui.TextUnformatted(
                display);

            ImGui.TextDisabled(
                mapped ==
                    null
                    ? HumanoidBoneCatalog.IsRequired(
                        semantic)
                        ? "Required - not mapped"
                        : "Optional - not mapped"
                    : $"Source bone: {mapped}");

            ImGui.TextDisabled(
                "Click to select this body part.");

            ImGui.EndTooltip();

            if (ImGui.IsMouseClicked(
                    ImGuiMouseButton.Left))
            {
                _selectedSemantic =
                    semantic;
            }
        }
    }

    private static BodyMapPoint[] BodyMapPoints() =>
        new[]
        {
            new BodyMapPoint(HumanoidBone.Head, 0.50f, 0.08f),
            new BodyMapPoint(HumanoidBone.Neck, 0.50f, 0.16f),
            new BodyMapPoint(HumanoidBone.UpperChest, 0.50f, 0.23f),
            new BodyMapPoint(HumanoidBone.Chest, 0.50f, 0.30f),
            new BodyMapPoint(HumanoidBone.Spine, 0.50f, 0.38f),
            new BodyMapPoint(HumanoidBone.Hips, 0.50f, 0.49f),

            new BodyMapPoint(HumanoidBone.LeftShoulder, 0.39f, 0.23f),
            new BodyMapPoint(HumanoidBone.LeftUpperArm, 0.28f, 0.29f),
            new BodyMapPoint(HumanoidBone.LeftLowerArm, 0.18f, 0.37f),
            new BodyMapPoint(HumanoidBone.LeftHand, 0.09f, 0.44f),

            new BodyMapPoint(HumanoidBone.RightShoulder, 0.61f, 0.23f),
            new BodyMapPoint(HumanoidBone.RightUpperArm, 0.72f, 0.29f),
            new BodyMapPoint(HumanoidBone.RightLowerArm, 0.82f, 0.37f),
            new BodyMapPoint(HumanoidBone.RightHand, 0.91f, 0.44f),

            new BodyMapPoint(HumanoidBone.LeftUpperLeg, 0.44f, 0.61f),
            new BodyMapPoint(HumanoidBone.LeftLowerLeg, 0.42f, 0.76f),
            new BodyMapPoint(HumanoidBone.LeftFoot, 0.40f, 0.90f),
            new BodyMapPoint(HumanoidBone.LeftToes, 0.35f, 0.94f),

            new BodyMapPoint(HumanoidBone.RightUpperLeg, 0.56f, 0.61f),
            new BodyMapPoint(HumanoidBone.RightLowerLeg, 0.58f, 0.76f),
            new BodyMapPoint(HumanoidBone.RightFoot, 0.60f, 0.90f),
            new BodyMapPoint(HumanoidBone.RightToes, 0.65f, 0.94f)
        };

    private void SelectFirstMissingRequired()
    {
        foreach (HumanoidBone candidate
                 in HumanoidBoneCatalog.Required)
        {
            if (!_mapping.IsMapped(
                    candidate))
            {
                _selectedSemantic =
                    candidate;

                return;
            }
        }

        _selectedSemantic =
            HumanoidBone.Hips;
    }

    private void SelectNextMissingRequired(
        HumanoidBone justMapped)
    {
        if (!_autoAdvanceRequired ||
            !HumanoidBoneCatalog.IsRequired(
                justMapped))
        {
            return;
        }

        IReadOnlyList<HumanoidBone> required =
            HumanoidBoneCatalog.Required;

        int currentIndex =
            0;

        for (int index = 0;
             index <
                required.Count;
             index++)
        {
            if (required[index] ==
                justMapped)
            {
                currentIndex =
                    index;

                break;
            }
        }

        for (int offset = 1;
             offset <=
                required.Count;
             offset++)
        {
            HumanoidBone candidate =
                required[
                    (currentIndex +
                     offset) %
                    required.Count];

            if (!_mapping.IsMapped(
                    candidate))
            {
                _selectedSemantic =
                    candidate;

                return;
            }
        }
    }

    private readonly record struct BodyMapPoint(
        HumanoidBone Bone,
        float X,
        float Y);

    private void DrawSemanticSection(
        string title,
        IEnumerable<HumanoidBone> bones)
    {
        ImGui.SeparatorText(
            title);

        foreach (HumanoidBone semantic
                 in bones)
        {
            string display =
                HumanoidRigAuthoring.DisplayName(
                    semantic);

            string? mapped =
                _mapping.GetBoneName(
                    semantic);

            string haystack =
                $"{display} {mapped}";

            if (!string.IsNullOrWhiteSpace(
                    _semanticSearch) &&
                !haystack.Contains(
                    _semanticSearch,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool selected =
                semantic ==
                _selectedSemantic;

            string label =
                mapped ==
                    null
                    ? $"{display}   [None]##Semantic:{semantic}"
                    : $"{display}   -> {mapped}##Semantic:{semantic}";

            if (ImGui.Selectable(
                    label,
                    selected))
            {
                _selectedSemantic =
                    semantic;
            }
        }
    }

    private void DrawSearchableSourceBonePicker()
    {
        if (_model?.Skeleton ==
            null)
        {
            return;
        }

        string? current =
            _mapping.GetBoneName(
                _selectedSemantic);

        string preview =
            current ??
            "None";

        if (!ImGui.BeginCombo(
                "Source Bone",
                preview))
        {
            return;
        }

        if (ImGui.IsWindowAppearing())
        {
            _boneSearch =
                string.Empty;

            ImGui.SetKeyboardFocusHere();
        }

        ImGui.InputTextWithHint(
            "##BoneSearch",
            "Search source bones...",
            ref _boneSearch,
            128);

        if (ImGui.Selectable(
                "None",
                current ==
                    null))
        {
            _mapping.ClearBone(
                _selectedSemantic);

            _dirty =
                true;
        }

        int sourceIndex =
            0;

        foreach (Bone bone
                 in _model.Skeleton.Bones
                     .OrderBy(
                         bone =>
                             bone.Name,
                         StringComparer.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(
                    _boneSearch) &&
                !bone.Name.Contains(
                    _boneSearch,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ImGui.PushID(
                sourceIndex++);

            bool selected =
                string.Equals(
                    bone.Name,
                    current,
                    StringComparison.OrdinalIgnoreCase);

            if (ImGui.Selectable(
                    bone.Name,
                    selected))
            {
                AssignBone(
                    _selectedSemantic,
                    bone.Name);
            }

            if (selected)
            {
                ImGui.SetItemDefaultFocus();
            }

            ImGui.PopID();
        }

        ImGui.EndCombo();
    }

    private void DrawPreview()
    {
        if (_model?.Skeleton ==
            null)
        {
            ImGui.TextWrapped(
                "No skeleton is available for preview.");

            return;
        }

        ImGui.TextDisabled(
            "Bind/reference pose  |  LMB orbit  |  Wheel zoom  |  F frame");

        ImGui.TextDisabled(
            "Hover a joint to see its bone name. Select a Humanoid slot on the left, then click a joint to assign it.");

        ImGui.Checkbox(
            "Mesh",
            ref _showMesh);

        ImGui.SameLine();

        ImGui.Checkbox(
            "Skeleton",
            ref _showSkeleton);

        ImGui.SameLine();

        ImGui.BeginDisabled(
            !_showSkeleton);

        ImGui.Checkbox(
            "Mapped Only",
            ref _mappedBonesOnly);

        ImGui.EndDisabled();

        ImGui.SameLine();

        ImGui.TextDisabled(
            "Blue = skeleton  Green = mapped  Orange = selected  Yellow = hovered");

        Vector2 available =
            ImGui.GetContentRegionAvail();

        Vector2 canvasSize =
            new(
                Math.Max(
                    available.X,
                    240.0f),
                Math.Max(
                    available.Y,
                    320.0f));

        Vector2 canvasMin =
            ImGui.GetCursorScreenPos();

        ImGui.InvisibleButton(
            "##HumanoidRigPreviewCanvas",
            canvasSize);

        Vector2 canvasMax =
            canvasMin +
            canvasSize;

        ImDrawListPtr drawList =
            ImGui.GetWindowDrawList();

        uint background =
            ImGui.GetColorU32(
                new Vector4(
                    0.055f,
                    0.065f,
                    0.08f,
                    1.0f));

        drawList.AddRectFilled(
            canvasMin,
            canvasMax,
            background);

        bool hovered =
            ImGui.IsItemHovered();

        int hoveredBone =
            -1;

        Vector2 mouse =
            ImGui.GetIO().MousePos;

        if (_showMesh)
        {
            DrawMesh(
                drawList,
                canvasMin,
                canvasSize);
        }

        if (_showSkeleton)
        {
            DrawSkeleton(
                drawList,
                canvasMin,
                canvasSize,
                mouse,
                hovered,
                ref hoveredBone);
        }

        if (hoveredBone >=
                0 &&
            hoveredBone <
                _model.Skeleton.Bones.Count)
        {
            Bone bone =
                _model.Skeleton.Bones[
                    hoveredBone];

            HumanoidBone? mappedSemantic =
                FindSemanticForBone(
                    bone.Name);

            ImGui.BeginTooltip();

            ImGui.TextUnformatted(
                bone.Name);

            if (mappedSemantic.HasValue)
            {
                ImGui.TextDisabled(
                    $"Mapped to: {HumanoidRigAuthoring.DisplayName(mappedSemantic.Value)}");
            }
            else
            {
                ImGui.TextDisabled(
                    "Not mapped");
            }

            ImGui.TextDisabled(
                $"Click to assign to {HumanoidRigAuthoring.DisplayName(_selectedSemantic)}");

            ImGui.EndTooltip();

            if (hovered &&
                ImGui.IsMouseClicked(
                    ImGuiMouseButton.Left))
            {
                AssignBone(
                    _selectedSemantic,
                    bone.Name);
            }
        }

        HandlePreviewInput(
            hovered,
            hoveredBone);
    }

    private void DrawMesh(
        ImDrawListPtr drawList,
        Vector2 canvasMin,
        Vector2 canvasSize)
    {
        uint meshColor =
            ImGui.GetColorU32(
                new Vector4(
                    0.36f,
                    0.40f,
                    0.48f,
                    0.10f));

        foreach (PreviewLine line
                 in _meshLines)
        {
            if (!TryProject(
                    line.A,
                    canvasMin,
                    canvasSize,
                    out Vector2 a) ||
                !TryProject(
                    line.B,
                    canvasMin,
                    canvasSize,
                    out Vector2 b))
            {
                continue;
            }

            drawList.AddLine(
                a,
                b,
                meshColor,
                0.75f);
        }
    }

    private void DrawSkeleton(
        ImDrawListPtr drawList,
        Vector2 canvasMin,
        Vector2 canvasSize,
        Vector2 mouse,
        bool viewportHovered,
        ref int hoveredBone)
    {
        if (_model?.Skeleton ==
            null)
        {
            return;
        }

        uint boneOutline =
            ImGui.GetColorU32(
                new Vector4(
                    0.0f,
                    0.0f,
                    0.0f,
                    0.92f));

        uint boneLine =
            ImGui.GetColorU32(
                new Vector4(
                    0.10f,
                    0.82f,
                    1.0f,
                    1.0f));

        uint mappedJoint =
            ImGui.GetColorU32(
                new Vector4(
                    0.20f,
                    1.0f,
                    0.38f,
                    1.0f));

        uint unmappedJoint =
            ImGui.GetColorU32(
                new Vector4(
                    0.94f,
                    0.96f,
                    1.0f,
                    1.0f));

        uint selectedJoint =
            ImGui.GetColorU32(
                new Vector4(
                    1.0f,
                    0.55f,
                    0.08f,
                    1.0f));

        uint hoveredJoint =
            ImGui.GetColorU32(
                new Vector4(
                    1.0f,
                    0.95f,
                    0.15f,
                    1.0f));

        Vector2[] projected =
            new Vector2[
                _bonePositions.Length];

        bool[] visible =
            new bool[
                _bonePositions.Length];

        for (int index = 0;
             index <
                _bonePositions.Length;
             index++)
        {
            visible[index] =
                TryProject(
                    _bonePositions[index],
                    canvasMin,
                    canvasSize,
                    out projected[index]);
        }

        for (int index = 0;
             index <
                _model.Skeleton.Bones.Count;
             index++)
        {
            int parent =
                _model.Skeleton.Bones[
                    index].ParentIndex;

            if (parent <
                    0 ||
                parent >=
                    projected.Length ||
                !visible[index] ||
                !visible[parent])
            {
                continue;
            }

            if (_mappedBonesOnly)
            {
                string childName =
                    _model.Skeleton.Bones[index].Name;

                string parentName =
                    _model.Skeleton.Bones[parent].Name;

                if (!FindSemanticForBone(
                        childName).HasValue &&
                    !FindSemanticForBone(
                        parentName).HasValue)
                {
                    continue;
                }
            }

            drawList.AddLine(
                projected[parent],
                projected[index],
                boneOutline,
                5.0f);

            drawList.AddLine(
                projected[parent],
                projected[index],
                boneLine,
                3.0f);
        }

        float bestDistance =
            11.0f;

        for (int index = 0;
             index <
                _model.Skeleton.Bones.Count;
             index++)
        {
            if (!visible[index])
            {
                continue;
            }

            string boneName =
                _model.Skeleton.Bones[
                    index].Name;

            HumanoidBone? semantic =
                FindSemanticForBone(
                    boneName);

            if (_mappedBonesOnly &&
                !semantic.HasValue)
            {
                continue;
            }

            bool selected =
                semantic.HasValue &&
                semantic.Value ==
                    _selectedSemantic;

            float distance =
                viewportHovered
                    ? Vector2.Distance(
                        mouse,
                        projected[index])
                    : float.PositiveInfinity;

            bool isHovered =
                distance <
                11.0f &&
                distance <=
                bestDistance;

            if (isHovered)
            {
                bestDistance =
                    distance;

                hoveredBone =
                    index;
            }

            uint color =
                isHovered
                    ? hoveredJoint
                    : selected
                        ? selectedJoint
                        : semantic.HasValue
                            ? mappedJoint
                            : unmappedJoint;

            float radius =
                isHovered
                    ? 7.0f
                    : selected
                        ? 6.2f
                        : semantic.HasValue
                            ? 5.2f
                            : 4.2f;

            drawList.AddCircleFilled(
                projected[index],
                radius +
                2.0f,
                boneOutline,
                16);

            drawList.AddCircleFilled(
                projected[index],
                radius,
                color,
                16);
        }
    }

    private void HandlePreviewInput(
        bool hovered,
        int hoveredBone)
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
            FramePreview();
        }

        /*
         * A click on a joint is reserved for assignment. Dragging elsewhere
         * or after moving the mouse orbits the preview.
         */
        if (hoveredBone <
                0 &&
            ImGui.IsMouseDragging(
                ImGuiMouseButton.Left))
        {
            _yaw +=
                io.MouseDelta.X *
                0.30f;

            _pitch =
                Math.Clamp(
                    _pitch -
                    io.MouseDelta.Y *
                    0.30f,
                    -85.0f,
                    85.0f);
        }

        if (MathF.Abs(
                io.MouseWheel) >
            0.0001f)
        {
            _distance =
                Math.Clamp(
                    _distance *
                    MathF.Pow(
                        0.88f,
                        io.MouseWheel),
                    Math.Max(
                        _previewRadius *
                        0.08f,
                        0.01f),
                    Math.Max(
                        _previewRadius *
                        30.0f,
                        1.0f));
        }
    }

    private bool TryProject(
        Vector3 point,
        Vector2 canvasMin,
        Vector2 canvasSize,
        out Vector2 screen)
    {
        screen =
            default;

        float aspect =
            canvasSize.X /
            Math.Max(
                canvasSize.Y,
                1.0f);

        Vector3 forward =
            CameraForward();

        Vector3 position =
            _previewCenter -
            forward *
            _distance;

        Matrix4x4 view =
            Matrix4x4.CreateLookAt(
                position,
                _previewCenter,
                Vector3.UnitY);

        Matrix4x4 projection =
            Matrix4x4.CreatePerspectiveFieldOfView(
                35.0f *
                MathF.PI /
                180.0f,
                Math.Max(
                    aspect,
                    0.001f),
                Math.Max(
                    _previewRadius *
                    0.001f,
                    0.001f),
                Math.Max(
                    _previewRadius *
                    50.0f,
                    100.0f));

        Vector4 clip =
            Vector4.Transform(
                new Vector4(
                    point,
                    1.0f),
                view *
                projection);

        if (!float.IsFinite(
                clip.W) ||
            clip.W <=
                0.00001f)
        {
            return false;
        }

        float x =
            clip.X /
            clip.W;

        float y =
            clip.Y /
            clip.W;

        if (!float.IsFinite(
                x) ||
            !float.IsFinite(
                y) ||
            x <
                -1.25f ||
            x >
                1.25f ||
            y <
                -1.25f ||
            y >
                1.25f)
        {
            return false;
        }

        screen =
            new Vector2(
                canvasMin.X +
                (
                    x *
                    0.5f +
                    0.5f
                ) *
                canvasSize.X,
                canvasMin.Y +
                (
                    1.0f -
                    (
                        y *
                        0.5f +
                        0.5f
                    )
                ) *
                canvasSize.Y);

        return true;
    }

    private Vector3 CameraForward()
    {
        float yaw =
            _yaw *
            MathF.PI /
            180.0f;

        float pitch =
            _pitch *
            MathF.PI /
            180.0f;

        return
            Vector3.Normalize(
                new Vector3(
                    MathF.Cos(
                        pitch) *
                    MathF.Cos(
                        yaw),
                    MathF.Sin(
                        pitch),
                    MathF.Cos(
                        pitch) *
                    MathF.Sin(
                        yaw)));
    }

    private void FramePreview()
    {
        _yaw =
            -90.0f;

        _pitch =
            -5.0f;

        _distance =
            Math.Max(
                _previewRadius *
                2.8f,
                0.5f);
    }

    private void BuildPreviewGeometry()
    {
        _meshLines.Clear();

        if (_model ==
            null)
        {
            _bonePositions =
                Array.Empty<Vector3>();

            return;
        }

        Dictionary<string, int> nodeIndices =
            _model.Nodes
                .Select(
                    (node, index) =>
                        new
                        {
                            node.Key,
                            Index =
                                index
                        })
                .ToDictionary(
                    item =>
                        item.Key,
                    item =>
                        item.Index,
                    StringComparer.Ordinal);

        int[] parentIndices =
            new int[
                _model.Nodes.Count];

        Array.Fill(
            parentIndices,
            -1);

        for (int index = 0;
             index <
                _model.Nodes.Count;
             index++)
        {
            string? parentKey =
                _model.Nodes[index]
                    .ParentKey;

            if (parentKey !=
                    null &&
                nodeIndices.TryGetValue(
                    parentKey,
                    out int parent))
            {
                parentIndices[index] =
                    parent;
            }
        }

        Matrix4x4[] globals =
            new Matrix4x4[
                _model.Nodes.Count];

        byte[] states =
            new byte[
                _model.Nodes.Count];

        for (int index = 0;
             index <
                _model.Nodes.Count;
             index++)
        {
            ResolveNodeGlobal(
                index);
        }

        Dictionary<string, Matrix4x4> meshTransforms =
            new(
                StringComparer.Ordinal);

        for (int index = 0;
             index <
                _model.Nodes.Count;
             index++)
        {
            foreach (string meshKey
                     in _model.Nodes[index]
                         .MeshKeys)
            {
                meshTransforms.TryAdd(
                    meshKey,
                    globals[index]);
            }
        }

        int triangleCount =
            _model.Meshes.Sum(
                mesh =>
                    mesh.Indices.Length /
                    3);

        int triangleStep =
            Math.Max(
                1,
                triangleCount /
                MaximumPreviewTriangles);

        int triangleOrdinal =
            0;

        var boundsPoints =
            new List<Vector3>();

        foreach (ImportedMesh mesh
                 in _model.Meshes)
        {
            Matrix4x4 transform =
                meshTransforms.TryGetValue(
                    mesh.Key,
                    out Matrix4x4 value)
                    ? value
                    : Matrix4x4.Identity;

            for (int index = 0;
                 index + 2 <
                    mesh.Indices.Length;
                 index +=
                    3)
            {
                if (triangleOrdinal++ %
                        triangleStep !=
                    0)
                {
                    continue;
                }

                int ia =
                    (int)mesh.Indices[index];

                int ib =
                    (int)mesh.Indices[
                        index +
                        1];

                int ic =
                    (int)mesh.Indices[
                        index +
                        2];

                Vector3 a =
                    TransformVertex(
                        mesh,
                        ia,
                        transform);

                Vector3 b =
                    TransformVertex(
                        mesh,
                        ib,
                        transform);

                Vector3 c =
                    TransformVertex(
                        mesh,
                        ic,
                        transform);

                _meshLines.Add(
                    new PreviewLine(
                        a,
                        b));

                _meshLines.Add(
                    new PreviewLine(
                        b,
                        c));

                _meshLines.Add(
                    new PreviewLine(
                        c,
                        a));

                boundsPoints.Add(
                    a);

                boundsPoints.Add(
                    b);

                boundsPoints.Add(
                    c);
            }
        }

        if (_model.Skeleton !=
            null)
        {
            _bonePositions =
                new Vector3[
                    _model.Skeleton.Bones.Count];

            Matrix4x4 skeletonBindFrame =
                ResolveSkeletonBindFrame(
                    meshTransforms);

            Dictionary<string, int> nodeByName =
                _model.Nodes
                    .Select(
                        (node, index) =>
                            new
                            {
                                node.Name,
                                Index =
                                    index
                            })
                    .Where(
                        item =>
                            !string.IsNullOrWhiteSpace(
                                item.Name))
                    .GroupBy(
                        item =>
                            item.Name,
                        StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        group =>
                            group.Key,
                        group =>
                            group.First().Index,
                        StringComparer.OrdinalIgnoreCase);

            bool meshProvidedFrame =
                boundsPoints.Count >
                0;

            for (int index = 0;
                 index <
                    _model.Skeleton.Bones.Count;
                 index++)
            {
                Bone bone =
                    _model.Skeleton.Bones[
                        index];

                if (Matrix4x4.Invert(
                        bone.BindPose,
                        out Matrix4x4 inverseBind))
                {
                    /*
                     * IMPORTANT:
                     *
                     * Bone.BindPose is one inverse-bind matrix per skeleton bone.
                     * FbxModelImporter currently keeps the FIRST offset matrix it
                     * encounters for that bone. C9I tried to infer a different
                     * mesh frame per bone from vertex weights. On multi-part FBX
                     * characters that mixes unrelated mesh-node spaces inside a
                     * single skeleton and can turn a perfectly valid humanoid
                     * into the exploded/contorted preview seen in C9I.
                     *
                     * Use one coherent skin frame for the entire skeleton. This
                     * matches the single-space SkeletonAsset contract and the
                     * way the stored inverse binds are consumed by the runtime.
                     *
                     * Row-vector relationship used by SkeletalMeshRenderer:
                     *
                     * inverseBind * jointGlobal * inverse(meshGlobal) = I
                     * jointGlobal = inverse(inverseBind) * meshGlobal
                     */
                    Matrix4x4 bindGlobal =
                        inverseBind *
                        skeletonBindFrame;

                    _bonePositions[index] =
                        bindGlobal.Translation;

                    boundsPoints.Add(
                        _bonePositions[index]);

                    continue;
                }

                bool resolvedFromNode =
                    nodeByName.TryGetValue(
                        bone.Name,
                        out int nodeIndex) &&
                    nodeIndex >=
                        0 &&
                    nodeIndex <
                        globals.Length;

                if (resolvedFromNode)
                {
                    _bonePositions[index] =
                        globals[nodeIndex]
                            .Translation;

                    if (!meshProvidedFrame)
                    {
                        boundsPoints.Add(
                            _bonePositions[index]);
                    }
                }
            }
        }
        else
        {
            _bonePositions =
                Array.Empty<Vector3>();
        }

        if (boundsPoints.Count ==
            0)
        {
            _previewCenter =
                new Vector3(
                    0.0f,
                    0.9f,
                    0.0f);

            _previewRadius =
                1.0f;
        }
        else
        {
            Vector3 minimum =
                boundsPoints.Aggregate(
                    new Vector3(
                        float.PositiveInfinity),
                    Vector3.Min);

            Vector3 maximum =
                boundsPoints.Aggregate(
                    new Vector3(
                        float.NegativeInfinity),
                    Vector3.Max);

            _previewCenter =
                (
                    minimum +
                    maximum
                ) *
                0.5f;

            _previewRadius =
                Math.Max(
                    (
                        maximum -
                        minimum
                    ).Length() *
                    0.5f,
                    0.1f);
        }

        FramePreview();

        Matrix4x4 ResolveNodeGlobal(
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
                globals[index] =
                    _model.Nodes[index]
                        .LocalTransform;

                states[index] =
                    2;

                return globals[index];
            }

            states[index] =
                1;

            int parent =
                parentIndices[index];

            globals[index] =
                parent >=
                    0 &&
                parent <
                    _model.Nodes.Count
                    ? _model.Nodes[index]
                        .LocalTransform *
                        ResolveNodeGlobal(
                            parent)
                    : _model.Nodes[index]
                        .LocalTransform;

            states[index] =
                2;

            return globals[index];
        }
    }

    /// <summary>
    /// Resolves ONE model-space frame for the imported skin.
    ///
    /// SkeletonAsset owns one inverse-bind matrix per bone, not one per
    /// mesh/bone pair. Mixing a separate mesh frame into every bone therefore
    /// creates an internally inconsistent skeleton on multi-part FBX models.
    ///
    /// The first imported mesh with valid skin weights is the best match for
    /// FbxModelImporter, which also walks source meshes in order when it keeps
    /// the first inverse-bind matrix for a bone.
    /// </summary>
    private Matrix4x4 ResolveSkeletonBindFrame(
        IReadOnlyDictionary<string, Matrix4x4> meshTransforms)
    {
        if (_model ==
            null)
        {
            return Matrix4x4.Identity;
        }

        foreach (ImportedMesh mesh
                 in _model.Meshes)
        {
            if (!meshTransforms.TryGetValue(
                    mesh.Key,
                    out Matrix4x4 meshFrame))
            {
                continue;
            }

            int vertexCount =
                mesh.Vertices.Length /
                8;

            if (vertexCount <=
                    0 ||
                mesh.JointIndices.Length !=
                    vertexCount ||
                mesh.JointWeights.Length !=
                    vertexCount)
            {
                continue;
            }

            bool hasSkinWeights =
                false;

            for (int vertexIndex = 0;
                 vertexIndex <
                    vertexCount;
                 vertexIndex++)
            {
                Vector4 weights =
                    mesh.JointWeights[
                        vertexIndex];

                if (weights.X >
                        0.00001f ||
                    weights.Y >
                        0.00001f ||
                    weights.Z >
                        0.00001f ||
                    weights.W >
                        0.00001f)
                {
                    hasSkinWeights =
                        true;

                    break;
                }
            }

            if (hasSkinWeights)
            {
                return meshFrame;
            }
        }

        /*
         * No skinned mesh was found. Identity is safer than mixing arbitrary
         * node spaces. The node-pose fallback below still handles bones whose
         * inverse bind cannot be inverted.
         */
        return Matrix4x4.Identity;
    }

    private static Vector3 TransformVertex(
        ImportedMesh mesh,
        int vertexIndex,
        Matrix4x4 transform)
    {
        int offset =
            vertexIndex *
            8;

        if (offset <
                0 ||
            offset +
                2 >=
            mesh.Vertices.Length)
        {
            return Vector3.Zero;
        }

        Vector3 local =
            new(
                mesh.Vertices[offset],
                mesh.Vertices[
                    offset +
                    1],
                mesh.Vertices[
                    offset +
                    2]);

        return
            Vector3.Transform(
                local,
                transform);
    }

    private HumanoidBone? FindSemanticForBone(
        string sourceBoneName)
    {
        foreach ((HumanoidBone semantic, string mapped)
                 in _mapping.Bones)
        {
            if (string.Equals(
                    mapped,
                    sourceBoneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return semantic;
            }
        }

        return null;
    }

    private void AssignBone(
        HumanoidBone semantic,
        string sourceBoneName)
    {
        HumanoidRigAuthoring.ClearDuplicateAssignment(
            _mapping,
            semantic,
            sourceBoneName);

        _mapping.SetBone(
            semantic,
            sourceBoneName);

        _dirty =
            true;

        SelectNextMissingRequired(
            semantic);
    }

    private void Apply(
        EditorLog log)
    {
        if (_asset == null ||
            _project == null)
        {
            return;
        }

        _asset.Metadata.ModelImporter.RigType =
            _rigType;

        _asset.Metadata.ModelImporter.HumanoidMapping =
            _mapping.Clone();

        if (!HumanoidRigAuthoring.Persist(
                _asset,
                out string? error))
        {
            _error =
                error;

            log.Error(
                $"Could not save Humanoid mapping for '{_asset.ProjectPath}': {error}");

            return;
        }

        try
        {
            _model =
                _project.Assets.ReimportModel(
                    _asset.Guid);

            _rigType =
                _asset.Metadata.ModelImporter.RigType;

            _mapping =
                _asset.Metadata.ModelImporter.HumanoidMapping.Clone();

            BuildPreviewGeometry();

            _dirty =
                false;

            _error =
                null;

            log.Info(
                $"Applied Humanoid mapping for '{_asset.ProjectPath}'.");
        }
        catch (Exception exception)
        {
            _error =
                exception.Message;

            log.Error(
                $"Could not reimport '{_asset.ProjectPath}' after Humanoid mapping change: {exception.Message}");
        }
    }

    private void Revert(
        EditorLog log)
    {
        if (_asset == null ||
            _project == null)
        {
            return;
        }

        try
        {
            _project.AssetDatabase.Scan();

            if (!_project.AssetDatabase.TryGetAsset(
                    _asset.Guid,
                    out AssetRecord? refreshed) ||
                refreshed ==
                    null)
            {
                return;
            }

            _asset =
                refreshed;

            refreshed.Metadata.ModelImporter.Normalize();

            _rigType =
                refreshed.Metadata.ModelImporter.RigType;

            _mapping =
                refreshed.Metadata.ModelImporter.HumanoidMapping.Clone();

            _model =
                _project.Assets.LoadModel(
                    new AssetReference(
                        refreshed.Guid,
                        refreshed.ProjectPath));

            BuildPreviewGeometry();

            _dirty =
                false;

            _error =
                null;

            log.Info(
                $"Reverted Humanoid mapping for '{refreshed.ProjectPath}'.");
        }
        catch (Exception exception)
        {
            _error =
                exception.Message;

            log.Error(
                $"Could not revert Humanoid mapping: {exception.Message}");
        }
    }

    private readonly record struct PreviewLine(
        Vector3 A,
        Vector3 B);
}

internal static class HumanoidRigConfiguratorRequest
{
    private static Guid? _pending;

    public static void Request(
        Guid assetGuid)
    {
        if (assetGuid ==
            Guid.Empty)
        {
            return;
        }

        _pending =
            assetGuid;
    }

    public static Guid? Consume()
    {
        Guid? pending =
            _pending;

        _pending =
            null;

        return pending;
    }
}
