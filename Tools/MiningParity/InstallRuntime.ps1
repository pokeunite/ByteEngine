$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source=Join-Path $repo 'Player/ByteEngine.Browser/bin/Release/net9.0/publish/wwwroot'
if(!(Test-Path -LiteralPath (Join-Path $source 'browser-plugin-builds.json'))){throw 'Publish the browser runtime first.'}
$engine='C:/Users/codex/ByteEngine'
foreach($base in @($repo,$engine)){
 foreach($relative in @('Dist/ByteEngine/BrowserRuntime','Editor/ByteEngine.Editor/bin/Debug/net9.0-windows/win-x64/BrowserRuntime','Editor/ByteEngine.Editor/bin/Release/net9.0-windows/win-x64/BrowserRuntime')){
  $target=[IO.Path]::GetFullPath((Join-Path $base $relative))
  if(!$target.StartsWith([IO.Path]::GetFullPath($base)+'\',[StringComparison]::OrdinalIgnoreCase)-or[IO.Path]::GetFileName($target)-ne'BrowserRuntime'){throw "Unsafe target $target"}
  if(Test-Path -LiteralPath $target){Remove-Item -LiteralPath $target -Recurse -Force}
  New-Item -ItemType Directory -Path $target -Force|Out-Null
  Get-ChildItem -LiteralPath $source -Force|Copy-Item -Destination $target -Recurse -Force
 }
}
$canonical=Join-Path $repo 'Plugins/KarlsonDemo'
$project='C:/Users/codex/Documents/palclone/PluginSource/KarlsonDemo'
foreach($file in Get-ChildItem -LiteralPath $canonical -Recurse -File){
 $relative=$file.FullName.Substring($canonical.Length+1)
 if($relative -match '(^|[\\/])(bin|obj)([\\/]|$)'){continue}
 $target=Join-Path $project $relative
 New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force|Out-Null
 Copy-Item -LiteralPath $file.FullName -Destination $target -Force
}
Write-Host 'Browser runtimes and project plugin source synchronized after both publishers completed.'
