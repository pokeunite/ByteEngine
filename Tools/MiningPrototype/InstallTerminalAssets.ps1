$ErrorActionPreference='Stop'
if(Get-Process ByteEngine -ErrorAction SilentlyContinue){throw 'Close ByteEngine before assigning the new models.'}
$gameRoot='C:/Users/codex/Documents/palclone'
$modelRoot=Join-Path $gameRoot 'Assets/Models/Mining'
$sourceRoot=Join-Path $gameRoot 'SourceArt/MiningMachines'
New-Item -ItemType Directory -Force $modelRoot,$sourceRoot | Out-Null
$backup=Join-Path $gameRoot ('.byteengine/Backups/company-terminal-assets-'+(Get-Date -Format yyyyMMdd-HHmmss))
New-Item -ItemType Directory -Force $backup | Out-Null
foreach($pair in @(@('artifacts/reference-terminal/CompanyTerminal.glb',"$modelRoot/CompanyTerminal.glb"),@('artifacts/reference-terminal/CompanyTerminal.blend',"$sourceRoot/CompanyTerminal.blend"),@('artifacts/reference-terminal/CompanyTerminal-preview.png0001.png',"$sourceRoot/CompanyTerminal-preview.png"))){
 if(Test-Path -LiteralPath $pair[1]){Copy-Item -LiteralPath $pair[1] -Destination (Join-Path $backup ([IO.Path]::GetFileName($pair[1])))}
 Copy-Item -LiteralPath $pair[0] -Destination $pair[1]
}
$meta="$modelRoot/CompanyTerminal.glb.meta"
if(!(Test-Path -LiteralPath $meta)){@{guid=[guid]::NewGuid().ToString();type='Model3D'} | ConvertTo-Json | Set-Content -LiteralPath $meta}
Copy-Item -LiteralPath Docs/Concepts/AlienMining/CompanyTerminal-approved.png -Destination "$sourceRoot/CompanyTerminal-Reference.png"
Write-Output "Company terminal assets installed. Backup $backup"
