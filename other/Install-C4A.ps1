param(
    [string]$RepoRoot = "C:\Users\codex\ByteEngine"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "ByteEngine C4A installer: $Message"
}

function Replace-Once(
    [string]$Text,
    [string]$Old,
    [string]$New,
    [string]$Label
) {
    $Text = $Text.Replace("`r`n", "`n")
    $Old = $Old.Replace("`r`n", "`n")
    $New = $New.Replace("`r`n", "`n")

    $first = $Text.IndexOf($Old, [System.StringComparison]::Ordinal)

    if ($first -lt 0) {
        Fail "Could not find expected source block: $Label"
    }

    $second = $Text.IndexOf(
        $Old,
        $first + $Old.Length,
        [System.StringComparison]::Ordinal
    )

    if ($second -ge 0) {
        Fail "Expected source block was not unique: $Label"
    }

    return $Text.Substring(0, $first) +
           $New +
           $Text.Substring($first + $Old.Length)
}

function Replace-Between(
    [string]$Text,
    [string]$StartMarker,
    [string]$EndMarker,
    [string]$Replacement,
    [string]$Label
) {
    $Text = $Text.Replace("`r`n", "`n")
    $StartMarker = $StartMarker.Replace("`r`n", "`n")
    $EndMarker = $EndMarker.Replace("`r`n", "`n")
    $Replacement = $Replacement.Replace("`r`n", "`n")

    $start = $Text.IndexOf(
        $StartMarker,
        [System.StringComparison]::Ordinal
    )

    if ($start -lt 0) {
        Fail "Could not find start marker: $Label"
    }

    $end = $Text.IndexOf(
        $EndMarker,
        $start + $StartMarker.Length,
        [System.StringComparison]::Ordinal
    )

    if ($end -lt 0) {
        Fail "Could not find end marker: $Label"
    }

    return $Text.Substring(0, $start) +
           $Replacement +
           $Text.Substring($end)
}

function Assert-TrackedBlob(
    [string]$RelativePath,
    [string]$ExpectedBlob
) {
    git -C $RepoRoot diff --quiet -- $RelativePath

    if ($LASTEXITCODE -ne 0) {
        Fail "$RelativePath has local unstaged changes. Restore or inspect them before installing C4A."
    }

    git -C $RepoRoot diff --cached --quiet -- $RelativePath

    if ($LASTEXITCODE -ne 0) {
        Fail "$RelativePath has staged changes. Restore or inspect them before installing C4A."
    }

    $actual = (
        git -C $RepoRoot rev-parse "HEAD:$RelativePath"
    ).Trim()

    if ($LASTEXITCODE -ne 0) {
        Fail "Could not read HEAD blob for $RelativePath"
    }

    if ($actual -ne $ExpectedBlob) {
        Fail @"
$RelativePath does not match the v0.11-C4A baseline.
Expected HEAD blob: $ExpectedBlob
Actual HEAD blob:   $actual

Stop here. No tracked source files were changed.
"@
    }
}

$expectedHead =
    "39daa7fbd0f6e3dd727e7df61eb8550ed4f9047c"

$head = (
    git -C $RepoRoot rev-parse HEAD
).Trim()

if ($LASTEXITCODE -ne 0) {
    Fail "Could not read Git HEAD."
}

if ($head -ne $expectedHead) {
    Fail @"
This installer was built for:
$expectedHead

Current HEAD is:
$head

No tracked source files were changed.
"@
}

$tracked = @{
    "Editor/ByteEngine.Editor/EditorState.cs" =
        "cd8e67448359eb1b609165f1ed2adf9684aff99a"

    "Editor/ByteEngine.Editor/Panels/AssetsPanel.cs" =
        "e1ae599cfadca171808ff6c983079e2d193bbe8e"

    "Editor/ByteEngine.Editor/Panels/InspectorPanel.cs" =
        "65b5422f7ffc2f06fd8ae085dc9b2e15b9574c46"

    "Editor/ByteEngine.Editor/ComponentPropertyRenderer.cs" =
        "6bd61e128d139b90d3d9f64a0b0ce8e50fec340b"

    "Editor/ByteEngine.Editor/EditorApplication.cs" =
        "89d7ff64ef38714f171c8ce09686f1ceb3e45260"
}

