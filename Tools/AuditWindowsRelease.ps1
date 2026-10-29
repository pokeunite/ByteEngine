param([Parameter(Mandatory=$true)][string]$BuildDirectory,[string]$ReportPath='')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($BuildDirectory)
if(!(Test-Path -LiteralPath $root -PathType Container)){throw 'Build folder does not exist.'}
$files=@(Get-ChildItem -LiteralPath $root -Recurse -File)
$forbidden=@($files|Where-Object {$_.Extension -in @('.cs','.csproj','.sln','.pdb','.pfx','.key') -or $_.Name -match '^(ByteEngine\.Editor|ByteAssistant|\.env($|\.)|secrets\.json$|appsettings\.Development\.json$)' -or $_.FullName.Substring($root.Length) -match '[\\/](\.git|\.codex|PluginSource)[\\/]'})
$report=[ordered]@{Build=$root;Files=$files.Count;Bytes=($files|Measure-Object Length -Sum).Sum;PassedInventory=$forbidden.Count-eq 0;UnexpectedFiles=@($forbidden|ForEach-Object {$_.FullName.Substring($root.Length+1)});ManagedCoreVisible=(Test-Path -LiteralPath (Join-Path $root 'Runtime/ByteEngine.Core.dll'));Notes=@('Executable client code can be extracted or reverse-engineered.','This checks accidental release files, not arbitrary secrets embedded in authored content.','Bundling is packaging, not a confidentiality boundary.')}
$json=$report|ConvertTo-Json -Depth 4
if($ReportPath){[IO.File]::WriteAllText([IO.Path]::GetFullPath($ReportPath),$json)}
Write-Output $json
if($forbidden.Count){throw 'Release contains unexpected development or credential files.'}
