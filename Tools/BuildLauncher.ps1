param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vs=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (!$vs) {throw 'Building the native launcher requires Visual Studio C++ Build Tools.'}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$source=Join-Path $repo 'Player/ByteEngine.Launcher/Launcher.cpp'
$exe=Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'ByteEngine.Launcher.exe'
$obj=Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'ByteEngine.Launcher.obj'
$resource=Join-Path $repo 'Player/ByteEngine.Launcher/Launcher.rc'
$res=Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'ByteEngine.Launcher.res'
$vcvars=Join-Path $vs 'VC/Auxiliary/Build/vcvars64.bat'
# Fixed compiler invocation; no filesystem operations are delegated to cmd.
Push-Location -LiteralPath $repo
try { & cmd /d /c "`"$vcvars`" >nul && rc /nologo /fo `"$res`" `"$resource`" && cl /nologo /std:c++17 /O2 /MT /W4 /EHsc `"$source`" /Fe:`"$exe`" /Fo:`"$obj`" /link /SUBSYSTEM:WINDOWS `"$res`" shell32.lib user32.lib"
if ($LASTEXITCODE -ne 0) {throw 'Native launcher compilation failed.'}
} finally { Pop-Location }
Remove-Item -LiteralPath $obj,$res -ErrorAction SilentlyContinue
