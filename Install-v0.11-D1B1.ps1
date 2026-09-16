param(
    [string]$RepoRoot = "C:\Users\codex\ByteEngine"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "ByteEngine v0.11-D1B.1 installer: $Message"
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

    $first = $Text.IndexOf($Old, [System.StringComparison]::Ordinal)
    if ($first -lt 0) {
        Fail "Could not find expected source block: $Label"
    }

    $second = $Text.IndexOf($Old, $first + $Old.Length, [System.StringComparison]::Ordinal)
    if ($second -ge 0) {
        Fail "Expected source block was not unique: $Label"
    }

    return $Text.Substring(0, $first) +
           $New +
           $Text.Substring($first + $Old.Length)
}

$assetsPath =
    Join-Path `
        $RepoRoot `
        "Editor/ByteEngine.Editor/Panels/AssetsPanel.cs"

if (!(Test-Path $assetsPath)) {
    Fail "AssetsPanel.cs not found."
}

$assets =
    Normalize (
        [IO.File]::ReadAllText(
            $assetsPath))

# Must already be the user's working D1A state.
foreach ($marker in @(
    "private float BrowserTileScale =>",
    "private float BrowserTileWidth =>",
    "private float BrowserTileHeight =>",
    "private float BrowserIconSize =>",
    "private void DrawAnimationTile(",
    "EditorIcons.ForAsset(",
    "Clips"
)) {
    if (!$assets.Contains($marker)) {
        Fail "Current AssetsPanel.cs does not look like the working D1A state. Missing: $marker"
    }
}

# Backup before touching the file.
$backupPath =
    $assetsPath +
    ".d1b1.backup"

Copy-Item `
    $assetsPath `
    $backupPath `
    -Force

# Continuous slider state.
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
        44.0f *
        BrowserTileScale;
'@

$assets =
    Replace-Once `
        $assets `
        $fieldsOld `
        $fieldsNew `
        "continuous tile-scale fields"

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
            150.0f);

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
        "continuous icon-size slider"

# Replace display name with extension-free registered asset names.
$nameOld = @'
            Path.GetFileName(file),
            EditorIcons.AssetTypeName(asset?.Type),
'@

$nameNew = @'
            GetTileDisplayName(
                file,
                asset?.Type),
            EditorIcons.AssetTypeName(asset?.Type),
'@

$assets =
    Replace-Once `
        $assets `
        $nameOld `
        $nameNew `
        "clean tile display name"

# Replace the full tile visual method by method boundary.
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
    Fail "Could not locate DrawBrowserTileVisual/FitTileText boundary."
}

$visualMethod =
    [IO.File]::ReadAllText(
        (
            Join-Path `
                $RepoRoot `
                "_ByteEngineD1B1\TileVisual.txt"
        ))

$assets =
    $assets.Substring(0, $visualStart) +
    (Normalize $visualMethod) +
    $assets.Substring($visualEnd)

# Add display-name helper before FitTileText.
$fitAnchor =
    "    private static string FitTileText(`n"

$displayHelper = @'
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
        $displayHelper `
        "tile display-name helper"

# Make model footer hit area match the scalable footer row.
$assets =
    $assets.Replace(
        "maximum.Y -`n                    22.0f *`n                    BrowserTileScale;",
        "maximum.Y -`n                    30.0f *`n                    BrowserTileScale;")

$utf8NoBom =
    New-Object System.Text.UTF8Encoding($false)

[IO.File]::WriteAllText(
    $assetsPath,
    $assets,
    $utf8NoBom)

# Do not gate installation on `git diff --check`.
# The existing D1A-generated source can contain harmless whitespace changes
# that make `git diff --check` non-zero even when the C# source is valid.
# Build validation is the authoritative check for this pass.
Remove-Item `
    (Join-Path $RepoRoot "_ByteEngineD1B1") `
    -Recurse `
    -Force

Write-Host ""
Write-Host "v0.11-D1B.1 asset browser polish installed." -ForegroundColor Green
Write-Host "Backup: $backupPath" -ForegroundColor DarkGray
Write-Host ""
Write-Host "Next: dotnet build ByteEngine.sln" -ForegroundColor Cyan
