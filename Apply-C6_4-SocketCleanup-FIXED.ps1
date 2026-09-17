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
Write-Host "ByteEngine C6.4 FIXED - socket cleanup" -ForegroundColor Cyan
Write-Host ""

# ---------------------------------------------------------------------------
# 1) BoneSocket picker nullability guard.
#    The previous C6.4 script may already have applied this before failing,
#    so this step is idempotent.
# ---------------------------------------------------------------------------

$text = Read-Text $propertyPath

if ($text -match 'component is BoneSocket3D boneSocket &&\s*project != null &&') {
    Write-Host "Already applied: BoneSocket picker project null guard." -ForegroundColor DarkGreen
}
else {
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

# ---------------------------------------------------------------------------
# 2) Keep deserialization fallback aligned with BoneSocket3D C6.2 default:
#    InheritBoneScale defaults OFF when older data does not contain the field.
# ---------------------------------------------------------------------------

$text = Read-Text $serializationPath

# If already false, nothing to do.
$alreadyFalsePattern = 'data\.Properties\["inheritBoneScale"\]\?\s*\.GetValue<bool>\(\)\s*\?\?\s*false'

if ([regex]::IsMatch(
        $text,
        $alreadyFalsePattern,
        [System.Text.RegularExpressions.RegexOptions]::Singleline)) {
    Write-Host "Already applied: BoneSocket Inherit Bone Scale fallback defaults OFF." -ForegroundColor DarkGreen
}
else {
    # Match only the BoneSocket3D fallback expression, regardless of whitespace.
    $pattern = 'data\.Properties\["inheritBoneScale"\]\?\s*\.GetValue<bool>\(\)\s*\?\?\s*true'

    $matches = [regex]::Matches(
        $text,
        $pattern,
        [System.Text.RegularExpressions.RegexOptions]::Singleline)

    if ($matches.Count -ne 1) {
        throw "Could not safely locate BoneSocket InheritBoneScale fallback. Expected 1 match, found $($matches.Count)."
    }

    $replacement = @'
data.Properties["inheritBoneScale"]?
                        .GetValue<bool>() ??
                    false
'@

    $text = [regex]::Replace(
        $text,
        $pattern,
        $replacement,
        [System.Text.RegularExpressions.RegexOptions]::Singleline)

    Write-Text $serializationPath $text
    Write-Host "Applied: BoneSocket Inherit Bone Scale fallback now defaults OFF." -ForegroundColor Green
}

Write-Host ""
Write-Host "C6.4 FIXED applied successfully." -ForegroundColor Green
Write-Host ""
Write-Host "Now run:" -ForegroundColor Yellow
Write-Host "powershell -ep bypass -file .\Rebuild-ByteEngine-And-Refresh-Dist.ps1" -ForegroundColor Cyan
Write-Host ""
