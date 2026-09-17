param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = [System.IO.Path]::GetFullPath($PSScriptRoot)

$propertyPath = Join-Path $root "Editor\ByteEngine.Editor\ComponentPropertyRenderer.cs"
$serializationPath = Join-Path $root "Engine\ByteEngine.Core\Animation\AnimationSerializationRegistrar.cs"

foreach ($path in @($propertyPath, $serializationPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Could not find expected ByteEngine source file: $path"
    }
}

function Read-Text([string]$Path) {
    [System.IO.File]::ReadAllText($Path)
}

function Write-Text([string]$Path, [string]$Text) {
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Text, $utf8)
}

Write-Host ""
Write-Host "ByteEngine C6.4 - socket cleanup" -ForegroundColor Cyan
Write-Host ""

# ---------------------------------------------------------------------------
# 1) Remove CS8604 by only using the project-dependent bone picker when a
#    project context actually exists.
# ---------------------------------------------------------------------------

$text = Read-Text $propertyPath

if ($text -notmatch 'component is BoneSocket3D boneSocket &&\s*project != null &&') {
    $pattern = 'component is BoneSocket3D boneSocket &&\s*descriptor\.Property\.Name == nameof\(BoneSocket3D\.BoneName\) &&'

    $matches = [regex]::Matches(
        $text,
        $pattern,
        [System.Text.RegularExpressions.RegexOptions]::Singleline)

    if ($matches.Count -ne 1) {
        throw "Could not safely apply BoneSocket project null guard. Expected 1 match, found $($matches.Count)."
    }

    $replacement = @'
component is BoneSocket3D boneSocket &&
                     project != null &&
                     descriptor.Property.Name == nameof(BoneSocket3D.BoneName) &&
'@

    $text = [regex]::Replace(
        $text,
        $pattern,
        $replacement,
        [System.Text.RegularExpressions.RegexOptions]::Singleline)

    Write-Text $propertyPath $text
    Write-Host "Applied: BoneSocket picker project null guard (CS8604 cleanup)." -ForegroundColor Green
}
else {
    Write-Host "Already applied: BoneSocket picker project null guard." -ForegroundColor DarkGreen
}

# ---------------------------------------------------------------------------
# 2) C6.2 changed BoneSocket3D's normal default so imported FBX scale is not
#    inherited automatically. Keep deserialization fallback consistent with
#    that default for scenes/blueprints that do not contain the property yet.
# ---------------------------------------------------------------------------

$text = Read-Text $serializationPath

$old = @'
                InheritBoneScale =
                    data.Properties["inheritBoneScale"]?
                        .GetValue<bool>() ??
                    true
'@

$new = @'
                InheritBoneScale =
                    data.Properties["inheritBoneScale"]?
                        .GetValue<bool>() ??
                    false
'@

if ($text.Contains($old)) {
    $text = $text.Replace($old, $new)
    Write-Text $serializationPath $text
    Write-Host "Applied: BoneSocket deserialization now defaults Inherit Bone Scale to OFF." -ForegroundColor Green
}
elseif ($text.Contains('data.Properties["inheritBoneScale"]') -and
        $text -match 'inheritBoneScale"\]\?\s*\.GetValue<bool>\(\)\s*\?\?\s*false') {
    Write-Host "Already applied: BoneSocket scale inheritance fallback." -ForegroundColor DarkGreen
}
else {
    throw "Could not safely locate BoneSocket InheritBoneScale deserialization block."
}

Write-Host ""
Write-Host "C6.4 applied. Now run:" -ForegroundColor Yellow
Write-Host "powershell -ep bypass -file .\Rebuild-ByteEngine-And-Refresh-Dist.ps1" -ForegroundColor Cyan
Write-Host ""
