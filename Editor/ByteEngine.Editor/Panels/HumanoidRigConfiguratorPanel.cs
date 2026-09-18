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

        if (validation.IsReady)
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
            "SELECTED HUMANOID SLOT");

        ImGui.Text(
            HumanoidRigAuthoring.DisplayName(
                _selectedSemantic));

        string? current =
            _mapping.GetBoneName(
                _selectedSemantic);

        ImGui.TextDisabled(
            current ==
                null
                ? "Source bone: None"
                : $"Source bone: {current}");

        DrawSearchableSourceBonePicker();

        ImGui.SeparatorText(
            "HUMANOID SLOTS");

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
    }

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

            for (int index = 0;
                 index <
                    _model.Skeleton.Bones.Count;
                 index++)
            {
                if (Matrix4x4.Invert(
                        _model.Skeleton.Bones[index]
                            .BindPose,
                        out Matrix4x4 modelSpace))
                {
                    _bonePositions[index] =
                        modelSpace.Translation;

                    boundsPoints.Add(
                        _bonePositions[index]);
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
