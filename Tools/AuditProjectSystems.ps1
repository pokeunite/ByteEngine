param(
 [string[]]$Projects = @('C:/Users/codex/Documents/DuneCompany','C:/Users/codex/Documents/Headwater'),
 [string]$OutputPath = ''
)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.IO.Compression.FileSystem
function PackageInfo([string]$path) {
 $zip=[IO.Compression.ZipFile]::OpenRead($path)
 try {
  $manifest=$zip.Entries | Where-Object {$_.FullName -eq 'plugin.json'} | Select-Object -First 1
  if(!$manifest){throw "Missing plugin.json: $path"}
  $reader=[IO.StreamReader]::new($manifest.Open());try {$m=$reader.ReadToEnd()|ConvertFrom-Json}finally{$reader.Dispose()}
  $dlls=@{}
  foreach($entry in $zip.Entries | Where-Object {$_.FullName -like '*.dll'}) {
   $stream=$entry.Open();try {$dlls[$entry.FullName]=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))}finally{$stream.Dispose()}
  }
  [pscustomobject]@{Path=$path;Id=$m.id;Version=$m.version;Assemblies=$dlls}
 } finally {$zip.Dispose()}
}
$library=@(Get-ChildItem "$repo/PluginLibrary" -Filter *.byteplugin | ForEach-Object {PackageInfo $_.FullName})
$coreText=(Get-ChildItem "$repo/Engine/ByteEngine.Core" -Filter *.cs -File -Recurse | Where-Object {$_.FullName -notmatch '[\\/](bin|obj)[\\/]'} | ForEach-Object {Get-Content -LiteralPath $_.FullName -Raw}) -join "`n"
$metadata=Get-Content "$repo/Editor/ByteEngine.Editor/ComponentMetadataRegistry.cs" -Raw
function ComponentTypes($node) {
 if($null -eq $node){return}
 if($node -is [array]){foreach($child in $node){ComponentTypes $child};return}
 if($node -is [pscustomobject]) {
  if($node.PSObject.Properties['components']){foreach($component in $node.components){if($component.type){[string]$component.type}}}
  foreach($property in $node.PSObject.Properties){if($property.Name -ne 'components'){ComponentTypes $property.Value}}
 }
}
$results=foreach($project in $Projects) {
 if(!(Test-Path -LiteralPath $project)){[pscustomobject]@{Project=$project;Status='Missing project'};continue}
 $sources=foreach($dir in Get-ChildItem "$project/PluginSource" -Directory -ErrorAction SilentlyContinue) {
  $canonical=Join-Path "$repo/Plugins" $dir.Name
  $differences=@()
  foreach($file in Get-ChildItem $dir.FullName -File -Recurse | Where-Object {$_.FullName -notmatch '[\\/](bin|obj)[\\/]'}) {
   $relative=[IO.Path]::GetRelativePath($dir.FullName,$file.FullName);$target=Join-Path $canonical $relative
   if(!(Test-Path -LiteralPath $target)){$differences+="Missing repository source: $relative"}
   elseif((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $target).Hash){$differences+="Different source: $relative"}
  }
  if(Test-Path -LiteralPath $canonical){
   foreach($file in Get-ChildItem $canonical -File -Recurse | Where-Object {$_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -in '.cs','.csproj','.json','.targets'}){
    $relative=[IO.Path]::GetRelativePath($canonical,$file.FullName)
    if(!(Test-Path -LiteralPath (Join-Path $dir.FullName $relative))){$differences+="Missing project source: $relative"}
   }
  }
  [pscustomobject]@{Plugin=$dir.Name;CanonicalSource=$canonical;Matches=($differences.Count -eq 0);Differences=$differences}
 }
 $packages=foreach($file in Get-ChildItem "$project/Plugins" -Filter *.byteplugin -ErrorAction SilentlyContinue) {
  $installed=PackageInfo $file.FullName;$canonical=$library|Where-Object Id -eq $installed.Id|Select-Object -First 1
  $matches=$null -ne $canonical -and $canonical.Version -eq $installed.Version
  if($matches){foreach($key in $installed.Assemblies.Keys){if($canonical.Assemblies[$key] -ne $installed.Assemblies[$key]){$matches=$false}};if($canonical.Assemblies.Count -ne $installed.Assemblies.Count){$matches=$false}}
  [pscustomobject]@{Id=$installed.Id;Version=$installed.Version;MatchesRepository=$matches;Installed=$file.FullName;Repository=$canonical.Path}
 }
 $types=@(Get-ChildItem @("$project/Scenes","$project/Assets/Blueprints") -Recurse -File -Include *.bytescene,*.byteblueprint -ErrorAction SilentlyContinue | Where-Object {$_.FullName -notmatch '[\\/](\.byteengine|bin|obj|Builds|BrowserRuntime|PlayerRuntime)[\\/]'} | ForEach-Object {ComponentTypes (Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json -Depth 100)} | Sort-Object -Unique)
 $native=foreach($type in $types | Where-Object {$_ -notmatch '\.' -and $_ -ne 'Transform'}) {
  $escaped=[regex]::Escape($type)
  [pscustomobject]@{Type=$type;SourceExists=($coreText -match "\b(class|struct)\s+$escaped\b");AddComponentEntry=($metadata -match "typeof\([^)]*\b$escaped\)")}
 }
 $badNative=@($native|Where-Object {!$_.SourceExists -or !$_.AddComponentEntry})
 [pscustomobject]@{Project=$project;Source=@($sources);Packages=@($packages);NativeComponents=@($native);Status=if(@($sources|Where-Object {!$_.Matches}).Count+@($packages|Where-Object {!$_.MatchesRepository}).Count+$badNative.Count -eq 0){'Pass'}else{'Mismatch'}}
}
$json=ConvertTo-Json -InputObject @($results) -Depth 12
if($OutputPath){$json|Set-Content -LiteralPath $OutputPath -Encoding utf8}
$json
if(@($results|Where-Object Status -ne 'Pass').Count){exit 1}