foreach ($pair in $tracked.GetEnumerator()) {
    Assert-TrackedBlob $pair.Key $pair.Value
}

# Work entirely in memory first. Nothing is written until every source
# transformation has found the exact expected baseline structure.
$statePath =
    Join-Path $RepoRoot "Editor\ByteEngine.Editor\EditorState.cs"

$assetsPath =
    Join-Path $RepoRoot "Editor\ByteEngine.Editor\Panels\AssetsPanel.cs"

$inspectorPath =
    Join-Path $RepoRoot "Editor\ByteEngine.Editor\Panels\InspectorPanel.cs"

$propertiesPath =
    Join-Path $RepoRoot "Editor\ByteEngine.Editor\ComponentPropertyRenderer.cs"

$appPath =
    Join-Path $RepoRoot "Editor\ByteEngine.Editor\EditorApplication.cs"

$state =
    [IO.File]::ReadAllText($statePath)

$assets =
    [IO.File]::ReadAllText($assetsPath)

$inspector =
    [IO.File]::ReadAllText($inspectorPath)

$properties =
    [IO.File]::ReadAllText($propertiesPath)

$app =
    [IO.File]::ReadAllText($appPath)

# -------------------------------------------------------------------------
# EditorState.cs
# -------------------------------------------------------------------------

$stateOld = @'
    public string? SelectedAssetPath { get; set; }

    public EditorCamera Camera { get; } =
'@

$stateNew = @'
    public string? SelectedAssetPath { get; set; }

    /// <summary>
    /// Virtual animation sub-asset selected beneath a Model3D asset.
    /// The model remains the real AssetDatabase record; this stores only the
    /// imported clip key so no .byteanimation extraction is required.
    /// </summary>
    public string? SelectedModelAnimationKey { get; set; }

    public EditorCamera Camera { get; } =
'@

$state = Replace-Once `
    $state `
    $stateOld `
    $stateNew `
    "EditorState animation sub-asset selection"

# -------------------------------------------------------------------------
# AssetsPanel.cs
# -------------------------------------------------------------------------

