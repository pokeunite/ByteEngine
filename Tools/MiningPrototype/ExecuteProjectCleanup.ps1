param([Parameter(Mandatory=$true)][string]$PlanPath)
$ErrorActionPreference = 'Stop'
$plan = Get-Content -LiteralPath $PlanPath -Raw | ConvertFrom-Json
$projectRoot = (Resolve-Path -LiteralPath 'C:\Users\codex\Documents\palclone').ProviderPath.TrimEnd('\')
if ($plan.root.TrimEnd('\') -ne $projectRoot) { throw 'Unexpected project root' }
if (Get-Process -Name 'ByteEngine*' -ErrorAction SilentlyContinue) { throw 'ByteEngine is open; cleanup has not started.' }
if ((Get-Item -LiteralPath $projectRoot -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Project root is a link' }
$prefix = $projectRoot + '\'
# Validate the complete deletion set before modifying anything. Do not follow links.
foreach ($entry in $plan.entries) {
 if ($plan.cachesOnly -and -not ($entry.relative.StartsWith('.byteengine/ImportCache/') -or $entry.relative.StartsWith('.byteengine/PluginCache/'))) { throw 'Cache-only plan contains a non-cache target' }
 $resolved = (Resolve-Path -LiteralPath $entry.path).ProviderPath
 if (-not $resolved.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw "Outside project: $resolved" }
 $item = Get-Item -LiteralPath $resolved -Force
 if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked target: $resolved" }
 $parent = if ($item.PSIsContainer) { $item.Parent } else { $item.Directory }
 while ($null -ne $parent -and $parent.FullName.Length -ge $projectRoot.Length) {
  if ($parent.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked ancestor: $resolved" }
  $parent = $parent.Parent
 }
 if ($item.PSIsContainer) {
  if (Get-ChildItem -LiteralPath $resolved -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint } | Select-Object -First 1) { throw "Link inside target: $resolved" }
 }
}
$deletedFiles = 0
foreach ($entry in $plan.entries) {
 $item = Get-Item -LiteralPath $entry.path -Force
 if ($item.PSIsContainer) { $deletedFiles += @(Get-ChildItem -LiteralPath $item.FullName -Recurse -Force -File).Count }
 else { $deletedFiles++ }
 Remove-Item -LiteralPath $item.FullName -Recurse -Force
}
$verified = 0
foreach ($property in $plan.protected.PSObject.Properties) {
 $path = Join-Path $projectRoot $property.Name
 if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Protected file missing: $path" }
 if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $property.Value) { throw "Protected file changed: $path" }
 $verified++
}
$remaining = (Get-ChildItem -LiteralPath $projectRoot -File -Recurse -Force | Measure-Object -Property Length -Sum).Sum
$result = [ordered]@{ Project=$projectRoot; BeforeBytes=$plan.beforeBytes; AfterBytes=$remaining; FreedBytes=($plan.beforeBytes-$remaining); DeletedFiles=$deletedFiles; RemovedTargets=$plan.entries.Count; VerifiedFiles=$verified; KeptRecoverySnapshots=$plan.retainedSnapshots; Completed=(Get-Date).ToString('s') }
$reportFolder = Split-Path -Parent $PlanPath
$result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $reportFolder 'result.json') -Encoding utf8
$rows = foreach ($entry in $plan.entries) { '<tr><td>'+[Net.WebUtility]::HtmlEncode($entry.relative)+'</td><td>'+[Math]::Round($entry.bytes/1MB,2)+'</td><td>'+[Net.WebUtility]::HtmlEncode($entry.reason)+'</td></tr>' }
$preserved = if ($plan.cachesOnly) { 'Removed only obsolete derived cache versions. Kept current caches and all backups, logs, source art, references, attribution and documentation.' } else { 'Kept current model caches, current plugin cache, five newest recovery snapshots and five newest diagnostic logs. Preserved original Blender art, references, attribution and documentation.' }
$html = '<!doctype html><meta charset="utf-8"><title>Hazard Pay cleanup</title><style>body{font:16px system-ui;max-width:1100px;margin:40px auto;background:#13171a;color:#eee}h1{color:#f2c329}table{border-collapse:collapse;width:100%}td,th{padding:10px;text-align:left;border-bottom:1px solid #40464b}p{line-height:1.6}</style><h1>Hazard Pay project cleanup</h1><p>Freed '+[Math]::Round($result.FreedBytes/1MB,1)+' MiB. Removed '+$deletedFiles+' redundant files. Verified '+$verified+' project, asset, scene, source and save files unchanged.</p><p>'+$preserved+'</p><table><tr><th>Removed path</th><th>MiB</th><th>Reason</th></tr>'+($rows -join '')+'</table>'
$report = Join-Path $projectRoot 'Documentation\Project-Cleanup-2026-11-05.html'
Set-Content -LiteralPath $report -Value $html -Encoding utf8
$result | ConvertTo-Json -Depth 4
Write-Output "Report: $report"
