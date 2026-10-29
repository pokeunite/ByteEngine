param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
# Serialize the entire build and installation, including generated AOT inputs.
# Concurrent chats may publish this shared engine; those builds must not rewrite
# the same physics assemblies or browser runtime while another publish uses them.
$identityHash = [Security.Cryptography.SHA256]::Create()
try {
    $lockIdentity = [BitConverter]::ToString($identityHash.ComputeHash(
        [Text.Encoding]::UTF8.GetBytes([IO.Path]::GetFullPath($repoRoot).ToLowerInvariant()))).Replace('-', '')
}
finally { $identityHash.Dispose() }
$publishMutex = [Threading.Mutex]::new($false, "Local\ByteEngineBrowserPublish_$lockIdentity")
$publishLockHeld = $false
try {
    while (-not $publishLockHeld) {
        try { $publishLockHeld = $publishMutex.WaitOne(1000) }
        catch [Threading.AbandonedMutexException] { $publishLockHeld = $true }
        if (-not $publishLockHeld) { Write-Verbose 'Waiting for the active browser publish.' }
    }
& dotnet publish (Join-Path $repoRoot 'Player/ByteEngine.Browser/ByteEngine.Browser.csproj') -c $Configuration -m:1 -nodeReuse:false
if ($LASTEXITCODE -ne 0) { throw 'Browser player publish failed. See the build diagnostics above; ensure the .NET 9 wasm-tools workload is installed.' }
$source = Join-Path $repoRoot "Player/ByteEngine.Browser/bin/$Configuration/net9.0/publish/wwwroot"
# Incremental publishes can leave old fingerprinted assemblies beside the current boot manifest.
# Keep only current versions so future exports do not ship duplicate runtime binaries.
$framework = Join-Path $source '_framework'
$boot = Get-Content -LiteralPath (Join-Path $framework 'blazor.boot.json') -Raw | ConvertFrom-Json
$currentFiles = @($boot.resources.fingerprinting.PSObject.Properties.Name)
$symbolDirectory = Join-Path $repoRoot 'artifacts/BrowserSymbols'
New-Item -ItemType Directory -Path $symbolDirectory -Force | Out-Null
foreach ($symbolFile in Get-ChildItem -LiteralPath $framework -File -Filter '*.symbols') {
    # Preserve private symbols before pruning deployable files; bind each map to its WASM hash.
    $nativeWasm = Get-ChildItem -LiteralPath $framework -File -Filter 'dotnet.native*.wasm' | Where-Object { $_.Name -in $currentFiles } | Select-Object -First 1
    if ($nativeWasm) {
        $nativeHash = (Get-FileHash -LiteralPath $nativeWasm.FullName -Algorithm SHA256).Hash
        Copy-Item -LiteralPath $symbolFile.FullName -Destination (Join-Path $symbolDirectory ($nativeHash+'.symbols')) -Force
    }
}
$privateMap = Join-Path $repoRoot "Player/ByteEngine.Browser/obj/$Configuration/net9.0/wasm/for-publish/dotnet.native.js.symbols"
$privateWasm = Join-Path (Split-Path -Parent $privateMap) 'dotnet.native.wasm'
if ((Test-Path -LiteralPath $privateMap) -and (Test-Path -LiteralPath $privateWasm)) {
    $nativeWasm = Get-ChildItem -LiteralPath $framework -File -Filter 'dotnet.native*.wasm' | Where-Object { $_.Name -in $currentFiles } | Select-Object -First 1
    $nativeHash = (Get-FileHash -LiteralPath $privateWasm -Algorithm SHA256).Hash
    if (!$nativeWasm -or (Get-FileHash -LiteralPath $nativeWasm.FullName -Algorithm SHA256).Hash -ne $nativeHash) {
        throw 'Private CPU symbol map does not match the published native WASM binary.'
    }
    Copy-Item -LiteralPath $privateMap -Destination (Join-Path $symbolDirectory ($nativeHash+'.symbols')) -Force
}
foreach ($file in Get-ChildItem -LiteralPath $framework -File) {
    $baseName = $file.Name -replace '\.(br|gz)$',''
    if ($file.Name -match '\.(br|gz|symbols)$' -or ($baseName -match '\.[a-z0-9]{10}\.(wasm|js|dat)$' -and $baseName -notin $currentFiles)) {
        Remove-Item -LiteralPath $file.FullName -Force
    }
}
[IO.File]::WriteAllText((Join-Path $source 'browser-plugins.json'),'["bytebard.desertterrain","bytebard.dunecompany","bytebard.karlsondemo"]')
$pluginBuilds = @{}
foreach ($pluginName in @('DesertTerrain','DuneCompany','KarlsonDemo')) {
    $pluginRoot = Join-Path $repoRoot "Plugins/$pluginName"
    $manifest = Get-Content (Join-Path $pluginRoot 'plugin.json') -Raw | ConvertFrom-Json
    $assembly = Join-Path $pluginRoot "bin/$Configuration/net9.0/$($manifest.assembly)"
    if (!(Test-Path -LiteralPath $assembly)) { throw "Missing compiled browser plugin: $assembly" }
    $pluginBuilds[$manifest.id] = @{ version=$manifest.version; sha256=(Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash }
}
[IO.File]::WriteAllText((Join-Path $source 'browser-plugin-builds.json'),($pluginBuilds | ConvertTo-Json -Depth 4))
foreach ($file in @('index.html','main.js','renderer.js','water.js','graphics.js','frame-wire.js','audio.js','video.js','content.js','performance.js','_framework/dotnet.js','Resources/Fonts/TypeLightSans.ttf')) {
    if (!(Test-Path -LiteralPath (Join-Path $source $file))) { throw "Browser runtime missing: $file" }
}
$runtimeFiles = [ordered]@{}
foreach ($file in Get-ChildItem -LiteralPath $source -File -Recurse | Sort-Object FullName) {
    $relative = $file.FullName.Substring($source.Length).TrimStart('\','/').Replace('\','/')
    if ($relative -eq 'browser-runtime-integrity.json') { continue }
    $runtimeFiles[$relative] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
}
$integrity = @{ formatVersion=1; configuration=$Configuration; files=$runtimeFiles }
[IO.File]::WriteAllText((Join-Path $source 'browser-runtime-integrity.json'),($integrity | ConvertTo-Json -Depth 5))
foreach ($target in @((Join-Path $repoRoot 'Dist/ByteEngine/BrowserRuntime'),
    (Join-Path $repoRoot 'Dist/ByteEngine-BuildDebug/BrowserRuntime'),
    (Join-Path $repoRoot 'Editor/ByteEngine.Editor/bin/Release/net9.0-windows/win-x64/BrowserRuntime'),
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
}
finally {
    if ($publishLockHeld) { $publishMutex.ReleaseMutex() }
    $publishMutex.Dispose()
}