$drawFiles = @'
    private void DrawFiles(
        EditorState state,
        EditorLog log)
    {
        string[] orderedFiles = _files.ToArray();
        _assetSelection.Retain(orderedFiles);
        _assetBounds.Clear();

        for (int fileIndex = 0; fileIndex < orderedFiles.Length; fileIndex++)
        {
            string file = orderedFiles[fileIndex];

            string projectPath =
                Path.GetRelativePath(
                        _project.ProjectRoot,
                        file)
                    .Replace(
                        '\\',
                        '/');

            _project.AssetDatabase.TryGetAsset(
                projectPath,
                out AssetRecord? asset);

            if (asset?.Type ==
                AssetType.Model3D)
            {
                DrawModelAssetEntry(
                    state,
                    log,
                    orderedFiles,
                    fileIndex,
                    file,
                    asset);

                continue;
            }

            string icon =
                GetAssetIcon(
                    asset?.Type);

            bool selected =
                _assetSelection.Contains(
                    file);

            bool clicked =
                ImGui.Selectable(
                    $"{icon} {Path.GetFileName(file)}##asset:{file}",
                    selected);

            if (clicked)
            {
                ImGuiIOPtr io =
                    ImGui.GetIO();

                _assetSelection.Click(
                    orderedFiles,
                    fileIndex,
                    io.KeyCtrl,
                    io.KeyShift);

                SyncPrimaryAssetSelection(
                    state);
            }

            _assetBounds.Add(
                new AssetSelectionBounds(
                    file,
                    ImGui.GetItemRectMin(),
                    ImGui.GetItemRectMax()));

            if (asset !=
                    null &&
                ImGui.IsItemHovered() &&
                ImGui.IsMouseDoubleClicked(
                    ImGuiMouseButton.Left))
            {
                OpenAsset(
                    asset,
                    log);
            }

            DrawAssetContextMenu(
                state,
                log,
                asset,
                file);

            DrawAssetDragSource(
                asset,
                file);
        }

        bool emptySpaceClicked =
            ImGui.IsWindowHovered() &&
            ImGui.IsMouseClicked(
                ImGuiMouseButton.Left) &&
            !ImGui.IsAnyItemHovered();

        if (emptySpaceClicked)
        {
            _assetMarquee =
                true;

            _assetMarqueeStart =
                ImGui.GetMousePos();

            _assetMarqueeEnd =
                _assetMarqueeStart;
        }

        if (_assetMarquee)
        {
            _assetMarqueeEnd =
                ImGui.GetMousePos();

            Vector2 minimum =
                Vector2.Min(
                    _assetMarqueeStart,
                    _assetMarqueeEnd);

            Vector2 maximum =
                Vector2.Max(
                    _assetMarqueeStart,
                    _assetMarqueeEnd);

            ImGui.GetWindowDrawList()
                .AddRectFilled(
                    minimum,
                    maximum,
                    ImGui.GetColorU32(
                        new Vector4(
                            .2f,
                            .55f,
                            1f,
                            .12f)));

            ImGui.GetWindowDrawList()
                .AddRect(
                    minimum,
                    maximum,
                    ImGui.GetColorU32(
                        new Vector4(
                            .3f,
                            .7f,
                            1f,
                            .9f)));

            if (!ImGui.IsMouseDown(
                    ImGuiMouseButton.Left))
            {
                _assetSelection.Marquee(
                    _assetBounds,
                    _assetMarqueeStart,
                    _assetMarqueeEnd,
                    ImGui.GetIO().KeyCtrl);

                SyncPrimaryAssetSelection(
                    state);

                _assetMarquee =
                    false;
            }
        }
    }

    private void DrawModelAssetEntry(
        EditorState state,
        EditorLog log,
        IReadOnlyList<string> orderedFiles,
        int fileIndex,
        string file,
        AssetRecord asset)
    {
        bool selected =
            _assetSelection.Contains(
                file) &&
            string.IsNullOrWhiteSpace(
                state.SelectedModelAnimationKey);

        ImGuiTreeNodeFlags flags =
            ImGuiTreeNodeFlags.OpenOnArrow |
            ImGuiTreeNodeFlags.SpanAvailWidth;

        if (selected)
        {
            flags |=
                ImGuiTreeNodeFlags.Selected;
        }

        bool open =
            ImGui.TreeNodeEx(
                $"[3D] {Path.GetFileName(file)}##model-asset:{file}",
                flags);

        if (ImGui.IsItemClicked(
                ImGuiMouseButton.Left))
        {
            ImGuiIOPtr io =
                ImGui.GetIO();

            _assetSelection.Click(
                orderedFiles,
                fileIndex,
                io.KeyCtrl,
                io.KeyShift);

            state.SelectedModelAnimationKey =
                null;

            SyncPrimaryAssetSelection(
                state);
        }

        _assetBounds.Add(
            new AssetSelectionBounds(
                file,
                ImGui.GetItemRectMin(),
                ImGui.GetItemRectMax()));

        if (ImGui.IsItemHovered() &&
            ImGui.IsMouseDoubleClicked(
                ImGuiMouseButton.Left))
        {
            OpenAsset(
                asset,
                log);
        }

        DrawAssetContextMenu(
            state,
            log,
            asset,
            file);

        DrawAssetDragSource(
            asset,
            file);

        if (!open)
        {
            return;
        }

        try
        {
            ModelAsset model =
                _project.Assets.LoadModel(
                    new AssetReference(
                        asset.Guid,
                        asset.ProjectPath));

            if (model.Animations.Count ==
                0)
            {
                ImGui.TextDisabled(
                    "  [ANIM] No animation clips");
            }
            else
            {
                foreach (var animation
                         in model.Animations)
                {
                    bool clipSelected =
                        state.SelectedAssetId ==
                            asset.Guid &&
                        string.Equals(
                            state.SelectedModelAnimationKey,
                            animation.Key,
                            StringComparison.Ordinal);

                    string label =
                        $"[ANIM] {animation.Name}  {animation.Duration:0.00}s##animation:{asset.Guid}:{animation.Key}";

                    if (ImGui.Selectable(
                            label,
                            clipSelected))
                    {
                        _assetSelection.Clear();

                        state.SelectedAssetId =
                            asset.Guid;

                        state.SelectedAssetPath =
                            asset.ProjectPath;

                        state.SelectedModelAnimationKey =
                            animation.Key;

                        state.SelectedObject =
                            null;
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(
                            $"Animation Clip\n{animation.Name}\nDuration: {animation.Duration:0.000}s");
                    }
                }
            }
        }
        catch (Exception exception)
        {
            ImGui.TextColored(
                new Vector4(
                    1.0f,
                    0.35f,
                    0.35f,
                    1.0f),
                "  Animation discovery failed");

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(
                    exception.Message);
            }
        }

        ImGui.TreePop();
    }

