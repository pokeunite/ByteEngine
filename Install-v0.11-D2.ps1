param(
    [string]$RepoRoot = "C:\Users\codex\ByteEngine"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "ByteEngine v0.11-D2 installer: $Message"
}

function Normalize([string]$Text) {
    return $Text.Replace("`r`n", "`n")
}

function Replace-Once(
    [string]$Text,
    [string]$Old,
    [string]$New,
    [string]$Label
) {
    $Text = Normalize $Text
    $Old = Normalize $Old
    $New = Normalize $New

    $first =
        $Text.IndexOf(
            $Old,
            [System.StringComparison]::Ordinal)

    if ($first -lt 0) {
        Fail "Could not find expected source block: $Label"
    }

    $second =
        $Text.IndexOf(
            $Old,
            $first + $Old.Length,
            [System.StringComparison]::Ordinal)

    if ($second -ge 0) {
        Fail "Expected source block was not unique: $Label"
    }

    return
        $Text.Substring(0, $first) +
        $New +
        $Text.Substring($first + $Old.Length)
}

$hierarchyPath =
    Join-Path `
        $RepoRoot `
        "Editor/ByteEngine.Editor/Panels/HierarchyPanel.cs"

$inspectorPath =
    Join-Path `
        $RepoRoot `
        "Editor/ByteEngine.Editor/Panels/InspectorPanel.cs"

foreach ($path in @(
    $hierarchyPath,
    $inspectorPath
)) {
    if (!(Test-Path $path)) {
        Fail "Missing source file: $path"
    }
}

$hierarchy =
    Normalize (
        [IO.File]::ReadAllText(
            $hierarchyPath))

$inspector =
    Normalize (
        [IO.File]::ReadAllText(
            $inspectorPath))

# This pass intentionally assumes the hierarchy/inspector are still the
# stable baseline versions. D1/D1B only changed theme/font/assets.
foreach ($marker in @(
    'ImGui.Begin("Hierarchy", ref isOpen);',
    'private void DrawNode(GameObject gameObject',
    'ImGui.Begin("Inspector", ref isOpen);',
    'ImGui.InputTextWithHint("##InspectorSearch"',
    'ImGui.Button("Add Component")'
)) {
    if (
        !$hierarchy.Contains($marker) -and
        !$inspector.Contains($marker)
    ) {
        Fail "Expected stable editor marker not found: $marker"
    }
}

Copy-Item `
    $hierarchyPath `
    ($hierarchyPath + ".d2.backup") `
    -Force

Copy-Item `
    $inspectorPath `
    ($inspectorPath + ".d2.backup") `
    -Force

# ----------------------------------------------------------------------
# HIERARCHY
# ----------------------------------------------------------------------

$hierarchyHeaderOld = @'
        ImGui.Begin("Hierarchy", ref isOpen);
        IsOpen = isOpen;
        ImGui.TextDisabled(state.DisplayedScene.Name);
        ImGui.SameLine();
        ImGui.TextColored(state.Mode == EditorMode.Edit ? new Vector4(.4f, .8f, 1f, 1f) : new Vector4(.4f, 1f, .5f, 1f), state.Mode.ToString());
        ImGui.Separator();
'@

$hierarchyHeaderNew = @'
        ImGui.Begin("Hierarchy", ref isOpen);
        IsOpen = isOpen;

        ImGui.PushStyleVar(
            ImGuiStyleVar.ChildRounding,
            4.0f);

        ImGui.PushStyleColor(
            ImGuiCol.ChildBg,
            new Vector4(
                0.105f,
                0.12f,
                0.15f,
                1.0f));

        if (ImGui.BeginChild(
                "##HierarchyHeader",
                new Vector2(
                    0.0f,
                    48.0f),
                true,
                ImGuiWindowFlags.NoScrollbar))
        {
            ImGui.TextUnformatted(
                state.DisplayedScene.Name);

            ImGui.SameLine();

            ImGui.TextColored(
                state.Mode ==
                    EditorMode.Edit
                    ? new Vector4(
                        .4f,
                        .8f,
                        1f,
                        1f)
                    : new Vector4(
                        .4f,
                        1f,
                        .5f,
                        1f),
                state.Mode.ToString());

            ImGui.SameLine();

            ImGui.TextDisabled(
                $"  {state.DisplayedScene.GameObjects.Count} objects");

            if (state.Mode ==
                    EditorMode.Edit)
            {
                float buttonWidth =
                    28.0f;

                ImGui.SameLine(
                    Math.Max(
                        ImGui.GetWindowContentRegionMax().X -
                        buttonWidth,
                        ImGui.GetCursorPosX()));

                if (ImGui.Button(
                        "+",
                        new Vector2(
                            buttonWidth,
                            0.0f)))
                {
                    createObject();
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(
                        "Create Empty GameObject");
                }
            }
        }

        ImGui.EndChild();
        ImGui.PopStyleColor();
        ImGui.PopStyleVar();

        ImGui.Spacing();
