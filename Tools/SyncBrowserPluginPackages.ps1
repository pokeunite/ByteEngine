[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][string]$SourceRoot,
 [string]$InstallRoot='',
 [string]$ProjectFile='',
 [string]$RuntimeDirectory=''
)
$ErrorActionPreference='Stop'
$SourceRoot=[IO.Path]::GetFullPath($SourceRoot)
if(!$InstallRoot){$InstallRoot=$SourceRoot}
$InstallRoot=[IO.Path]::GetFullPath($InstallRoot)
if($ProjectFile-and!(Test-Path -LiteralPath $ProjectFile)){throw "Project file not found: $ProjectFile"}
if(!$RuntimeDirectory){$RuntimeDirectory=Join-Path $SourceRoot 'Dist/ByteEngine/BrowserRuntime'}
$buildFile=Join-Path $RuntimeDirectory 'browser-plugin-builds.json'
if(!(Test-Path -LiteralPath $buildFile)){throw "Browser plugin build manifest is missing: $buildFile"}
Add-Type -AssemblyName System.IO.Compression.FileSystem
function ReadPackage([string]$path){
 $zip=[IO.Compression.ZipFile]::OpenRead($path)
 try{
  $entry=$zip.GetEntry('plugin.json');if(!$entry){throw "Plugin package has no manifest: $path"}
  $reader=[IO.StreamReader]::new($entry.Open())
  try{$manifest=$reader.ReadToEnd()|ConvertFrom-Json}finally{$reader.Dispose()}
  $assembly=$zip.GetEntry($manifest.assembly);if(!$assembly){throw "Plugin assembly missing: $path"}
  $stream=$assembly.Open();$hasher=[Security.Cryptography.SHA256]::Create()
  try{$hash=[BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$hasher.Dispose()}
  return [pscustomobject]@{Id=$manifest.id;Version=$manifest.version;Hash=$hash}
 }finally{$zip.Dispose()}
}
function CopyPackage([string]$source,[string]$target){
 New-Item -ItemType Directory -Force (Split-Path -Parent $target)|Out-Null
 if((Test-Path -LiteralPath $target)-and(Get-FileHash -LiteralPath $source).Hash-eq(Get-FileHash -LiteralPath $target).Hash){return}
 try{Copy-Item -LiteralPath $source -Destination $target -Force}
 catch [IO.IOException]{
  if(!(Test-Path -LiteralPath $target)){throw}
  $backup=$target+'.previous-'+[guid]::NewGuid().ToString('N');Move-Item -LiteralPath $target -Destination $backup
  try{Copy-Item -LiteralPath $source -Destination $target -Force}catch{if(Test-Path -LiteralPath $target){Remove-Item -LiteralPath $target -Force};Move-Item -LiteralPath $backup -Destination $target;throw}
 }
}
$builds=Get-Content -LiteralPath $buildFile -Raw|ConvertFrom-Json
$definitions=@{}
foreach($directory in Get-ChildItem -LiteralPath (Join-Path $SourceRoot 'Plugins') -Directory){
 $manifestFile=Join-Path $directory.FullName 'plugin.json';if(!(Test-Path -LiteralPath $manifestFile)){continue}
 $manifest=Get-Content -LiteralPath $manifestFile -Raw|ConvertFrom-Json
 if($definitions.ContainsKey($manifest.id)){throw "Duplicate plugin source id: $($manifest.id)"}
 $definitions[$manifest.id]=$directory.Name
}
$plans=@()
# Validate every candidate before touching any installed plugin. Prefer the
# immutable input archive, since a subsequent desktop build can change bin/.
foreach($entry in $builds.PSObject.Properties){
 $id=$entry.Name;$build=$entry.Value
 if(!$definitions.ContainsKey($id)){throw "No source package is registered for browser plugin: $id"}
 if($build.sha256-notmatch'^[A-Fa-f0-9]{64}$'){throw "Invalid plugin fingerprint: $id"}
 $name=$definitions[$id]
 $archive=Join-Path $SourceRoot "artifacts/browser-plugin-inputs/$id/$($build.sha256.ToUpperInvariant()).byteplugin"
 $candidate=$archive
 if(!(Test-Path -LiteralPath $candidate)){$candidate=Join-Path $SourceRoot "artifacts/plugins/$name.byteplugin"}
 if(!(Test-Path -LiteralPath $candidate)){throw "Matching package for $id is missing. Rebuild the browser runtime; no installed packages were changed."}
 $actual=ReadPackage $candidate
 if($actual.Id-ne$id-or$actual.Version-ne$build.version-or$actual.Hash-ne$build.sha256){throw "Built package does not match the linked browser input: $id. Rebuild the browser runtime; no installed packages were changed."}
 $plans+=,[pscustomobject]@{Id=$id;Name=$name;Source=$candidate;Archive=$archive;Hash=$actual.Hash}
}
$engineRoots=@($SourceRoot,$InstallRoot)|Select-Object -Unique
foreach($plan in $plans){
 CopyPackage $plan.Source $plan.Archive
 foreach($root in $engineRoots){
  foreach($relative in @('PluginLibrary','Dist/ByteEngine/PluginLibrary','Dist/ByteEngine-BuildDebug/PluginLibrary','Editor/ByteEngine.Editor/bin/Debug/net9.0-windows/win-x64/PluginLibrary','Editor/ByteEngine.Editor/bin/Release/net9.0-windows/win-x64/PluginLibrary')){
   $target=Join-Path $root "$relative/$($plan.Name).byteplugin";CopyPackage $plan.Archive $target
   if((ReadPackage $target).Hash-ne$plan.Hash){throw "Installed browser plugin verification failed: $target"}
  }
 }
 if($ProjectFile){
  if(!(Test-Path -LiteralPath $ProjectFile)){throw "Project file not found: $ProjectFile"}
  $projectRoot=Split-Path -Parent ([IO.Path]::GetFullPath($ProjectFile))
  # Refresh only plugins already present; keep enable/disable state sidecars.
  foreach($folder in @('Plugins','PluginLibrary')){
   $target=Join-Path $projectRoot "$folder/$($plan.Name).byteplugin"
   if(Test-Path -LiteralPath $target){CopyPackage $plan.Archive $target;if((ReadPackage $target).Hash-ne$plan.Hash){throw "Project browser plugin verification failed: $target"}}
  }
 }
 Write-Host "Verified matching browser plugin package: $($plan.Id)"
}
