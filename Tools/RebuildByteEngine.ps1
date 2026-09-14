param(
    [switch]$NoLaunch,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$toolsDirectory = $PSScriptRoot
$repositoryRoot = Split-Path -Parent $toolsDirectory
$solution = Join-Path $repositoryRoot "ByteEngine.sln"
$testsProject = Join-Path $repositoryRoot "Tests\ByteEngine.Tests\ByteEngine.Tests.csproj"
$publishScript = Join-Path $toolsDirectory "PublishEditor.ps1"
$publishedEditor = Join-Path $repositoryRoot "Dist\ByteEngine\ByteEngine.Editor.exe"
$publishedDirectory = Split-Path -Parent $publishedEditor

Set-Location $repositoryRoot

Write-Host ""
Write-Host "========================================="
Write-Host " ByteEngine - Full Rebuild / Update"
Write-Host "========================================="
Write-Host ""

Write-Host "[1/5] Closing running ByteEngine Editor..."
Get-Process "ByteEngine.Editor" -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue

Write-Host "[2/5] Restoring packages..."
dotnet restore $solution
if ($LASTEXITCODE -ne 0) { throw "Package restore failed." }

Write-Host "[3/5] Building full solution..."
dotnet build $solution -c Debug --no-restore
if ($LASTEXITCODE -ne 0) { throw "Solution build failed." }

if (-not $SkipTests)
{
    Write-Host "[4/5] Running ByteEngine regression suite..."
    dotnet run --project $testsProject -c Debug --no-build
    if ($LASTEXITCODE -ne 0) { throw "Regression tests failed." }
}
else
{
    Write-Host "[4/5] Regression tests skipped."
}

Write-Host "[5/5] Publishing the self-contained ByteEngine editor folder..."
& $publishScript -Configuration Release

if (-not (Test-Path -LiteralPath $publishedEditor))
{
    throw "Published ByteEngine.Editor.exe was not produced."
}

Write-Host ""
Write-Host "ByteEngine update completed successfully."

if (-not $NoLaunch)
{
    Write-Host "Launching updated ByteEngine Editor..."
    Start-Process -FilePath $publishedEditor -WorkingDirectory $publishedDirectory
}