'@

$hierarchy =
    Replace-Once `
        $hierarchy `
        $hierarchyHeaderOld `
        $hierarchyHeaderNew `
        "Hierarchy header card"

$treeOld = @'
        ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanFullWidth;
        if (gameObject.Children.Count == 0) flags |= ImGuiTreeNodeFlags.Leaf;
        if (state.Selection.Contains(gameObject)) flags |= ImGuiTreeNodeFlags.Selected;
        bool open = ImGui.TreeNodeEx($"{gameObject.Name}##{gameObject.Id}", flags);
'@

$treeNew = @'
        ImGuiTreeNodeFlags flags =
            ImGuiTreeNodeFlags.OpenOnArrow |
            ImGuiTreeNodeFlags.SpanFullWidth |
            ImGuiTreeNodeFlags.FramePadding;

        if (gameObject.Children.Count == 0)
        {
            flags |=
                ImGuiTreeNodeFlags.Leaf;
        }

        if (state.Selection.Contains(
                gameObject))
        {
            flags |=
                ImGuiTreeNodeFlags.Selected;
        }

        ImGui.PushStyleVar(
            ImGuiStyleVar.FramePadding,
            new Vector2(
                5.0f,
                4.0f));

        bool open =
            ImGui.TreeNodeEx(
                $"  {gameObject.Name}##{gameObject.Id}",
                flags);

        Vector2 rowMinimum =
            ImGui.GetItemRectMin();

        float markerY =
            (
                rowMinimum.Y +
                ImGui.GetItemRectMax().Y
            ) *
            0.5f;

        ImGui.GetWindowDrawList()
            .AddCircleFilled(
                new Vector2(
                    rowMinimum.X +
                    18.0f,
                    markerY),
                3.0f,
                ImGui.GetColorU32(
                    gameObject.Active
                        ? new Vector4(
                            0.38f,
                            0.72f,
                            1.0f,
                            1.0f)
                        : new Vector4(
                            0.42f,
                            0.45f,
                            0.50f,
                            1.0f)));

        ImGui.PopStyleVar();
'@

$hierarchy =
    Replace-Once `
        $hierarchy `
        $treeOld `
        $treeNew `
        "Hierarchy object rows"

# ----------------------------------------------------------------------
# INSPECTOR
# ----------------------------------------------------------------------

$searchBlockOld = @'
        ImGui.SetNextItemWidth(-38f);
        ImGui.InputTextWithHint("##InspectorSearch", "Search properties...", ref _search, 128);
        ImGui.SameLine();
        if (ImGui.Button("...")) ImGui.OpenPopup("Inspector Options");
'@

$searchBlockNew = @'
        ImGui.PushStyleColor(
            ImGuiCol.ChildBg,
            new Vector4(
                0.105f,
                0.12f,
                0.15f,
                1.0f));

        ImGui.PushStyleVar(
            ImGuiStyleVar.ChildRounding,
            4.0f);

        if (ImGui.BeginChild(
                "##InspectorObjectHeader",
                new Vector2(
                    0.0f,
                    82.0f),
                true,
                ImGuiWindowFlags.NoScrollbar))
        {
            ImGui.TextColored(
                new Vector4(
                    0.55f,
                    0.78f,
                    1.0f,
                    1.0f),
                "GAME OBJECT");

            ImGui.SameLine();
            ImGui.TextDisabled(
                state.Mode.ToString());

            ImGui.SetNextItemWidth(
                -1.0f);

            string headerName =
                selected.Name;

            bool headerNameChanged =
                ImGui.InputText(
                    "##InspectorHeaderName",
                    ref headerName,
                    256);

            string nextHeaderName =
                string.IsNullOrWhiteSpace(
                    headerName)
                    ? "GameObject"
                    : headerName;

            if (headerNameChanged)
            {
                string previousName =
                    selected.Name;

                selected.Name =
                    nextHeaderName;

                TrackItem(
                    state,
                    "Rename GameObject",
                    true,
                    () =>
                        selected.Name =
                            previousName,
                    () =>
                        selected.Name =
                            nextHeaderName);
            }
        }

        ImGui.EndChild();
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();

        ImGui.Spacing();

        ImGui.SetNextItemWidth(
            -42.0f);

        ImGui.InputTextWithHint(
            "##InspectorSearch",
            "Search properties...",
            ref _search,
            128);

        ImGui.SameLine();

        if (ImGui.Button(
                "..."))
        {
            ImGui.OpenPopup(
                "Inspector Options");
        }
