param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$toolsDirectory = $PSScriptRoot
$repositoryRoot = Split-Path -Parent $toolsDirectory
$project = Join-Path $repositoryRoot "Editor\ByteEngine.Editor\ByteEngine.Editor.csproj"
$output = Join-Path $repositoryRoot "Dist\ByteEngine"
$publishedExecutable = Join-Path $output "ByteEngine.Editor.exe"
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
dotnet publish $project -c $Configuration -r win-x64 --self-contained true -o $resolvedOutput -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false

if ($LASTEXITCODE -ne 0)
{
    throw "ByteEngine Editor publish failed."
}

if (-not (Test-Path -LiteralPath $publishedExecutable))
{
    throw "Published ByteEngine.Editor.exe was not produced."
}

if (Test-Path -LiteralPath $rootExecutable)
{
    # Older updater versions created this broken single-file hard link. Remove it
    # so nobody accidentally launches a package that cannot display FBX models.
    Remove-Item -LiteralPath $rootExecutable -Force
}

Write-Host "Published ByteEngine Editor: $publishedExecutable"
Write-Host "The complete Dist\ByteEngine folder must stay beside the executable."
