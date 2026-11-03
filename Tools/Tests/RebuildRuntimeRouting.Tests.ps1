$ErrorActionPreference='Stop'
$root=Join-Path (Get-Location) ('.artifacts/rebuild-routing-'+[guid]::NewGuid().ToString('N'))
$src=Join-Path $root 'source';$dest=Join-Path $root 'installed'
foreach($relative in @('Tools','Plugins/KarlsonDemo','artifacts/plugins','Dist/ByteEngine/BrowserRuntime')){New-Item -ItemType Directory -Force (Join-Path $src $relative)|Out-Null}
foreach($relative in @('ByteEngine.sln','Plugins/KarlsonDemo/KarlsonDemo.Plugin.csproj','artifacts/plugins/KarlsonDemo.byteplugin')){Set-Content (Join-Path $src $relative) 'fixture'}
Set-Content (Join-Path $src 'Tools/PublishPlayer.ps1') 'param($Configuration) $global:LASTEXITCODE=0'
@'
param($Configuration,[switch]$SkipBrowser,[switch]$IncludeBrowser)
$dist=Join-Path (Split-Path $PSScriptRoot) 'Dist/ByteEngine'
New-Item -ItemType Directory -Force (Join-Path $dist 'PlayerRuntime')|Out-Null
foreach($name in @('ByteEngine.Editor.exe','ByteEngine.Editor.dll','ByteEngine.Core.dll','PlayerRuntime/ByteEngine.Player.exe','PlayerRuntime/ByteEngine.Launcher.exe','PlayerRuntime/ByteEngine.Core.dll','PlayerRuntime/openal32.dll')){Set-Content (Join-Path $dist $name) 'matching-fixture'}
Set-Content (Join-Path (Split-Path $PSScriptRoot) 'routing.txt') "Skip=$SkipBrowser Include=$IncludeBrowser"
if(!$SkipBrowser-and$IncludeBrowser){Set-Content (Join-Path $dist 'BrowserRuntime/route.txt') 'explicit-full-web'}
$global:LASTEXITCODE=0
'@ | Set-Content (Join-Path $src 'Tools/PublishEditor.ps1')
@'
param($Configuration)
Set-Content (Join-Path (Split-Path $PSScriptRoot) 'Dist/ByteEngine/BrowserRuntime/route.txt') 'explicit-web-only'
$global:LASTEXITCODE=0
'@ | Set-Content (Join-Path $src 'Tools/PublishBrowser.ps1')
New-Item -ItemType Directory -Force (Join-Path $dest 'Dist/ByteEngine/BrowserRuntime')|Out-Null
Set-Content (Join-Path $src 'Dist/ByteEngine/BrowserRuntime/route.txt') 'old-source-web'
Set-Content (Join-Path $dest 'Dist/ByteEngine/BrowserRuntime/route.txt') 'installed-web'
@'
param($SourceRoot,$InstallRoot,$ProjectFile)
Add-Content (Join-Path $SourceRoot 'sync-routes.txt') $InstallRoot
$global:LASTEXITCODE=0
'@ | Set-Content (Join-Path $src 'Tools/SyncBrowserPluginPackages.ps1')
$script=Join-Path (Get-Location) 'Rebuild-ByteEngine-And-Refresh-Dist.ps1'
$global:rebuildHarnessCalls=0
function global:dotnet {$global:rebuildHarnessCalls++;$global:LASTEXITCODE=0}
& $script -SourceRoot $src -InstallRoot $dest
if((Get-Content (Join-Path $src 'routing.txt'))-ne 'Skip=True Include=False'){throw 'Windows default selected browser build'}
if((Get-Content (Join-Path $dest 'Dist/ByteEngine/BrowserRuntime/route.txt'))-ne 'installed-web'){throw 'Windows-only update replaced existing browser runtime'}
if(Test-Path (Join-Path $src 'sync-routes.txt')){throw 'Windows default unexpectedly synchronized browser plugin inputs'}
Write-Output 'PASS Windows default skips web and preserves installed web files.'
& $script -SourceRoot $src -InstallRoot $dest -IncludeBrowser
if((Get-Content (Join-Path $dest 'Dist/ByteEngine/BrowserRuntime/route.txt'))-ne 'explicit-full-web'){throw 'IncludeBrowser was not synchronized'}
if(@(Get-Content (Join-Path $src 'sync-routes.txt')).Count-ne 1){throw 'Full browser rebuild did not synchronize plugin packages'}
Write-Output 'PASS explicit full rebuild includes and installs web.'
$calls=$global:rebuildHarnessCalls
& $script -SourceRoot $src -InstallRoot $dest -BrowserOnly
if($global:rebuildHarnessCalls-ne$calls){throw 'Browser-only unnecessarily built Windows projects'}
foreach($relative in @('Dist/ByteEngine/BrowserRuntime','Editor/ByteEngine.Editor/bin/Debug/net9.0-windows/win-x64/BrowserRuntime','Editor/ByteEngine.Editor/bin/Release/net9.0-windows/win-x64/BrowserRuntime')){if((Get-Content (Join-Path $dest "$relative/route.txt"))-ne 'explicit-web-only'){throw 'Browser-only copies missing'}}
Write-Output 'PASS BrowserOnly skips Windows and synchronizes Dist, Debug and Release.'
if(@(Get-Content (Join-Path $src 'sync-routes.txt')).Count-ne 2){throw 'BrowserOnly did not synchronize plugin packages'}
Write-Output 'PASS BrowserOnly and IncludeBrowser both synchronize matching plugin packages.'
Remove-Item Function:/dotnet