'@

$inspector =
    Replace-Once `
        $inspector `
        $searchBlockOld `
        $searchBlockNew `
        "Inspector object header/search"

# Remove the old duplicate Name field because the new header owns renaming.
$nameBlockOld = @'
        string oldName = selected.Name;
        string name = oldName;
        bool nameChanged = ImGui.InputText("Name", ref name, 256);
        string nextName = string.IsNullOrWhiteSpace(name) ? "GameObject" : name;
        if (nameChanged) selected.Name = nextName;
        TrackItem(state, "Rename GameObject", nameChanged, () => selected.Name = oldName, () => selected.Name = nextName);

'@

$inspector =
    Replace-Once `
        $inspector `
        $nameBlockOld `
        "" `
        "remove duplicate Inspector Name field"

$transformOld = @'
        if (showTransform && ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen))
        {
'@

$transformNew = @'
        ImGui.PushStyleColor(
            ImGuiCol.Header,
            new Vector4(
                0.14f,
                0.17f,
                0.21f,
                1.0f));

        ImGui.PushStyleColor(
            ImGuiCol.HeaderHovered,
            new Vector4(
                0.18f,
                0.22f,
                0.28f,
                1.0f));

        ImGui.PushStyleColor(
            ImGuiCol.HeaderActive,
            new Vector4(
                0.20f,
                0.30f,
                0.40f,
                1.0f));

        bool transformOpen =
            showTransform &&
            ImGui.CollapsingHeader(
                "Transform",
                ImGuiTreeNodeFlags.DefaultOpen);

        ImGui.PopStyleColor(3);

        if (transformOpen)
        {
'@

$inspector =
    Replace-Once `
        $inspector `
        $transformOld `
        $transformNew `
        "Transform component header"

$componentOld = @'
            ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.None;
            if (!ImGui.CollapsingHeader($"{metadata.DisplayName}##{component.GetHashCode()}", flags)) continue;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(metadata.Description);
'@

$componentNew = @'
            ImGuiTreeNodeFlags flags =
                ImGuiTreeNodeFlags.None;

            ImGui.PushStyleColor(
                ImGuiCol.Header,
                new Vector4(
                    0.14f,
                    0.17f,
                    0.21f,
                    1.0f));

            ImGui.PushStyleColor(
                ImGuiCol.HeaderHovered,
                new Vector4(
                    0.18f,
                    0.22f,
                    0.28f,
                    1.0f));

            ImGui.PushStyleColor(
                ImGuiCol.HeaderActive,
                new Vector4(
                    0.20f,
                    0.30f,
                    0.40f,
                    1.0f));

            bool componentOpen =
                ImGui.CollapsingHeader(
                    $"{metadata.DisplayName}##{component.GetHashCode()}",
                    flags);

            bool componentHeaderHovered =
                ImGui.IsItemHovered();

            ImGui.PopStyleColor(3);

            if (componentHeaderHovered)
            {
                ImGui.SetTooltip(
                    metadata.Description);
            }

            if (!componentOpen)
            {
                continue;
            }
'@

$inspector =
    Replace-Once `
        $inspector `
        $componentOld `
        $componentNew `
        "Inspector component cards"

$addOld = @'
        ImGui.Separator();
        ImGui.BeginDisabled(readOnly);
        if (ImGui.Button("Add Component")) ImGui.OpenPopup("Add Component Popup");
'@

$addNew = @'
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.BeginDisabled(readOnly);

        if (ImGui.Button(
                "+ Add Component",
                new Vector2(
                    -1.0f,
                    34.0f)))
        {
            ImGui.OpenPopup(
                "Add Component Popup");
        }
'@

$inspector =
    Replace-Once `
        $inspector `
        $addOld `
        $addNew `
        "Inspector Add Component button"

$utf8NoBom =
    New-Object System.Text.UTF8Encoding($false)

[IO.File]::WriteAllText(
    $hierarchyPath,
    $hierarchy,
    $utf8NoBom)

[IO.File]::WriteAllText(
    $inspectorPath,
    $inspector,
    $utf8NoBom)

Write-Host ""
Write-Host "v0.11-D2 Hierarchy + Inspector visual pass installed." -ForegroundColor Green
Write-Host ""
Write-Host "Backups:" -ForegroundColor DarkGray
Write-Host "  $hierarchyPath.d2.backup" -ForegroundColor DarkGray
Write-Host "  $inspectorPath.d2.backup" -ForegroundColor DarkGray
Write-Host ""
Write-Host "Next: dotnet build ByteEngine.sln" -ForegroundColor Cyan
