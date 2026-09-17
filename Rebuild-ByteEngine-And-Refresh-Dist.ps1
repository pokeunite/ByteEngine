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
$publishScript = Join-Path $repoRoot "Tools\PublishEditor.ps1"
$distRoot = Join-Path $repoRoot "Dist\ByteEngine"
$distExe = Join-Path $distRoot "ByteEngine.Editor.exe"
$distDll = Join-Path $distRoot "ByteEngine.Editor.dll"
$distOpenAL = Join-Path $distRoot "openal32.dll"

Write-Host ""
Write-Host "==============================================" -ForegroundColor DarkCyan
Write-Host " ByteEngine - Rebuild + Refresh Dist" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor DarkCyan
Write-Host "Repository: $repoRoot"
Write-Host "Build config: $BuildConfiguration"
Write-Host ""

if (-not (Test-Path -LiteralPath $solution)) {
    throw "ByteEngine.sln was not found beside this script. Put this .ps1 file in C:\Users\codex\ByteEngine and run it from there."
}

if (-not (Test-Path -LiteralPath $publishScript)) {
    throw "Required publish script was not found: $publishScript"
}

# Make absolutely sure the editor is not holding old Dist binaries open.
$runningEditors = Get-Process -Name "ByteEngine.Editor" -ErrorAction SilentlyContinue

if ($runningEditors) {
    Write-Host "Stopping running ByteEngine Editor process(es)..." -ForegroundColor Yellow

    foreach ($process in $runningEditors) {
        try {
            $path = $process.Path
        }
        catch {
            $path = "<path unavailable>"
        }

        Write-Host "  PID $($process.Id)  $path"
        Stop-Process -Id $process.Id -Force
    }

    Start-Sleep -Milliseconds 700
}

Push-Location $repoRoot

try {
    $startedAt = Get-Date

    Write-Host ""
    Write-Host "[1/4] Cleaning previous compiled output..." -ForegroundColor Green
    Invoke-DotNet `
        -Arguments @("clean", $solution, "-c", $BuildConfiguration, "--nologo") `
        -FailureMessage "ByteEngine clean failed"

    Write-Host ""
    Write-Host "[2/4] Building ByteEngine solution..." -ForegroundColor Green
    Invoke-DotNet `
        -Arguments @("build", $solution, "-c", $BuildConfiguration, "--nologo", "--no-incremental") `
        -FailureMessage "ByteEngine solution build failed"

    Write-Host ""
    Write-Host "[3/4] Recreating Dist\ByteEngine..." -ForegroundColor Green

    # PublishEditor.ps1 deletes Dist\ByteEngine first and republishes the editor
    # as a complete self-contained folder. It also verifies/copies openal32.dll.
    & $publishScript -Configuration $BuildConfiguration

    if ($LASTEXITCODE -ne 0) {
        throw "Tools\PublishEditor.ps1 failed (exit code $LASTEXITCODE)."
    }

    Write-Host ""
    Write-Host "[4/4] Verifying fresh Dist package..." -ForegroundColor Green

    foreach ($requiredFile in @($distExe, $distDll, $distOpenAL)) {
        if (-not (Test-Path -LiteralPath $requiredFile)) {
            throw "Required Dist file is missing: $requiredFile"
        }
    }

    $exeInfo = Get-Item -LiteralPath $distExe
    $dllInfo = Get-Item -LiteralPath $distDll
    $openAlInfo = Get-Item -LiteralPath $distOpenAL

    # Because PublishEditor removes the whole Dist folder before publishing,
    # these files must have been recreated during this run. This timestamp
    # check catches any unexpected publish/output-path problem.
    $timestampTolerance = $startedAt.AddSeconds(-5)

    if ($exeInfo.LastWriteTime -lt $timestampTolerance) {
        throw "Dist executable does not look freshly generated. LastWriteTime: $($exeInfo.LastWriteTime)"
    }

    if ($dllInfo.LastWriteTime -lt $timestampTolerance) {
        throw "Dist editor DLL does not look freshly generated. LastWriteTime: $($dllInfo.LastWriteTime)"
    }

    $exeHash = (Get-FileHash -LiteralPath $distExe -Algorithm SHA256).Hash
    $dllHash = (Get-FileHash -LiteralPath $distDll -Algorithm SHA256).Hash

    Write-Host ""
    Write-Host "==============================================" -ForegroundColor DarkGreen
    Write-Host " BYTEENGINE BUILD + DIST REFRESH SUCCESS" -ForegroundColor Green
    Write-Host "==============================================" -ForegroundColor DarkGreen
    Write-Host ""
    Write-Host "Fresh Dist package:"
    Write-Host "  $distRoot"
    Write-Host ""
    Write-Host "ByteEngine.Editor.exe"
    Write-Host "  LastWriteTime : $($exeInfo.LastWriteTime)"
    Write-Host "  Size          : $([Math]::Round($exeInfo.Length / 1MB, 2)) MB"
    Write-Host "  SHA256        : $exeHash"
    Write-Host ""
    Write-Host "ByteEngine.Editor.dll"
    Write-Host "  LastWriteTime : $($dllInfo.LastWriteTime)"
    Write-Host "  Size          : $([Math]::Round($dllInfo.Length / 1KB, 1)) KB"
    Write-Host "  SHA256        : $dllHash"
    Write-Host ""
    Write-Host "openal32.dll"
    Write-Host "  LastWriteTime : $($openAlInfo.LastWriteTime)"
    Write-Host "  Size          : $([Math]::Round($openAlInfo.Length / 1KB, 1)) KB"
    Write-Host ""
    Write-Host "Launch this build:"
    Write-Host "  $distExe" -ForegroundColor Cyan
    Write-Host ""
}
finally {
    Pop-Location
}
