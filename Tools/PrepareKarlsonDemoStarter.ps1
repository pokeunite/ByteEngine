param(
    [string]$ProjectFile = 'C:/Users/codex/Documents/karlson demo/karlson demo.byteproject'
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$output = Join-Path $repoRoot 'Editor/ByteEngine.Editor/Resources/Starters/KarlsonDemo.bytepak'
Push-Location $repoRoot
try {
    & dotnet run --project Tests/ByteEngine.Tests -c Release -- --prepare-karlson-starter $ProjectFile $output
    if ($LASTEXITCODE -ne 0) { throw 'Karlson Demo template packaging or validation failed.' }
}
finally { Pop-Location }