'@

$assets = Replace-Between `
    $assets `
    "    private void DrawFiles(" `
    "    private void SyncPrimaryAssetSelection" `
    $drawFiles `
    "AssetsPanel DrawFiles"

$syncSelection = @'
    private void SyncPrimaryAssetSelection(EditorState state)
    {
        state.SelectedModelAnimationKey =
            null;

        string? primary =
            _assetSelection.PrimaryPath;

        if (primary ==
            null)
        {
            state.SelectedAssetId =
                null;

            state.SelectedAssetPath =
                null;

            return;
        }

        string projectPath =
            Path.GetRelativePath(
                    _project.ProjectRoot,
                    primary)
                .Replace(
                    Path.DirectorySeparatorChar,
                    '/');

        _project.AssetDatabase.TryGetAsset(
            projectPath,
            out AssetRecord? asset);

        state.SelectedAssetId =
            asset?.Guid;

        state.SelectedAssetPath =
            projectPath;

        state.SelectedObject =
            null;
    }

'@

$assets = Replace-Between `
    $assets `
    "    private void SyncPrimaryAssetSelection" `
    "    private void OpenAsset(" `
    $syncSelection `
    "AssetsPanel SyncPrimaryAssetSelection"

# -------------------------------------------------------------------------
# InspectorPanel.cs
# -------------------------------------------------------------------------

$inspector = Replace-Once `
    $inspector `
    "using ByteEngine.Core.Assets;`nusing ByteEngine.Core.Blueprints;" `
    "using ByteEngine.Core.Assets;`nusing ByteEngine.Core.Assets.Importers;`nusing ByteEngine.Core.Blueprints;" `
    "Inspector importer using"

$inspector = Replace-Once `
    $inspector `
    "internal sealed class InspectorPanel`n{" `
    "internal sealed class InspectorPanel`n    : IDisposable`n{" `
    "Inspector IDisposable"

$oldInspectorSignature = @'
    private readonly CameraActivationPromptState _cameraActivationPrompt = new();

    public void Draw(EditorState state, EditorProjectContext project, Action<AssetReference> openBlueprint)
'@

$newInspectorSignature = @'
    private readonly CameraActivationPromptState _cameraActivationPrompt = new();
    private readonly AnimationClipPreview _animationPreview = new();

    public void Draw(
        EditorState state,
        EditorProjectContext project,
        Renderer2D renderer,
        Renderer3D renderer3D,
        int windowWidth,
        int windowHeight,
        Action<AssetReference> openBlueprint)
'@

$inspector = Replace-Once `
    $inspector `
    $oldInspectorSignature `
    $newInspectorSignature `
    "Inspector Draw signature"

$oldAssetDraw = @'
            if (state.SelectedAssetId.HasValue || state.SelectedAssetPath != null) DrawAssetOrEmpty(state, project);
