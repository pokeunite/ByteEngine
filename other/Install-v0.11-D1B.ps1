param(
    [string]$RepoRoot = "C:\Users\codex\ByteEngine"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "ByteEngine v0.11-D1B installer: $Message"
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
        Fail "Could not find expected D1A source block: $Label"
    }

    $second =
        $Text.IndexOf(
            $Old,
            $first + $Old.Length,
            [System.StringComparison]::Ordinal)

    if ($second -ge 0) {
        Fail "Expected D1A source block was not unique: $Label"
    }

    return
        $Text.Substring(0, $first) +
        $New +
        $Text.Substring($first + $Old.Length)
}

$expectedHead =
    "1f36804a258f96324c155d88e17793f01c8ec15e"

$head =
    (
        git -C $RepoRoot rev-parse HEAD
    ).Trim()

if ($head -ne $expectedHead) {
    Fail "Expected Git HEAD $expectedHead but found $head. D1B will not patch an unknown source state."
}

$imguiPath =
    Join-Path `
        $RepoRoot `
        "Editor/ByteEngine.Editor/ImGuiController.cs"

$assetsPath =
    Join-Path `
        $RepoRoot `
        "Editor/ByteEngine.Editor/Panels/AssetsPanel.cs"

$imgui =
    Normalize (
        [IO.File]::ReadAllText(
            $imguiPath))

$assets =
    Normalize (
        [IO.File]::ReadAllText(
            $assetsPath))

# Validate that this is the tested D1A state before changing anything.
foreach ($marker in @(
    "EditorTheme.ApplyGodotInspired();",
    "private float BrowserTileScale =>",
    "private void DrawAnimationTile(",
    "LMB Orbit"
)) {
    # LMB Orbit belongs to another file and is not expected here.
    if ($marker -eq "LMB Orbit") {
        continue
    }

    if (
        !$imgui.Contains($marker) -and
        !$assets.Contains($marker)
    ) {
        Fail "Current files do not look like the tested D1A state. Missing marker: $marker"
    }
}

# ----------------------------------------------------------------------
# ImGuiController.cs — use an actual Windows UI font at a real pixel size.
# ----------------------------------------------------------------------

