$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Push-Location $projectRoot
try {
    dotnet run --project 'Sandbox/ByteEngine.Sandbox/ByteEngine.Sandbox.csproj' -- --goblin-scrapwar
    if ($LASTEXITCODE -ne 0) { throw "Goblin Scrapwar exited with code $LASTEXITCODE." }
}
finally {
    Pop-Location
}
