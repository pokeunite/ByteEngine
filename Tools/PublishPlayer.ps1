param([string]$Configuration = "Release", [string]$DistributionDirectory = "")
$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot "Player\ByteEngine.Player\ByteEngine.Player.csproj"
$output = Join-Path $repositoryRoot (".artifacts\player-publish\" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $output -Force | Out-Null
try {
dotnet publish $project -c $Configuration -r win-x64 --self-contained true -o $output -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw "Windows player publish failed." }
$bundleOutput=Join-Path $output 'Bundled'
# Bundle the exact binaries just published. Rebuilding with single-file global
# properties can produce a different Core MVID from the loose player/editor.
dotnet publish $project -c $Configuration -r win-x64 --self-contained true --no-build -o $bundleOutput -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw "Bundled Windows player publish failed." }
$bundleExe=Join-Path $bundleOutput 'ByteEngine.Player.exe'
$identity=Join-Path $bundleOutput 'core-mvid.txt'
$identityProcess=Start-Process -FilePath $bundleExe -ArgumentList @('--write-runtime-identity',('"'+$identity+'"')) -WindowStyle Hidden -Wait -PassThru
if($identityProcess.ExitCode-ne 0-or!(Test-Path -LiteralPath $identity)){throw 'Bundled Windows player failed its startup identity check.'}
$looseIdentity=Join-Path $output 'loose-core-mvid.txt'
$looseProcess=Start-Process -FilePath (Join-Path $output 'ByteEngine.Player.exe') -ArgumentList @('--write-runtime-identity',('"'+$looseIdentity+'"')) -WindowStyle Hidden -Wait -PassThru
if($looseProcess.ExitCode-ne 0-or!(Test-Path -LiteralPath $looseIdentity)-or(Get-Content -LiteralPath $looseIdentity -Raw).Trim()-ne(Get-Content -LiteralPath $identity -Raw).Trim()){throw 'Bundled and loose Windows players have different Core identities.'}
Remove-Item -LiteralPath $looseIdentity -Force
# Only the self-contained executable and its paired identity belong in this
# staging subfolder. Native dependencies are already included in the bundle.
foreach($extra in Get-ChildItem -LiteralPath $bundleOutput -Force){if($extra.Name-notin @('ByteEngine.Player.exe','core-mvid.txt')){Remove-Item -LiteralPath $extra.FullName -Recurse -Force}}
& (Join-Path $PSScriptRoot "BuildLauncher.ps1") -OutputDirectory $output
foreach ($required in @("ByteEngine.Launcher.exe", "ByteEngine.Player.exe", "ByteEngine.Core.dll", "coreclr.dll", "hostfxr.dll", "hostpolicy.dll", "openal32.dll", "glfw3.dll", "assimp.dll")) {
    if (-not (Test-Path -LiteralPath (Join-Path $output $required))) { throw "Player publish is incomplete: $required" }
}
$targets = if ($Configuration -eq 'Debug') { @(
    (Join-Path $repositoryRoot "Dist\ByteEngine-BuildDebug\PlayerRuntime"),
    (Join-Path $repositoryRoot "Editor\ByteEngine.Editor\bin\Debug\net9.0-windows\win-x64\PlayerRuntime")
) } else { @(
    (Join-Path $repositoryRoot "Dist\ByteEngine\PlayerRuntime"),
    (Join-Path $repositoryRoot "Editor\ByteEngine.Editor\bin\Release\net9.0-windows\win-x64\PlayerRuntime")
) }
if($DistributionDirectory){$targets=@($targets+(Join-Path ([IO.Path]::GetFullPath($DistributionDirectory)) 'PlayerRuntime')|Select-Object -Unique)}
foreach ($target in $targets) {
    $resolved = [IO.Path]::GetFullPath($target)
    # Only generated, explicitly named runtime folders may be refreshed.
    if (-not $resolved.StartsWith([IO.Path]::GetFullPath($repositoryRoot) + "\", [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -ne "PlayerRuntime") { throw "Unsafe runtime target: $resolved" }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
    New-Item -ItemType Directory -Path $resolved -Force | Out-Null
    foreach ($item in Get-ChildItem -LiteralPath $output -Force) {
        Copy-Item -LiteralPath $item.FullName -Destination $resolved -Recurse -Force
    }
}
# The editor's exporter validates exact core build identity, not just AssemblyVersion.
# Publish flags can change the MVID; use this same current-source core in its matching editor.
foreach ($target in $targets) {
    $editorRoot = Split-Path -Parent $target
    if (Test-Path -LiteralPath (Join-Path $editorRoot "ByteEngine.Editor.dll")) {
        Copy-Item -LiteralPath (Join-Path $output "ByteEngine.Core.dll") -Destination (Join-Path $editorRoot "ByteEngine.Core.dll") -Force
    }
}
Write-Host "Matching $Configuration Windows player and editor core refreshed."

}
finally {
    $publishRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot ".artifacts\player-publish"))
    $resolvedOutput = [IO.Path]::GetFullPath($output)
    if (-not [string]::Equals([IO.Path]::GetDirectoryName($resolvedOutput), $publishRoot, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolvedOutput) -notmatch '^[0-9a-f]{32}$') {
        throw "Unsafe temporary publish directory: $resolvedOutput"
    }
    if (Test-Path -LiteralPath $resolvedOutput) {
        Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
    }
}