$fontHookOld = @'
        ImGuiIOPtr io =
            ImGui.GetIO();

        io.Fonts.GetTexDataAsRGBA32(
'@

$fontHookNew = @'
        ImGuiIOPtr io =
            ImGui.GetIO();

        ConfigureEditorFont(
            io);

        io.Fonts.GetTexDataAsRGBA32(
'@

$imgui =
    Replace-Once `
        $imgui `
        $fontHookOld `
        $fontHookNew `
        "ImGui font atlas hook"

$fontMethodAnchor = @'
    private unsafe void CreateDeviceResources()
'@

$fontMethod = @'
    private static void ConfigureEditorFont(
        ImGuiIOPtr io)
    {
        /*
         * Use the native Windows UI font instead of scaling ImGui's tiny
         * bitmap-looking default. ByteEngine does not ship or redistribute
         * the font file; it simply uses the copy already installed by Windows.
         */
        string fontsDirectory =
            Environment.GetFolderPath(
                Environment.SpecialFolder.Fonts);

        string segoeUi =
            Path.Combine(
                fontsDirectory,
                "segoeui.ttf");

        if (File.Exists(
                segoeUi))
        {
            io.Fonts.AddFontFromFileTTF(
                segoeUi,
                18.0f);
        }
        else
        {
            /*
             * Safe fallback for unusual Windows installations. The editor
             * still starts even if Segoe UI cannot be found.
             */
            io.Fonts.AddFontDefault();
        }

        io.FontGlobalScale =
            1.0f;
    }

    private unsafe void CreateDeviceResources()
'@

$imgui =
    Replace-Once `
        $imgui `
        $fontMethodAnchor `
        $fontMethod `
        "Segoe UI font setup"

# ----------------------------------------------------------------------
# AssetsPanel.cs — replace S/M/L presets with a continuous slider and make
# tile geometry depend on actual font metrics so labels cannot collide.
# ----------------------------------------------------------------------

$fieldsOld = @'
    private int _assetTileScaleIndex =
        1;

    private float BrowserTileScale =>
        _assetTileScaleIndex switch
        {
            0 => 0.85f,
            2 => 1.20f,
            _ => 1.0f
        };

    private float BrowserTileWidth =>
        142.0f *
        BrowserTileScale;

    private float BrowserTileHeight =>
        132.0f *
        BrowserTileScale;

    private float BrowserIconSize =>
        48.0f *
        BrowserTileScale;
'@

$fieldsNew = @'
    private float _assetTileScale =
        1.0f;

    private float BrowserTileScale =>
        Math.Clamp(
            _assetTileScale,
            0.75f,
            1.65f);

    private float BrowserIconSize =>
        48.0f *
        BrowserTileScale;

    private float BrowserTileWidth =>
        158.0f *
        BrowserTileScale;

    private float BrowserTileHeight =>
        BrowserIconSize +
        ImGui.GetTextLineHeight() *
        3.0f +
        42.0f *
        BrowserTileScale;
'@

$assets =
    Replace-Once `
        $assets `
        $fieldsOld `
        $fieldsNew `
        "continuous tile scale fields"

$toolbarOld = @'
        ImGui.SameLine();
        ImGui.TextDisabled("Tiles");
        ImGui.SameLine();

        if (ImGui.SmallButton(
                _assetTileScaleIndex == 0
                    ? "S*"
                    : "S"))
        {
            _assetTileScaleIndex = 0;
        }

        ImGui.SameLine();

        if (ImGui.SmallButton(
                _assetTileScaleIndex == 1
                    ? "M*"
                    : "M"))
        {
            _assetTileScaleIndex = 1;
        }

        ImGui.SameLine();

        if (ImGui.SmallButton(
                _assetTileScaleIndex == 2
                    ? "L*"
                    : "L"))
        {
            _assetTileScaleIndex = 2;
        }

        ImGui.TextDisabled("PATH");
'@

$toolbarNew = @'
        ImGui.SameLine();
        ImGui.TextDisabled("Icon Size");
        ImGui.SameLine();

        ImGui.SetNextItemWidth(
            125.0f);

        ImGui.SliderFloat(
            "##AssetIconSize",
            ref _assetTileScale,
            0.75f,
            1.65f,
            "%.2fx",
            ImGuiSliderFlags.AlwaysClamp);

        ImGui.TextDisabled("PATH");
'@

$assets =
    Replace-Once `
        $assets `
        $toolbarOld `
        $toolbarNew `
        "asset icon-size slider"

$assetNameOld = @'
            Path.GetFileName(file),
            EditorIcons.AssetTypeName(asset?.Type),
'@

$assetNameNew = @'
            GetTileDisplayName(
                file,
                asset?.Type),
            EditorIcons.AssetTypeName(asset?.Type),
'@

$assets =
    Replace-Once `
        $assets `
        $assetNameOld `
        $assetNameNew `
        "clean asset display name"

# Replace the complete tile visual function using stable method boundaries.
$visualStart =
    $assets.IndexOf(
        "    private void DrawBrowserTileVisual(",
        [System.StringComparison]::Ordinal)

$visualEnd =
    $assets.IndexOf(
        "    private static string FitTileText(",
        $visualStart,
        [System.StringComparison]::Ordinal)

if ($visualStart -lt 0 -or $visualEnd -lt 0) {
    Fail "Could not locate D1A tile visual method."
}

$visualMethod = @'
    private void DrawBrowserTileVisual(
        Vector2 minimum,
        Vector2 maximum,
        EditorIconKind icon,
        string name,
        string typeName,
        bool selected,
        bool hovered,
        string? footer = null,
        bool showExpandArrow = false,
        bool expanded = false)
    {
        var drawList =
            ImGui.GetWindowDrawList();

        float scale =
            BrowserTileScale;

        float lineHeight =
            ImGui.GetTextLineHeight();

        Vector4 background =
            selected
                ? new Vector4(
                    EditorTheme.Accent.X,
                    EditorTheme.Accent.Y,
                    EditorTheme.Accent.Z,
                    0.28f)
                : hovered
                    ? EditorTheme.PanelHovered
                    : EditorTheme.PanelRaised;

        drawList.AddRectFilled(
            minimum,
            maximum,
            ImGui.GetColorU32(
                background),
            4.0f);

        drawList.AddRect(
            minimum,
            maximum,
            ImGui.GetColorU32(
                selected
                    ? EditorTheme.Accent
                    : EditorTheme.Border),
            4.0f);

        float iconSize =
            BrowserIconSize;

        float iconTop =
            minimum.Y +
            10.0f *
            scale;

        EditorIcons.DrawTileIcon(
            icon,
            new Vector2(
                minimum.X +
                (
                    BrowserTileWidth -
                    iconSize
                ) *
                0.5f,
                iconTop),
            iconSize);

        float textWidth =
            BrowserTileWidth -
            18.0f *
            scale;

        float nameY =
            iconTop +
            iconSize +
            9.0f *
            scale;

        string displayName =
            FitTileText(
                name,
                textWidth);

        Vector2 nameSize =
            ImGui.CalcTextSize(
                displayName);

        drawList.AddText(
            new Vector2(
                minimum.X +
                Math.Max(
                    (
                        BrowserTileWidth -
                        nameSize.X
                    ) *
                    0.5f,
                    5.0f),
                nameY),
            ImGui.GetColorU32(
                EditorTheme.Text),
            displayName);

        float typeY =
            nameY +
            lineHeight +
            4.0f *
            scale;

        string displayType =
            FitTileText(
                typeName,
                textWidth);

        Vector2 typeSize =
            ImGui.CalcTextSize(
                displayType);

        drawList.AddText(
            new Vector2(
                minimum.X +
                Math.Max(
                    (
                        BrowserTileWidth -
                        typeSize.X
                    ) *
                    0.5f,
                    5.0f),
                typeY),
            ImGui.GetColorU32(
                EditorTheme.TextMuted),
            displayType);

        if (string.IsNullOrWhiteSpace(
                footer))
        {
            return;
        }

        float footerTop =
            typeY +
            lineHeight +
            4.0f *
            scale;

        uint footerBackground =
            ImGui.GetColorU32(
                new Vector4(
                    0.08f,
                    0.095f,
                    0.12f,
                    0.72f));

        drawList.AddRectFilled(
            new Vector2(
                minimum.X +
                5.0f *
                scale,
                footerTop),
            new Vector2(
                maximum.X -
                5.0f *
                scale,
                maximum.Y -
                6.0f *
                scale),
            footerBackground,
            3.0f);

        if (showExpandArrow)
        {
            float arrowSize =
                5.5f *
                scale;

            Vector2 arrowCenter =
                new(
                    minimum.X +
                    18.0f *
                    scale,
                    footerTop +
                    lineHeight *
                    0.55f);

            uint animationColor =
                ImGui.GetColorU32(
                    EditorIcons.Color(
                        EditorIconKind.Animation));

            if (expanded)
            {
                drawList.AddTriangleFilled(
                    new Vector2(
                        arrowCenter.X -
                        arrowSize,
                        arrowCenter.Y -
                        arrowSize *
                        0.55f),
                    new Vector2(
                        arrowCenter.X +
                        arrowSize,
                        arrowCenter.Y -
                        arrowSize *
                        0.55f),
                    new Vector2(
                        arrowCenter.X,
                        arrowCenter.Y +
                        arrowSize),
                    animationColor);
            }
            else
            {
                drawList.AddTriangleFilled(
                    new Vector2(
                        arrowCenter.X -
                        arrowSize *
                        0.55f,
                        arrowCenter.Y -
                        arrowSize),
                    new Vector2(
                        arrowCenter.X -
                        arrowSize *
                        0.55f,
                        arrowCenter.Y +
                        arrowSize),
                    new Vector2(
                        arrowCenter.X +
                        arrowSize,
                        arrowCenter.Y),
                    animationColor);
            }

            drawList.AddText(
                new Vector2(
                    minimum.X +
                    31.0f *
                    scale,
                    footerTop),
                animationColor,
                footer);

            return;
        }

        string displayFooter =
            FitTileText(
                footer,
                textWidth);

        Vector2 footerSize =
            ImGui.CalcTextSize(
                displayFooter);

        drawList.AddText(
            new Vector2(
                minimum.X +
                Math.Max(
                    (
                        BrowserTileWidth -
                        footerSize.X
                    ) *
                    0.5f,
                    5.0f),
                footerTop),
            ImGui.GetColorU32(
                EditorTheme.TextMuted),
            displayFooter);
    }

'@

$assets =
    $assets.Substring(
        0,
        $visualStart) +
    $visualMethod +
    $assets.Substring(
        $visualEnd)

$fitAnchor = @'
    private static string FitTileText(
'@

$displayNameMethod = @'
    private static string GetTileDisplayName(
        string file,
        AssetType? type)
    {
        if (type ==
                null ||
            type ==
                AssetType.Unknown)
        {
            return
                Path.GetFileName(
                    file);
        }

        /*
         * The type is already displayed on its own line. Repeating long
         * extensions such as .byteblueprint wastes the name area and was one
         * of the reasons cards looked cramped.
         */
        return
            Path.GetFileNameWithoutExtension(
                file);
    }

    private static string FitTileText(
'@

$assets =
    Replace-Once `
        $assets `
        $fitAnchor `
        $displayNameMethod `
        "asset display-name helper"

# All transforms succeeded in memory. Only now write tracked files.
$utf8NoBom =
    New-Object System.Text.UTF8Encoding($false)

[IO.File]::WriteAllText(
    $imguiPath,
    $imgui,
    $utf8NoBom)

[IO.File]::WriteAllText(
    $assetsPath,
    $assets,
    $utf8NoBom)

# Remove old local installer scripts so the repo is not littered with stale
# versions. These were never committed engine source.
foreach ($stale in @(
    "Install-v0.11-D1.ps1",
    "Install-v0.11-D1A.ps1"
)) {
    $stalePath =
        Join-Path `
            $RepoRoot `
            $stale

    if (Test-Path(
            $stalePath))
    {
        Remove-Item `
            $stalePath `
            -Force
    }
}

git -C $RepoRoot diff --check

if ($LASTEXITCODE -ne 0) {
    Fail "git diff --check reported an error."
}

Write-Host ""
Write-Host "v0.11-D1B typography + asset browser polish installed." -ForegroundColor Green
Write-Host ""
git -C $RepoRoot status --short
Write-Host ""
Write-Host "Next: dotnet build ByteEngine.sln" -ForegroundColor Cyan
