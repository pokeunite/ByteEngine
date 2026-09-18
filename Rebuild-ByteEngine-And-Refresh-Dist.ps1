param(
    [ValidateSet("Debug", "Release")]
    [string]$BuildConfiguration = "Release"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter(Mandatory = $true)]
        [string]$FailureMessage
    )

    Write-Host ""
    Write-Host "> dotnet $($Arguments -join ' ')" -ForegroundColor Cyan

    & dotnet @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage (exit code $LASTEXITCODE)."
    }
}

$repoRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$solution = Join-Path $repoRoot "ByteEngine.sln"
$editorProject = Join-Path $repoRoot "Editor\ByteEngine.Editor\ByteEngine.Editor.csproj"
$publishScript = Join-Path $repoRoot "Tools\PublishEditor.ps1"

$distRoot = Join-Path $repoRoot "Dist\ByteEngine"
$distExe = Join-Path $distRoot "ByteEngine.Editor.exe"
$distDll = Join-Path $distRoot "ByteEngine.Editor.dll"
$distOpenAL = Join-Path $distRoot "openal32.dll"

$debugEditorRoot = Join-Path $repoRoot "Editor\ByteEngine.Editor\bin\Debug\net9.0-windows\win-x64"
$debugEditorExe = Join-Path $debugEditorRoot "ByteEngine.Editor.exe"
$debugEditorDll = Join-Path $debugEditorRoot "ByteEngine.Editor.dll"
$debugOpenAL = Join-Path $debugEditorRoot "openal32.dll"

Write-Host ""
Write-Host "==========================================================" -ForegroundColor DarkCyan
Write-Host " ByteEngine - Rebuild Debug + Selected Config + Dist" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor DarkCyan
Write-Host "Repository      : $repoRoot"
Write-Host "Dist config     : $BuildConfiguration"
Write-Host "dotnet run path : Debug"
Write-Host ""

if (-not (Test-Path -LiteralPath $solution)) {
    throw "ByteEngine.sln was not found beside this script."
}

if (-not (Test-Path -LiteralPath $editorProject)) {
    throw "Editor project was not found: $editorProject"
}

if (-not (Test-Path -LiteralPath $publishScript)) {
    throw "Required publish script was not found: $publishScript"
}

$runningEditors = Get-Process -Name "ByteEngine.Editor" -ErrorAction SilentlyContinue

if ($runningEditors) {
    Write-Host "Stopping running ByteEngine Editor process(es)..." -ForegroundColor Yellow

    foreach ($process in $runningEditors) {
        try { $path = $process.Path }
        catch { $path = "<path unavailable>" }

        Write-Host "  PID $($process.Id)  $path"
        Stop-Process -Id $process.Id -Force
    }

    Start-Sleep -Milliseconds 700
}

Push-Location $repoRoot

