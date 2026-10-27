param([switch]$IncludeModels)
$ErrorActionPreference='Stop'
if(Get-Process ByteEngine -ErrorAction SilentlyContinue){throw 'Close ByteEngine before installing.'}
$engineRoot='C:/Users/codex/ByteEngine'
$gameRoot='C:/Users/codex/Documents/palclone'
$stamp=Get-Date -Format yyyyMMdd-HHmmss
$backup=Join-Path $engineRoot "UpdateBackups/mining-ui-$stamp"
New-Item -ItemType Directory -Force $backup | Out-Null
$coreTargets=@('Dist/ByteEngine/ByteEngine.Core.dll','Dist/ByteEngine/PlayerRuntime/ByteEngine.Core.dll','Editor/ByteEngine.Editor/bin/Debug/net9.0-windows/win-x64/ByteEngine.Core.dll','Editor/ByteEngine.Editor/bin/Debug/net9.0-windows/win-x64/PlayerRuntime/ByteEngine.Core.dll')
for($i=0;$i -lt $coreTargets.Count;$i++){
 $target=Join-Path $engineRoot $coreTargets[$i]
 if(Test-Path $target){Copy-Item -LiteralPath $target -Destination (Join-Path $backup "Core-$i.dll");Copy-Item -LiteralPath 'Engine/ByteEngine.Core/bin/Release/net9.0/ByteEngine.Core.dll' -Destination $target}
}
$package='artifacts/plugins/KarlsonDemo.byteplugin'
$targets=@('PluginLibrary/KarlsonDemo.byteplugin',"$gameRoot/Plugins/KarlsonDemo.byteplugin","$gameRoot/PluginLibrary/KarlsonDemo.byteplugin","$engineRoot/PluginLibrary/KarlsonDemo.byteplugin","$engineRoot/Dist/ByteEngine/PluginLibrary/KarlsonDemo.byteplugin","$engineRoot/Editor/ByteEngine.Editor/bin/Debug/net9.0-windows/win-x64/PluginLibrary/KarlsonDemo.byteplugin")
for($i=0;$i -lt $targets.Count;$i++){$target=$targets[$i];New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null;if(Test-Path $target){Copy-Item -LiteralPath $target -Destination (Join-Path $backup "Plugin-$i.byteplugin")};Copy-Item -LiteralPath $package -Destination $target}
$fontRoot=Join-Path $gameRoot 'Assets/UI/Mining/Fonts'
New-Item -ItemType Directory -Force $fontRoot | Out-Null
Copy-Item artifacts/mining-ui-fonts/* -Destination $fontRoot
Get-ChildItem -LiteralPath $fontRoot -File | Where-Object {$_.Extension -ne '.meta'} | ForEach-Object {$meta=$_.FullName+'.meta';if(!(Test-Path $meta)){@{guid=[guid]::NewGuid().ToString();type=$(if($_.Extension -eq '.ttf'){'Font'}else{'Unknown'})}|ConvertTo-Json|Set-Content -LiteralPath $meta}}
$artRoot=Join-Path $gameRoot 'Assets/UI/Mining/Artwork'
New-Item -ItemType Directory -Force $artRoot | Out-Null
Copy-Item Docs/Concepts/AlienMining/UIAssets/*.png -Destination $artRoot
Copy-Item Plugins/KarlsonDemo/Samples/Hud/Art/research-*.png -Destination $artRoot
$modelRoot=Join-Path $gameRoot 'Assets/Models/Mining'
$blendRoot=Join-Path $gameRoot 'SourceArt/MiningMachines'
New-Item -ItemType Directory -Force $modelRoot,$blendRoot | Out-Null
if($IncludeModels){
 Copy-Item artifacts/mining-models/*.glb -Destination $modelRoot
 Copy-Item artifacts/mining-models/*.blend -Destination $blendRoot
 foreach($pair in @(@('MiningDrill','drill-model'),@('AmmoPress','press-model'),@('Refinery','refinery-model'),@('CompanyTent','tent-model'))){Copy-Item -LiteralPath "artifacts/mining-models/$($pair[0])-preview.png" -Destination "$artRoot/$($pair[1]).png"}
}
Get-ChildItem -LiteralPath $modelRoot -Filter *.glb | ForEach-Object {$meta=$_.FullName+'.meta';if(!(Test-Path $meta)){@{guid=[guid]::NewGuid().ToString();type='Model3D'}|ConvertTo-Json|Set-Content -LiteralPath $meta}}
Get-ChildItem -LiteralPath $artRoot -Filter *.png | ForEach-Object {$meta=$_.FullName+'.meta';if(!(Test-Path $meta)){@{guid=[guid]::NewGuid().ToString();type='Texture2D'}|ConvertTo-Json|Set-Content -LiteralPath $meta}}
$source=Join-Path (Get-Location) 'Plugins/KarlsonDemo'
Get-ChildItem -LiteralPath $source -Recurse -File | Where-Object {$_.FullName -notmatch '[\\/](bin|obj)[\\/]'} | ForEach-Object {
 $relative=[IO.Path]::GetRelativePath($source,$_.FullName);$target=Join-Path "$gameRoot/PluginSource/KarlsonDemo" $relative
 New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null;Copy-Item -LiteralPath $_.FullName -Destination $target
}
Write-Output "Installed Modular Weapon Gameplay 3.33.0. Backup: $backup"
