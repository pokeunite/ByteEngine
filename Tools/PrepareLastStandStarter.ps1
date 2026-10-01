param(
    [Parameter(Mandatory = $true)][string]$ProjectFile
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$output = Join-Path $repoRoot 'Editor\ByteEngine.Editor\Resources\Starters\LastStand.bytepak'
Push-Location $repoRoot
try {
    & dotnet run --project Tests/ByteEngine.Tests -c Release -- --prepare-last-stand-starter $ProjectFile $output
    if ($LASTEXITCODE -ne 0) { throw 'Last Stand starter packaging or validation failed.' }
}
finally { Pop-Location }
