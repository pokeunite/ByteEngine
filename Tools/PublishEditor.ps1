param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$toolsDirectory = $PSScriptRoot
$repositoryRoot = Split-Path -Parent $toolsDirectory
$project = Join-Path $repositoryRoot "Editor\ByteEngine.Editor\ByteEngine.Editor.csproj"
$output = Join-Path $repositoryRoot "Dist\ByteEngine"
$publishedExecutable = Join-Path $output "ByteEngine.Editor.exe"
$publishedOpenAL = Join-Path $output "openal32.dll"
$rootExecutable = Join-Path $repositoryRoot "ByteEngine.Editor.exe"

$expectedOutput = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRoot "Dist\ByteEngine"))
$resolvedOutput = [System.IO.Path]::GetFullPath($output)

if (-not [string]::Equals(
    $resolvedOutput,
    $expectedOutput,
    [System.StringComparison]::OrdinalIgnoreCase))
{
    throw "Refusing to clean unexpected publish directory '$resolvedOutput'."
}

if (Test-Path -LiteralPath $resolvedOutput)
{
    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}

New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null

# Keep the editor as a normal self-contained folder. ByteEngine loads FBX models
# through managed and native Assimp assemblies; collapsing those files into a
# single executable changes runtime loading behavior and caused meshes to vanish.
dotnet publish $project `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -o $resolvedOutput `
    -p:PublishSingleFile=false `
    -p:DebugType=None `
    -p:DebugSymbols=false

if ($LASTEXITCODE -ne 0)
{
    throw "ByteEngine Editor publish failed."
}

if (-not (Test-Path -LiteralPath $publishedExecutable))
{
    throw "Published ByteEngine.Editor.exe was not produced."
}

#
# The Editor csproj has an AfterTargets=Publish step which copies the native
# Silk.NET OpenAL Soft runtime to the exact filename OpenTK resolves:
#
#     soft_oal.dll -> openal32.dll
#
# Do not silently publish a package without it.
#
if (-not (Test-Path -LiteralPath $publishedOpenAL))
{
    # Defensive fallback for unusual NuGet/MSBuild environments. The package
    # has already been restored at this point, so search the two standard
    # global-package locations directly.
    $candidateRoots = @()

    if (-not [string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES))
    {
        $candidateRoots += $env:NUGET_PACKAGES
    }

    if (-not [string]::IsNullOrWhiteSpace($env:USERPROFILE))
    {
        $candidateRoots += (Join-Path $env:USERPROFILE ".nuget\packages")
    }

    $candidateRoots = $candidateRoots |
        Select-Object -Unique

    $copied = $false

    foreach ($nugetRoot in $candidateRoots)
    {
        $softOal = Join-Path $nugetRoot "silk.net.openal.soft.native\1.23.1\runtimes\win-x64\native\soft_oal.dll"

        if (-not (Test-Path -LiteralPath $softOal))
        {
            continue
        }

        Copy-Item `
            -LiteralPath $softOal `
            -Destination $publishedOpenAL `
            -Force

        Write-Host "ByteEngine: copied OpenAL Soft from NuGet cache:"
        Write-Host "  $softOal"
        Write-Host "  -> $publishedOpenAL"

        $copied = $true
        break
    }

    if (-not $copied)
    {
        throw @"
ByteEngine Editor publish produced no openal32.dll.

OpenAL Soft should have been restored by:
  Silk.NET.OpenAL.Soft.Native 1.23.1

Expected NuGet native file:
  silk.net.openal.soft.native\1.23.1\runtimes\win-x64\native\soft_oal.dll

Run:
  dotnet restore

Then publish again.
"@
    }
}

if (Test-Path -LiteralPath $rootExecutable)
{
    # Older updater versions created this broken single-file hard link. Remove
    # it so nobody accidentally launches a package that cannot display FBX
    # models or load native engine dependencies.
    Remove-Item -LiteralPath $rootExecutable -Force
}

$openAlInfo = Get-Item -LiteralPath $publishedOpenAL

Write-Host ""
Write-Host "Published ByteEngine Editor:"
Write-Host "  $publishedExecutable"
Write-Host ""
Write-Host "OpenAL runtime verified:"
Write-Host "  $($openAlInfo.FullName)"
Write-Host "  $([Math]::Round($openAlInfo.Length / 1KB, 1)) KB"
Write-Host ""
Write-Host "The complete Dist\ByteEngine folder must stay beside the executable."
