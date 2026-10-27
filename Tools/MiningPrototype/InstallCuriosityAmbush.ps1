$ErrorActionPreference='Stop'
if(Get-Process ByteEngine -ErrorAction SilentlyContinue){throw 'ByteEngine is open; save and close before installing.'}
$gameRoot='C:/Users/codex/Documents/palclone'
$engineRoot='C:/Users/codex/ByteEngine'
$backup=Join-Path $gameRoot ('.byteengine/Backups/ambush-plugin-'+(Get-Date -Format yyyyMMdd-HHmmss))
New-Item -ItemType Directory -Path $backup -Force | Out-Null
$targets=@('PluginLibrary/KarlsonDemo.byteplugin',"$gameRoot/Plugins/KarlsonDemo.byteplugin","$gameRoot/PluginLibrary/KarlsonDemo.byteplugin","$engineRoot/PluginLibrary/KarlsonDemo.byteplugin","$engineRoot/Dist/ByteEngine/PluginLibrary/KarlsonDemo.byteplugin","$engineRoot/Editor/ByteEngine.Editor/bin/Debug/net9.0-windows/win-x64/PluginLibrary/KarlsonDemo.byteplugin")
for($i=0;$i -lt $targets.Count;$i++){
 $destination=$targets[$i];New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
 if(Test-Path -LiteralPath $destination){Copy-Item -LiteralPath $destination -Destination (Join-Path $backup "Plugin-$i.byteplugin")}
 Copy-Item -LiteralPath 'artifacts/plugins/KarlsonDemo.byteplugin' -Destination $destination -Force
}
$source=(Resolve-Path 'Plugins/KarlsonDemo').Path
$destination=Join-Path $gameRoot 'PluginSource/KarlsonDemo'
Get-ChildItem -LiteralPath $source -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } | ForEach-Object {
 $relative=[IO.Path]::GetRelativePath($source,$_.FullName);$to=Join-Path $destination $relative
 New-Item -ItemType Directory -Path (Split-Path $to) -Force | Out-Null
 Copy-Item -LiteralPath $_.FullName -Destination $to -Force
}
Write-Output "Installed Modular Weapon Gameplay $((Get-Content Plugins/KarlsonDemo/plugin.json -Raw | ConvertFrom-Json).version). Backups: $backup"
