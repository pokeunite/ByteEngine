param([string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot "Player\ByteEngine.Player\ByteEngine.Player.csproj"
$output = Join-Path $repositoryRoot (".artifacts\player-publish\" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $output -Force | Out-Null
try {
dotnet publish $project -c $Configuration -r win-x64 --self-contained true -o $output -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw "Windows player publish failed." }
foreach ($required in @("ByteEngine.Player.exe", "ByteEngine.Core.dll", "coreclr.dll", "hostfxr.dll", "hostpolicy.dll", "openal32.dll", "glfw3.dll", "assimp.dll")) {
    if (-not (Test-Path -LiteralPath (Join-Path $output $required))) { throw "Player publish is incomplete: $required" }
}
$targets = @(
    (Join-Path $repositoryRoot "Dist\ByteEngine\PlayerRuntime"),
    (Join-Path $repositoryRoot "Editor\ByteEngine.Editor\bin\Debug\net9.0-windows\win-x64\PlayerRuntime")
)
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
Write-Host "Standalone Windows player bundled with editor distribution and Debug editor."

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