'@

$newAssetDraw = @'
            if (state.SelectedAssetId.HasValue || state.SelectedAssetPath != null)
            {
                DrawAssetOrEmpty(
                    state,
                    project,
                    renderer,
                    renderer3D,
                    windowWidth,
                    windowHeight);
            }
'@

$inspector = Replace-Once `
    $inspector `
    $oldAssetDraw `
    $newAssetDraw `
    "Inspector asset selection draw"

$inspector = Replace-Once `
    $inspector `
    "        bool readOnly = state.Mode != EditorMode.Edit;" `
    "        _animationPreview.Reset();`n`n        bool readOnly = state.Mode != EditorMode.Edit;" `
    "Inspector preview reset for object selection"

$assetInspectorTail = @'
    private void DrawAssetOrEmpty(
        EditorState state,
        EditorProjectContext project,
        Renderer2D renderer,
        Renderer3D renderer3D,
        int windowWidth,
        int windowHeight)
    {
        AssetRecord? asset =
            null;

        if (state.SelectedAssetId.HasValue)
        {
            project.AssetDatabase.TryGetAsset(
                state.SelectedAssetId.Value,
                out asset);
        }

        if (asset ==
                null &&
            state.SelectedAssetPath !=
                null)
        {
            project.AssetDatabase.TryGetAsset(
                state.SelectedAssetPath,
                out asset);
        }

        if (asset ==
            null)
        {
            _animationPreview.Reset();

            ImGui.TextDisabled(
                state.SelectedAssetPath ==
                    null
                    ? "Select a GameObject or asset."
                    : "Missing Asset");

            return;
        }

        if (asset.Type ==
            AssetType.Model3D)
        {
            try
            {
                ModelAsset model =
                    project.Assets.LoadModel(
                        new AssetReference(
                            asset.Guid,
                            asset.ProjectPath));

                if (!string.IsNullOrWhiteSpace(
                        state.SelectedModelAnimationKey))
                {
                    ImportedAnimation? selectedAnimation =
                        model.Animations.FirstOrDefault(
                            animation =>
                                string.Equals(
                                    animation.Key,
                                    state.SelectedModelAnimationKey,
                                    StringComparison.Ordinal));

                    if (selectedAnimation !=
                        null)
                    {
                        DrawAnimationClipAsset(
                            project,
                            asset,
                            model,
                            selectedAnimation,
                            renderer,
                            renderer3D,
                            windowWidth,
                            windowHeight);

                        return;
                    }

                    state.SelectedModelAnimationKey =
                        null;
                }

                _animationPreview.Reset();

                ImGui.Text(
                    Path.GetFileName(
                        asset.ProjectPath));

                ImGui.TextDisabled(
                    asset.ProjectPath);

                ImGui.TextDisabled(
                    $"GUID: {asset.Guid}");

                ImGui.SeparatorText(
                    "MODEL IMPORT SETTINGS");

                ImGui.Text(
                    $"Source: {Path.GetFileName(asset.ProjectPath)}");

                ImGui.Text(
                    $"Meshes: {model.Meshes.Count}");

                ImGui.Text(
                    $"Materials: {model.Materials.Count}");

                ImGui.Text(
                    $"Skeleton: {model.Skeleton?.Name ?? "None"}");

                ImGui.Text(
                    $"Bones: {model.Skeleton?.Bones.Count ?? 0}");

                ImGui.Text(
                    $"Animations: {model.Animations.Count}");

                if (model.Animations.Count >
                    0)
                {
                    ImGui.SeparatorText(
                        "ANIMATIONS");

                    foreach (ImportedAnimation animation
                             in model.Animations)
                    {
                        ImGui.BulletText(
                            $"{animation.Name}  {animation.Duration:0.00}s");
                    }
                }

                float scale =
                    asset.Metadata.ModelImporter.ImportScale;

                bool generateNormals =
                    asset.Metadata.ModelImporter.GenerateNormals;

                bool embeddedMaterials =
                    asset.Metadata.ModelImporter.PreferEmbeddedMaterials;

                bool changed =
                    ImGui.DragFloat(
                        "Import Scale",
                        ref scale,
                        .01f,
                        .0001f,
                        1000f);

                changed |=
                    ImGui.Checkbox(
                        "Generate Normals",
                        ref generateNormals);

                changed |=
                    ImGui.Checkbox(
                        "Prefer Embedded Materials",
                        ref embeddedMaterials);

                if (changed)
                {
                    project.AssetDatabase.SetModelImporterSettings(
                        asset.Guid,
                        scale,
                        generateNormals,
                        embeddedMaterials);
                }

                if (ImGui.Button(
                        "Reimport"))
                {
                    project.Assets.ReimportModel(
                        asset.Guid);
                }
            }
            catch (Exception exception)
            {
                _animationPreview.Reset();

                ImGui.TextColored(
                    new Vector4(
                        1f,
                        .35f,
                        .35f,
                        1f),
                    "Model import failed");

                ImGui.TextWrapped(
                    exception.Message);
            }

            return;
        }

        _animationPreview.Reset();

        ImGui.Text(
            Path.GetFileName(
                asset.ProjectPath));

        ImGui.TextDisabled(
            asset.ProjectPath);

        ImGui.TextDisabled(
            $"GUID: {asset.Guid}");

        if (asset.Type !=
            AssetType.Texture2D)
        {
            return;
        }

        ImGui.SeparatorText(
            "Texture Import Settings");

        TextureFilter filter =
            asset.Metadata.Importer.Filter;

        if (ImGui.BeginCombo(
                "Filter",
                filter.ToString()))
        {
            foreach (TextureFilter option
                     in Enum.GetValues<TextureFilter>())
            {
                bool selected =
                    filter ==
                    option;

                if (ImGui.Selectable(
                        option.ToString(),
                        selected))
                {
                    project.AssetDatabase.SetTextureFilter(
                        asset.Guid,
                        option);
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }
    }

    private void DrawAnimationClipAsset(
        EditorProjectContext project,
        AssetRecord asset,
        ModelAsset model,
        ImportedAnimation animation,
        Renderer2D renderer,
        Renderer3D renderer3D,
        int windowWidth,
        int windowHeight)
    {
        ImGui.Text(
            animation.Name);

        ImGui.TextDisabled(
            $"{Path.GetFileName(asset.ProjectPath)} / Animation Clip");

        ImGui.SeparatorText(
            "ANIMATION CLIP");

        ImGui.Text(
            $"Name: {animation.Name}");

        ImGui.Text(
            $"Source: {Path.GetFileName(asset.ProjectPath)}");

        ImGui.TextDisabled(
            $"Key: {animation.Key}");

        ImGui.Text(
            $"Duration: {animation.Duration:0.000} s");

        ImGui.Text(
            $"Channels: {animation.Channels.Count}");

        int keyframes =
            animation.Channels.Sum(
                channel =>
                    (channel.Translation?.Keys.Count ?? 0) +
                    (channel.Rotation?.Keys.Count ?? 0) +
                    (channel.Scale?.Keys.Count ?? 0));

        ImGui.Text(
            $"Keyframes: {keyframes}");

        ImGui.Text(
            $"Skeleton Bones: {model.Skeleton?.Bones.Count ?? 0}");

        ImGui.SeparatorText(
            "PREVIEW");

        _animationPreview.Draw(
            project,
            asset,
            model,
            animation,
            renderer,
            renderer3D,
            windowWidth,
            windowHeight);
    }

    public void Dispose()
    {
        _animationPreview.Dispose();
    }
}
'@

$inspectorStart =
    $inspector.IndexOf(
        "    private static void DrawAssetOrEmpty",
        [System.StringComparison]::Ordinal
    )

if ($inspectorStart -lt 0) {
    Fail "Could not find Inspector asset-tail start."
}

$inspector =
    $inspector.Substring(0, $inspectorStart) +
    $assetInspectorTail

# -------------------------------------------------------------------------
# ComponentPropertyRenderer.cs
# -------------------------------------------------------------------------

$properties = Replace-Once `
    $properties `
    "using System.Text.RegularExpressions;`nusing ByteEngine.Core.Assets;" `
    "using System.Text.RegularExpressions;`nusing ByteEngine.Core.Animation;`nusing ByteEngine.Core.Assets;" `
    "ComponentPropertyRenderer animation using"

$oldStringFallback = @'
            else if (descriptor.Property.PropertyType == typeof(Guid) &&
                     descriptor.Property.Name.EndsWith("TagId", StringComparison.Ordinal))
            {
                Guid tag = before is Guid value ? value : Guid.Empty;
                edited = ClassificationPickers.DrawTag(label,
                    project?.Project.Classification ?? component.GameObject.Scene?.Classification ?? ClassificationSettings.CreateDefault(), ref tag);
                after = tag;
            }
            else
            {
'@

$newStringFallback = @'
            else if (descriptor.Property.PropertyType == typeof(Guid) &&
                     descriptor.Property.Name.EndsWith("TagId", StringComparison.Ordinal))
            {
                Guid tag = before is Guid value ? value : Guid.Empty;
                edited = ClassificationPickers.DrawTag(label,
                    project?.Project.Classification ?? component.GameObject.Scene?.Classification ?? ClassificationSettings.CreateDefault(), ref tag);
                after = tag;
            }
            else if (component is AnimationController animationController &&
                     project != null &&
                     descriptor.Property.PropertyType == typeof(string) &&
                     AnimationClipDiscovery.IsLocomotionClipProperty(descriptor.Property.Name))
            {
                string clipName =
                    before?.ToString() ??
                    string.Empty;

                edited =
                    DrawAnimationClipSelector(
                        animationController,
                        project,
                        label,
                        ref clipName);

                after =
                    clipName;
            }
            else
            {
'@

$properties = Replace-Once `
    $properties `
    $oldStringFallback `
    $newStringFallback `
    "AnimationController string picker"

$oldAfterModules = @'
        if (component is EventModuleComponent eventModules && project != null)
            DrawEventModules(eventModules, project, context, begin, changed, end);
        if (component is MeshRenderer mesh)
'@

$newAfterModules = @'
        if (component is EventModuleComponent eventModules && project != null)
            DrawEventModules(eventModules, project, context, begin, changed, end);
        if (component is AnimationController animationController && project != null)
            DrawAnimationControllerTools(animationController, project, context, begin, changed, end);
        if (component is MeshRenderer mesh)
'@

$properties = Replace-Once `
    $properties `
    $oldAfterModules `
    $newAfterModules `
    "AnimationController inspector tools hook"

$animationHelpers = @'
    private static bool DrawAnimationClipSelector(
        AnimationController controller,
        EditorProjectContext project,
        string label,
        ref string clipName)
    {
        IReadOnlyList<string> clips =
            AnimationClipDiscovery.GetClipNames(
                controller,
                project);

        string preview =
            string.IsNullOrWhiteSpace(
                clipName)
                ? "None"
                : clips.Any(
                    candidate =>
                        string.Equals(
                            candidate,
                            clipName,
                            StringComparison.OrdinalIgnoreCase))
                    ? clipName
                    : $"{clipName} (Missing)";

        bool changed =
            false;

        if (!ImGui.BeginCombo(
                label,
                preview))
        {
            return false;
        }

        bool noneSelected =
            string.IsNullOrWhiteSpace(
                clipName);

        if (ImGui.Selectable(
                "None",
                noneSelected))
        {
            clipName =
                string.Empty;

            changed =
                true;
        }

        if (clips.Count ==
            0)
        {
            ImGui.TextDisabled(
                "No animation clips found under this character.");
        }
        else
        {
            foreach (string clip
                     in clips)
            {
                bool selected =
                    string.Equals(
                        clipName,
                        clip,
                        StringComparison.OrdinalIgnoreCase);

                if (ImGui.Selectable(
                        clip,
                        selected))
                {
                    clipName =
                        clip;

                    changed =
                        true;
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }
        }

        ImGui.EndCombo();

        return changed;
    }

    private static void DrawAnimationControllerTools(
        AnimationController controller,
        EditorProjectContext project,
        PropertyEditorContext context,
        Action begin,
        Action changed,
        Action end)
    {
        ImGui.SeparatorText(
            "ANIMATION CLIPS");

        if (!AnimationClipDiscovery.TryGetModel(
                controller,
                project,
                out ModelAsset? model,
                out AssetReference modelReference) ||
            model ==
                null)
        {
            ImGui.TextDisabled(
                "No animated model found under this character.");

            return;
        }

        AssetRecord? asset =
            project.AssetDatabase.Resolve(
                modelReference);

        ImGui.TextDisabled(
            $"Source: {Path.GetFileName(asset?.ProjectPath ?? model.Name)}");

        ImGui.TextDisabled(
            $"{model.Animations.Count} clip(s) discovered automatically.");

        if (context ==
            PropertyEditorContext.Runtime)
        {
            return;
        }

        if (ImGui.Button(
                "Auto Assign Locomotion Clips"))
        {
            begin();

            if (AnimationClipDiscovery.AutoAssign(
                    controller,
                    project))
            {
                changed();
            }

            end();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Matches common Idle, Walk, Run, Jump, Fall and Land names from the imported model.");
        }
    }

'@

$properties = Replace-Once `
    $properties `
    "    private static bool IsColorProperty(`n" `
    ($animationHelpers + "    private static bool IsColorProperty(`n") `
    "AnimationController helper methods"

# -------------------------------------------------------------------------
# EditorApplication.cs
# -------------------------------------------------------------------------

$app = Replace-Once `
    $app `
    "        RuntimeDiagnostics.OutputSink = null;`n        _sceneView.Dispose();" `
    "        RuntimeDiagnostics.OutputSink = null;`n        _inspector.Dispose();`n        _sceneView.Dispose();" `
    "EditorApplication inspector disposal"

$inspectorDrawBlock = @'
        if (_inspector.IsOpen)
        {
            _inspector.Draw(
                _state,
                _projectContext!,
                Renderer,
                Renderer3D,
                FramebufferSize.X,
                FramebufferSize.Y,
                reference =>
                {
                    AssetRecord? asset =
                        _projectContext!.AssetDatabase.Resolve(
                            reference);

                    if (asset != null)
                    {
                        _blueprintWorkspace.Open(
                            asset,
                            _projectContext);
                    }
                });
        }

'@

$app = Replace-Between `
    $app `
    "        if (_inspector.IsOpen)" `
    "        if (_sceneView.IsOpen)" `
    $inspectorDrawBlock `
    "EditorApplication Inspector draw call"

# Every transform succeeded. Write the five tracked replacements.
$utf8NoBom =
    New-Object System.Text.UTF8Encoding($false)

[IO.File]::WriteAllText(
    $statePath,
    $state,
    $utf8NoBom
)

[IO.File]::WriteAllText(
    $assetsPath,
    $assets,
    $utf8NoBom
)

[IO.File]::WriteAllText(
    $inspectorPath,
    $inspector,
    $utf8NoBom
)

[IO.File]::WriteAllText(
    $propertiesPath,
    $properties,
    $utf8NoBom
)

[IO.File]::WriteAllText(
    $appPath,
    $app,
    $utf8NoBom
)

Write-Host ""
Write-Host "C4A tracked source files installed successfully." -ForegroundColor Green
Write-Host ""
git -C $RepoRoot status --short

Write-Host ""
Write-Host "Running git diff --check..." -ForegroundColor Cyan
git -C $RepoRoot diff --check

if ($LASTEXITCODE -ne 0) {
    Fail "git diff --check reported an error."
}

Write-Host ""
Write-Host "Installer complete. Run dotnet build ByteEngine.sln next." -ForegroundColor Green
