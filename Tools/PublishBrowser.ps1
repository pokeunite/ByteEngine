param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
& dotnet publish (Join-Path $repoRoot 'Player/ByteEngine.Browser/ByteEngine.Browser.csproj') -c $Configuration
if ($LASTEXITCODE -ne 0) { throw 'Browser player publish failed. Install the .NET 9 wasm-tools workload.' }
$source = Join-Path $repoRoot "Player/ByteEngine.Browser/bin/$Configuration/net9.0/publish/wwwroot"
[IO.File]::WriteAllText((Join-Path $source 'browser-plugins.json'),'["bytebard.desertterrain","bytebard.dunecompany"]')
foreach ($file in @('index.html','main.js','renderer.js','audio.js','content.js','_framework/dotnet.js','Resources/Fonts/TypeLightSans.ttf')) {
    if (!(Test-Path -LiteralPath (Join-Path $source $file))) { throw "Browser runtime missing: $file" }
}
foreach ($target in @((Join-Path $repoRoot 'Dist/ByteEngine/BrowserRuntime'),
    (Join-Path $repoRoot 'Editor/ByteEngine.Editor/bin/Debug/net9.0-windows/win-x64/BrowserRuntime'))) {
    $resolved = [IO.Path]::GetFullPath($target)
    if (!$resolved.StartsWith([IO.Path]::GetFullPath($repoRoot)+'\', [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -ne 'BrowserRuntime') { throw "Unsafe generated target: $resolved" }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
    New-Item -ItemType Directory -Path $resolved | Out-Null
    foreach ($item in Get-ChildItem -LiteralPath $source -Force) {
        Copy-Item -LiteralPath $item.FullName -Destination $resolved -Recurse -Force
    }
}
Write-Host 'Browser game runtime bundled with Dist and Debug editor.'
