param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot "Tools\ByteEngine.Mcp\ByteEngine.Mcp.csproj"
$output = Join-Path $repositoryRoot "Dist\ByteEngine-MCP"

$expectedOutput = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRoot "Dist\ByteEngine-MCP"))
$resolvedOutput = [System.IO.Path]::GetFullPath($output)
if (-not [string]::Equals($resolvedOutput, $expectedOutput, [System.StringComparison]::OrdinalIgnoreCase))
{
    throw "Refusing to clean unexpected publish directory '$resolvedOutput'."
}

if (Test-Path -LiteralPath $resolvedOutput)
{
    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}

New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null

dotnet publish $project -c $Configuration -o $resolvedOutput --nologo
if ($LASTEXITCODE -ne 0)
{
    throw "ByteEngine MCP publish failed."
}

$executable = Join-Path $resolvedOutput "ByteEngine.Mcp.exe"
if (-not (Test-Path -LiteralPath $executable))
{
    throw "MCP publish is incomplete: ByteEngine.Mcp.exe was not produced."
}

Write-Host "Published ByteEngine MCP to $resolvedOutput"
Write-Host "Register it with: command='$executable'"
