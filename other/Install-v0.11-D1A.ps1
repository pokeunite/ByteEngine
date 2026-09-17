param(
    [string]$RepoRoot = "C:\Users\codex\ByteEngine"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    throw "ByteEngine v0.11-D1A installer: $Message"
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
        Fail "Could not find expected block: $Label"
    }

    $second = $Text.IndexOf($Old, $first + $Old.Length, [System.StringComparison]::Ordinal)
    if ($second -ge 0) {
        Fail "Expected block was not unique: $Label"
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
    $Text = Normalize $Text
    $StartMarker = Normalize $StartMarker
    $EndMarker = Normalize $EndMarker
    $Replacement = Normalize $Replacement

    $start = $Text.IndexOf($StartMarker, [System.StringComparison]::Ordinal)
    if ($start -lt 0) {
        Fail "Could not find start marker: $Label"
    }

    $end = $Text.IndexOf(
        $EndMarker,
        $start + $StartMarker.Length,
        [System.StringComparison]::Ordinal)

    if ($end -lt 0) {
        Fail "Could not find end marker: $Label"
    }

    return $Text.Substring(0, $start) +
           $Replacement +
           $Text.Substring($end)
}

function Assert-CleanTrackedFile(
    [string]$RelativePath,
    [string]$ExpectedBlob
) {
    git -C $RepoRoot diff --quiet -- $RelativePath
    if ($LASTEXITCODE -ne 0) {
        Fail "$RelativePath has local unstaged changes."
    }

    git -C $RepoRoot diff --cached --quiet -- $RelativePath
    if ($LASTEXITCODE -ne 0) {
        Fail "$RelativePath has staged changes."
    }

    $actual = (git -C $RepoRoot rev-parse "HEAD:$RelativePath").Trim()
    if ($actual -ne $ExpectedBlob) {
        Fail "$RelativePath does not match the v0.11-D1A baseline. Expected $ExpectedBlob, got $actual."
    }
}

$expectedHead = "1f36804a258f96324c155d88e17793f01c8ec15e"
$head = (git -C $RepoRoot rev-parse HEAD).Trim()

if ($head -ne $expectedHead) {
    Fail "This package was built for $expectedHead but current HEAD is $head."
}

$imguiRelative = "Editor/ByteEngine.Editor/ImGuiController.cs"
$assetsRelative = "Editor/ByteEngine.Editor/Panels/AssetsPanel.cs"

Assert-CleanTrackedFile $imguiRelative "de7e9f41dc305b26431ee8b0a76d3c21fa575bb1"
Assert-CleanTrackedFile $assetsRelative "5f6fa211c34e45abb7dd3be303d5bd011b19fe18"

$imguiPath = Join-Path $RepoRoot $imguiRelative
$assetsPath = Join-Path $RepoRoot $assetsRelative
$supportRoot = Join-Path $RepoRoot "_ByteEngineD1"

$configureStyle = [IO.File]::ReadAllText((Join-Path $supportRoot "ConfigureStyle.txt"))
$toolbar = [IO.File]::ReadAllText((Join-Path $supportRoot "Toolbar.txt"))
$browserBlock = [IO.File]::ReadAllText((Join-Path $supportRoot "BrowserBlock.txt"))

$imgui = [IO.File]::ReadAllText($imguiPath)
$assets = [IO.File]::ReadAllText($assetsPath)

$imgui = Replace-Between `
    $imgui `
    "    private static void ConfigureStyle()" `
    "    private unsafe void CreateDeviceResources()" `
    $configureStyle `
    "ImGuiController.ConfigureStyle"

$assets = Replace-Once `
    $assets `
    "using ByteEngine.Core.Assets;`nusing ByteEngine.Core.Blueprints;" `
    "using ByteEngine.Core.Assets;`nusing ByteEngine.Core.Assets.Importers;`nusing ByteEngine.Core.Blueprints;" `
    "AssetsPanel importer using"

$fieldsOld = @'
    private bool _focusNextDraw =
        true;

    public bool IsOpen { get; set; } =
'@

$fieldsNew = @'
    private bool _focusNextDraw =
        true;

    private bool _gridView =
        true;

    private string _assetSearch =
        string.Empty;

    private string? _expandedModelPath;

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

    public bool IsOpen { get; set; } =
'@

$assets = Replace-Once `
    $assets `
    $fieldsOld `
    $fieldsNew `
    "AssetsPanel browser state"

$assets = Replace-Between `
    $assets `
    "    private void DrawToolbar(" `
    "    // ========================================================`n    // FOLDER TREE" `
    $toolbar `
    "AssetsPanel toolbar"

$assets = $assets.Replace(
    "        ImGui.TextDisabled(`n            `"PROJECT`");",
    "        ImGui.TextDisabled(`n            `"FILESYSTEM`");")

$assets = Replace-Between `
    $assets `
    "    private void DrawCurrentFolder(" `
    "    private void DrawDirectories(" `
    $browserBlock `
    "AssetsPanel content browser"

$audioOld = @'
            AssetType.Texture2D =>
                "[IMG]",

            AssetType.Scene =>
'@

$audioNew = @'
            AssetType.Texture2D =>
                "[IMG]",

            AssetType.AudioClip =>
                "[AUD]",

            AssetType.Scene =>
'@

$assets = Replace-Once `
    $assets `
    $audioOld `
    $audioNew `
    "AssetsPanel audio icon"

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

[IO.File]::WriteAllText($imguiPath, $imgui, $utf8NoBom)
[IO.File]::WriteAllText($assetsPath, $assets, $utf8NoBom)

Write-Host ""
Write-Host "v0.11-D1A readability pass installed." -ForegroundColor Green
Write-Host ""

git -C $RepoRoot diff --check
if ($LASTEXITCODE -ne 0) {
    Fail "git diff --check reported an error."
}

Remove-Item $supportRoot -Recurse -Force

Write-Host ""
git -C $RepoRoot status --short
Write-Host ""
Write-Host "Next: dotnet build ByteEngine.sln" -ForegroundColor Cyan
