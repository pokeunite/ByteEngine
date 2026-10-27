$ErrorActionPreference='Stop'
if(Get-Process ByteEngine -ErrorAction SilentlyContinue){throw 'Close ByteEngine before installing.'}
$projectRoot='C:/Users/codex/Documents/palclone'
$engineRoot='C:/Users/codex/ByteEngine'
$backup=Join-Path $projectRoot ('.byteengine/Backups/reference-refinery-assets-'+(Get-Date -Format yyyyMMdd-HHmmss))
New-Item -ItemType Directory -Force $backup | Out-Null
foreach($relative in @('Assets/Models/Mining/Refinery.glb','SourceArt/MiningMachines/Refinery.blend','Assets/UI/Mining/Artwork/refinery-model.png')){
 $target=Join-Path $projectRoot $relative
 if(Test-Path -LiteralPath $target){Copy-Item -LiteralPath $target -Destination (Join-Path $backup ([IO.Path]::GetFileName($target)))}
}
Copy-Item -LiteralPath artifacts/reference-refinery/Refinery.glb -Destination "$projectRoot/Assets/Models/Mining/Refinery.glb"
Copy-Item -LiteralPath artifacts/reference-refinery/Refinery.blend -Destination "$projectRoot/SourceArt/MiningMachines/Refinery.blend"
Copy-Item -LiteralPath artifacts/reference-refinery/Refinery-preview.png0001.png -Destination "$projectRoot/Assets/UI/Mining/Artwork/refinery-model.png"
# Editor-only cursor lease fix, in addition to the plugin/core installation.
$editorImage='Editor/ByteEngine.Editor/bin/Debug/net9.0-windows/win-x64/ByteEngine.Editor.dll'
$index=0
foreach($target in @("$engineRoot/Dist/ByteEngine/ByteEngine.Editor.dll","$engineRoot/Editor/ByteEngine.Editor/bin/Debug/net9.0-windows/win-x64/ByteEngine.Editor.dll")){
 if(Test-Path -LiteralPath $target){Copy-Item -LiteralPath $target -Destination (Join-Path $backup "Editor-$index.dll")}
 Copy-Item -LiteralPath $editorImage -Destination $target;$index++
}
Write-Output "Refinery and editor cursor fix installed; asset GUID retained. Backup $backup"
