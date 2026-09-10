param([string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "Editor/ByteEngine.Editor/ByteEngine.Editor.csproj"
$output = Join-Path $repoRoot "Dist/ByteEngine"
New-Item -ItemType Directory -Force -Path $output | Out-Null
dotnet publish $project -c $Configuration -r win-x64 --self-contained true -o $output -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false
$exe = Join-Path $output "ByteEngine.Editor.exe"
if (-not (Test-Path -LiteralPath $exe)) { throw "Publish did not produce $exe" }
$rootExe = Join-Path $repoRoot "ByteEngine.Editor.exe"
if (Test-Path -LiteralPath $rootExe) { Remove-Item -LiteralPath $rootExe -Force }
New-Item -ItemType HardLink -Path $rootExe -Target $exe | Out-Null
Write-Host "Published ByteEngine Editor: $exe"
Write-Host "Updated zero-copy convenience hard link: $rootExe"
