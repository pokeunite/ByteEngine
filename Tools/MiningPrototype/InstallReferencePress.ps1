$ErrorActionPreference='Stop'
if(Get-Process ByteEngine -ErrorAction SilentlyContinue){throw 'Close ByteEngine before installing.'}
$projectRoot='C:/Users/codex/Documents/palclone'
$backup=Join-Path $projectRoot ('.byteengine/Backups/reference-press-assets-'+(Get-Date -Format yyyyMMdd-HHmmss))
New-Item -ItemType Directory -Force $backup | Out-Null
foreach($relative in @('Assets/Models/Mining/AmmoPress.glb','SourceArt/MiningMachines/AmmoPress.blend','Assets/UI/Mining/Artwork/press-model.png')){
 $target=Join-Path $projectRoot $relative
 if(Test-Path -LiteralPath $target){Copy-Item -LiteralPath $target -Destination (Join-Path $backup ([IO.Path]::GetFileName($target)))}
}
Copy-Item -LiteralPath artifacts/reference-press/AmmoPress.glb -Destination "$projectRoot/Assets/Models/Mining/AmmoPress.glb"
Copy-Item -LiteralPath artifacts/reference-press/AmmoPress.blend -Destination "$projectRoot/SourceArt/MiningMachines/AmmoPress.blend"
Copy-Item -LiteralPath artifacts/reference-press/AmmoPress-preview.png0001.png -Destination "$projectRoot/Assets/UI/Mining/Artwork/press-model.png"
Write-Output "Reference press installed; existing asset GUID retained. Backup: $backup"
