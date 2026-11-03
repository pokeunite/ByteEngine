<#
.SYNOPSIS
Refreshes the editor and Windows player without exporting a game.
.EXAMPLE
powershell -ep bypass -file .\Rebuild-ByteEngine-And-Refresh-Dist.ps1
.EXAMPLE
powershell -ep bypass -file .\Rebuild-ByteEngine-And-Refresh-Dist.ps1 -BrowserOnly
.EXAMPLE
powershell -ep bypass -file .\Rebuild-ByteEngine-And-Refresh-Dist.ps1 -IncludeBrowser
#>
[CmdletBinding()]
param(
 [ValidateSet('Debug','Release')][string]$BuildConfiguration='Release',
 [string]$SourceRoot='',
 [string]$InstallRoot='',
 [string]$ProjectFile='',
 [switch]$VerifyOnly,
 [switch]$SkipBrowser,
 [switch]$IncludeBrowser,
 [switch]$BrowserOnly
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
# Installed launchers may point at the actively maintained source checkout.
$configFile=Join-Path $PSScriptRoot '.byteengine-build-source.json'
if(Test-Path -LiteralPath $configFile){
 $config=Get-Content -LiteralPath $configFile -Raw | ConvertFrom-Json
 if(!$SourceRoot -and $config.PSObject.Properties['SourceRoot']){$SourceRoot=$config.SourceRoot}
 if(!$ProjectFile -and $config.PSObject.Properties['ProjectFile']){$ProjectFile=$config.ProjectFile}
}
if(!$SourceRoot){$SourceRoot=$PSScriptRoot}
if(!$InstallRoot){$InstallRoot=$PSScriptRoot}
$SourceRoot=[IO.Path]::GetFullPath($SourceRoot)
$InstallRoot=[IO.Path]::GetFullPath($InstallRoot)
$syncBrowser=($IncludeBrowser -and !$SkipBrowser) -or $BrowserOnly
foreach($relative in @('ByteEngine.sln','Tools/PublishEditor.ps1','Tools/PublishPlayer.ps1','Tools/SyncBrowserPluginPackages.ps1','Plugins/KarlsonDemo/KarlsonDemo.Plugin.csproj')){
 if(!(Test-Path -LiteralPath (Join-Path $SourceRoot $relative))){throw "Current source is incomplete: $SourceRoot ($relative). Pass -SourceRoot with the maintained checkout."}
}
if($VerifyOnly){
 $dist=Join-Path $InstallRoot 'Dist/ByteEngine'
 $editor=Join-Path $dist 'ByteEngine.Core.dll';$player=Join-Path $dist 'PlayerRuntime/ByteEngine.Core.dll'
 if(!(Test-Path -LiteralPath $editor) -or !(Test-Path -LiteralPath $player)){throw 'Editor or player runtime is missing.'}
 if((Get-FileHash -LiteralPath $editor).Hash-ne (Get-FileHash -LiteralPath $player).Hash){throw 'Editor and player core builds differ.'}
 $bundle=Join-Path $dist 'PlayerRuntime/Bundled'
 if(Test-Path -LiteralPath (Join-Path $bundle 'ByteEngine.Player.exe')){
  $identity=Join-Path $bundle 'core-mvid.txt'
  $expected=[Reflection.Assembly]::LoadFile($editor).ManifestModule.ModuleVersionId.ToString()
  if(!(Test-Path -LiteralPath $identity)-or(Get-Content -LiteralPath $identity -Raw).Trim()-ne $expected){throw 'Bundled single-executable player differs from the editor. Rebuild the Windows player runtime.'}
 }
 Write-Host "Verified matching editor/player runtime. Maintained source: $SourceRoot";exit 0
}
if(Get-Process -Name 'ByteEngine.Editor','ByteEngine' -ErrorAction SilentlyContinue){throw 'Save and close ByteEngine before rebuilding. No editor process was terminated.'}
function CopyPluginPackage([string]$source,[string]$destination){
 if(Test-Path -LiteralPath $destination){
  if((Get-FileHash -LiteralPath $source).Hash -eq (Get-FileHash -LiteralPath $destination).Hash){return}
 }
 try{Copy-Item -LiteralPath $source -Destination $destination -Force}
 catch [System.IO.IOException]{
  if(!(Test-Path -LiteralPath $destination)){throw}
  # Windows may retain a read mapping after plugin inspection. Keep the old
  # package recoverable and put the new package at the canonical path.
  $previous=$destination+'.previous-'+[guid]::NewGuid().ToString('N')
  Move-Item -LiteralPath $destination -Destination $previous
  try{Copy-Item -LiteralPath $source -Destination $destination -Force}
  catch{Move-Item -LiteralPath $previous -Destination $destination;throw}
 }
}
function Build([string]$project,[string]$configuration){
 & dotnet build (Join-Path $SourceRoot $project) -c $configuration -m:1 -nodeReuse:false --nologo
 if($LASTEXITCODE-ne 0){throw "Build failed: $project ($configuration)"}
}
function SyncFolder([string]$source,[string]$destination){
 $source=[IO.Path]::GetFullPath($source);$destination=[IO.Path]::GetFullPath($destination)
 if($source-eq $destination){return}
 if(!$destination.StartsWith($InstallRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw "Install target outside the requested engine: $destination"}
 New-Item -ItemType Directory -Path $destination -Force | Out-Null
 foreach($item in Get-ChildItem -LiteralPath $source -Force){if(!$syncBrowser-and $item.Name-eq 'BrowserRuntime'-and(Test-Path -LiteralPath (Join-Path $destination 'BrowserRuntime'))){continue};Copy-Item -LiteralPath $item.FullName -Destination $destination -Recurse -Force}
 if(!$syncBrowser){return}
 # A copy-only install leaves old fingerprinted WebAssembly binaries behind.
 # Only prune direct files in the generated browser framework folder.
 $relativeFramework=if([IO.Path]::GetFileName($source)-eq 'BrowserRuntime'){'_framework'}else{'BrowserRuntime/_framework'}
 $sourceFramework=Join-Path $source $relativeFramework;$targetFramework=[IO.Path]::GetFullPath((Join-Path $destination $relativeFramework))
 if((Test-Path -LiteralPath $sourceFramework)-and(Test-Path -LiteralPath $targetFramework)){
  if(!$targetFramework.StartsWith($InstallRoot+'\',[StringComparison]::OrdinalIgnoreCase)-or[IO.Path]::GetFileName($targetFramework)-ne '_framework'){throw "Unsafe generated browser target: $targetFramework"}
  $currentFiles=@(Get-ChildItem -LiteralPath $sourceFramework -File | ForEach-Object Name)
  foreach($old in Get-ChildItem -LiteralPath $targetFramework -File){if($old.Name-notin $currentFiles){Remove-Item -LiteralPath $old.FullName -Force}}
 }
}
Push-Location $SourceRoot
try{
 if($BrowserOnly){
  & (Join-Path $SourceRoot 'Tools/PublishBrowser.ps1') -Configuration Release
  if($LASTEXITCODE-ne 0){throw 'Browser runtime refresh failed.'}
  $browser=Join-Path $SourceRoot 'Dist/ByteEngine/BrowserRuntime'
  foreach($relative in @('Dist/ByteEngine/BrowserRuntime','Editor/ByteEngine.Editor/bin/Debug/net9.0-windows/win-x64/BrowserRuntime','Editor/ByteEngine.Editor/bin/Release/net9.0-windows/win-x64/BrowserRuntime')){SyncFolder $browser (Join-Path $InstallRoot $relative)}
  & (Join-Path $SourceRoot 'Tools/SyncBrowserPluginPackages.ps1') -SourceRoot $SourceRoot -InstallRoot $InstallRoot -ProjectFile $ProjectFile
  Write-Host 'SUCCESS: browser runtime and matching project plugin packages refreshed. No game was exported.' -ForegroundColor Green
  return
 }
 $buildBrowser=$IncludeBrowser -and !$SkipBrowser
 Write-Host "Building current source: $SourceRoot"
 Write-Host "Installing editor and export runtimes: $InstallRoot"
 Build 'Plugins/KarlsonDemo/KarlsonDemo.Plugin.csproj' 'Release'
 $package=Join-Path $SourceRoot 'artifacts/plugins/KarlsonDemo.byteplugin'
 $library=Join-Path $SourceRoot 'PluginLibrary';New-Item -ItemType Directory -Path $library -Force | Out-Null
 CopyPluginPackage $package (Join-Path $library 'KarlsonDemo.byteplugin')
 Build 'Editor/ByteEngine.Editor/ByteEngine.Editor.csproj' 'Debug'
 & (Join-Path $SourceRoot 'Tools/PublishPlayer.ps1') -Configuration Debug
 if($LASTEXITCODE-ne 0){throw 'Debug player refresh failed.'}
 & (Join-Path $SourceRoot 'Tools/PublishEditor.ps1') -Configuration $BuildConfiguration -SkipBrowser:(!$buildBrowser) -IncludeBrowser:$buildBrowser
 if($LASTEXITCODE-ne 0){throw 'Editor distribution refresh failed.'}
 # Publish the selected distribution and preserve its correctly paired player runtime.
 SyncFolder (Join-Path $SourceRoot 'Dist/ByteEngine') (Join-Path $InstallRoot 'Dist/ByteEngine')
 foreach($configName in @('Debug','Release')){
  $relative="Editor/ByteEngine.Editor/bin/$configName/net9.0-windows/win-x64"
  $source=Join-Path $SourceRoot $relative
  if(Test-Path -LiteralPath (Join-Path $source 'ByteEngine.Editor.dll')){SyncFolder $source (Join-Path $InstallRoot $relative)}
 }
 foreach($folder in @((Join-Path $InstallRoot 'PluginLibrary'),(Join-Path $InstallRoot 'Dist/ByteEngine/PluginLibrary'),(Join-Path $InstallRoot 'Editor/ByteEngine.Editor/bin/Debug/net9.0-windows/win-x64/PluginLibrary'))){
  New-Item -ItemType Directory -Path $folder -Force | Out-Null;CopyPluginPackage $package (Join-Path $folder 'KarlsonDemo.byteplugin')
 }
 if($ProjectFile){
  if(!(Test-Path -LiteralPath $ProjectFile)){throw "Project not found: $ProjectFile"}
  $gameRoot=Split-Path -Parent ([IO.Path]::GetFullPath($ProjectFile))
  foreach($name in @('Plugins','PluginLibrary')){$to=Join-Path $gameRoot $name;New-Item -ItemType Directory -Path $to -Force | Out-Null;CopyPluginPackage $package (Join-Path $to 'KarlsonDemo.byteplugin')}
 }
 if($buildBrowser){& (Join-Path $SourceRoot 'Tools/SyncBrowserPluginPackages.ps1') -SourceRoot $SourceRoot -InstallRoot $InstallRoot -ProjectFile $ProjectFile}
 $dist=Join-Path $InstallRoot 'Dist/ByteEngine'
 foreach($required in @('ByteEngine.Editor.exe','ByteEngine.Core.dll','PlayerRuntime/ByteEngine.Player.exe','PlayerRuntime/ByteEngine.Launcher.exe','PlayerRuntime/ByteEngine.Core.dll','PlayerRuntime/openal32.dll')){if(!(Test-Path -LiteralPath (Join-Path $dist $required))){throw "Missing rebuilt file: $required"}}
 $editorHash=(Get-FileHash -LiteralPath (Join-Path $dist 'ByteEngine.Core.dll')).Hash
 $playerHash=(Get-FileHash -LiteralPath (Join-Path $dist 'PlayerRuntime/ByteEngine.Core.dll')).Hash
 if($editorHash-ne $playerHash){throw 'Editor and export runtime are different builds. Refusing to report a successful refresh.'}
 $report=[ordered]@{SourceRoot=$SourceRoot;InstallRoot=$InstallRoot;Configuration=$BuildConfiguration;CoreSHA256=$editorHash;PluginSHA256=(Get-FileHash -LiteralPath $package).Hash;CompletedUtc=[DateTime]::UtcNow.ToString('o')}
 $report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $dist 'build-identity.json') -Encoding UTF8
 Write-Host 'SUCCESS: editor, Windows export runtime and gameplay plugin are synchronized. No game was exported.' -ForegroundColor Green
 if(!$buildBrowser){Write-Host 'Web runtime was intentionally not rebuilt. Use -BrowserOnly when you need to update web gameplay.'}
}finally{Pop-Location}