try {
    $startedAt = Get-Date

    Write-Host ""
    Write-Host "[1/6] Cleaning selected solution configuration..." -ForegroundColor Green
    Invoke-DotNet `
        -Arguments @("clean", $solution, "-c", $BuildConfiguration, "--nologo") `
        -FailureMessage "ByteEngine $BuildConfiguration clean failed"

    Write-Host ""
    Write-Host "[2/6] Building selected solution configuration..." -ForegroundColor Green
    Invoke-DotNet `
        -Arguments @("build", $solution, "-c", $BuildConfiguration, "--nologo", "--no-incremental") `
        -FailureMessage "ByteEngine $BuildConfiguration solution build failed"

    Write-Host ""
    Write-Host "[3/6] Refreshing Debug output used by plain dotnet run..." -ForegroundColor Green

    Invoke-DotNet `
        -Arguments @("clean", $editorProject, "-c", "Debug", "--nologo") `
        -FailureMessage "ByteEngine Editor Debug clean failed"

    Invoke-DotNet `
        -Arguments @("build", $editorProject, "-c", "Debug", "--nologo", "--no-incremental") `
        -FailureMessage "ByteEngine Editor Debug build failed"

    Write-Host ""
    Write-Host "[4/6] Verifying Debug editor output..." -ForegroundColor Green

    foreach ($requiredFile in @($debugEditorExe, $debugEditorDll, $debugOpenAL)) {
        if (-not (Test-Path -LiteralPath $requiredFile)) {
            throw "Required Debug editor file is missing: $requiredFile"
        }
    }

    $timestampTolerance = $startedAt.AddSeconds(-5)
    $debugExeInfo = Get-Item -LiteralPath $debugEditorExe
    $debugDllInfo = Get-Item -LiteralPath $debugEditorDll

    if ($debugExeInfo.LastWriteTime -lt $timestampTolerance) {
        throw "Debug editor executable does not look freshly generated. LastWriteTime: $($debugExeInfo.LastWriteTime)"
    }

    if ($debugDllInfo.LastWriteTime -lt $timestampTolerance) {
        throw "Debug editor DLL does not look freshly generated. LastWriteTime: $($debugDllInfo.LastWriteTime)"
    }

    Write-Host ""
    Write-Host "[5/6] Recreating Dist\ByteEngine..." -ForegroundColor Green

    & $publishScript -Configuration $BuildConfiguration

    if ($LASTEXITCODE -ne 0) {
        throw "Tools\PublishEditor.ps1 failed (exit code $LASTEXITCODE)."
    }

    Write-Host ""
    Write-Host "[6/6] Verifying fresh Dist package..." -ForegroundColor Green

    foreach ($requiredFile in @($distExe, $distDll, $distOpenAL)) {
        if (-not (Test-Path -LiteralPath $requiredFile)) {
            throw "Required Dist file is missing: $requiredFile"
        }
    }

    $exeInfo = Get-Item -LiteralPath $distExe
    $dllInfo = Get-Item -LiteralPath $distDll
    $openAlInfo = Get-Item -LiteralPath $distOpenAL

    if ($exeInfo.LastWriteTime -lt $timestampTolerance) {
        throw "Dist executable does not look freshly generated. LastWriteTime: $($exeInfo.LastWriteTime)"
    }

    if ($dllInfo.LastWriteTime -lt $timestampTolerance) {
        throw "Dist editor DLL does not look freshly generated. LastWriteTime: $($dllInfo.LastWriteTime)"
    }

    $debugExeHash = (Get-FileHash -LiteralPath $debugEditorExe -Algorithm SHA256).Hash
    $debugDllHash = (Get-FileHash -LiteralPath $debugEditorDll -Algorithm SHA256).Hash
    $distExeHash = (Get-FileHash -LiteralPath $distExe -Algorithm SHA256).Hash
    $distDllHash = (Get-FileHash -LiteralPath $distDll -Algorithm SHA256).Hash

    Write-Host ""
    Write-Host "==========================================================" -ForegroundColor DarkGreen
    Write-Host " BYTEENGINE DEBUG + DIST REFRESH SUCCESS" -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor DarkGreen
    Write-Host ""

    Write-Host "Development / dotnet run output:"
    Write-Host "  $debugEditorRoot" -ForegroundColor Cyan
    Write-Host "  EXE timestamp : $($debugExeInfo.LastWriteTime)"
    Write-Host "  DLL timestamp : $($debugDllInfo.LastWriteTime)"
    Write-Host "  EXE SHA256    : $debugExeHash"
    Write-Host "  DLL SHA256    : $debugDllHash"
    Write-Host ""

    Write-Host "Dist package:"
    Write-Host "  $distRoot" -ForegroundColor Cyan
    Write-Host "  EXE timestamp : $($exeInfo.LastWriteTime)"
    Write-Host "  DLL timestamp : $($dllInfo.LastWriteTime)"
    Write-Host "  EXE SHA256    : $distExeHash"
    Write-Host "  DLL SHA256    : $distDllHash"
    Write-Host "  OpenAL        : $($openAlInfo.LastWriteTime)"
    Write-Host ""

    Write-Host "Both launch paths are now fresh:"
    Write-Host "  dotnet run --project Editor/ByteEngine.Editor/ByteEngine.Editor.csproj" -ForegroundColor Cyan
    Write-Host "  .\Dist\ByteEngine\ByteEngine.Editor.exe" -ForegroundColor Cyan
    Write-Host ""
}
finally {
    Pop-Location
}
