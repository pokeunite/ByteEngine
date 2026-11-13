#requires -Version 5.1
<#
.SYNOPSIS
    Paired performance comparison with a bootstrap confidence interval.

.DESCRIPTION
    Implements the 2026-11-09 measurement protocol: per-run medians, paired
    improvements over index-matched baseline/candidate runs, a percentile
    bootstrap over pairs, and a pre-registered gate (CI lower bound > 0).
    Runs must be interleaved (ABBA/BAAB) by Measure-Performance.ps1 so drift
    is spread across both arms instead of biasing one.

.EXAMPLE
    ./Aggregate-Performance.ps1 -Baseline 'a/1.json','a/2.json' -Candidate 'b/1.json','b/2.json'
#>
param(
    [Parameter(Mandatory)][string[]]$Baseline,
    [Parameter(Mandatory)][string[]]$Candidate,
    [string]$Metric = 'meanMs',
    [int]$Seeds = 4000,
    [double]$Gate = 0,
    [string]$Output
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Read-Run([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Missing run artifact: $Path" }
    $json = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    foreach ($required in 'meanMs', 'medianMs', 'p95Ms', 'fps') {
        if ($null -eq $json.PSObject.Properties[$required]) { throw "$Path is not a stress artifact (no $required)" }
    }
    return $json
}
function Median([double[]]$Values) {
    if ($Values.Count -eq 0) { return 0 }
    $sorted = $Values | Sort-Object
    $middle = [int][Math]::Floor($sorted.Count / 2)
    if ($sorted.Count % 2 -eq 1) { return $sorted[$middle] }
    return ($sorted[$middle - 1] + $sorted[$middle]) / 2
}
function Stat([object[]]$Runs, [string]$Name) {
    $values = @($Runs | ForEach-Object { [double]$_.$Name })
    [pscustomobject]@{ Metric = $Name; Median = [Math]::Round((Median $values), 3); Min = [Math]::Round(($values | Measure-Object -Minimum).Minimum, 3); Max = [Math]::Round(($values | Measure-Object -Maximum).Maximum, 3) }
}

$base = @($Baseline | ForEach-Object { Read-Run $_ })
$cand = @($Candidate | ForEach-Object { Read-Run $_ })
if ($base.Count -ne $cand.Count) { throw "Paired comparison needs equal run counts (baseline $($base.Count), candidate $($cand.Count))" }
if ($base.Count -lt 2) { throw 'At least two paired runs are required; one run cannot separate an effect from run-to-run drift' }

# Paired relative improvement per index: positive means the candidate is faster.
$deltas = for ($i = 0; $i -lt $base.Count; $i++) {
    $b = [double]$base[$i].$Metric; $c = [double]$cand[$i].$Metric
    if ($b -le 0) { throw "Non-positive baseline $Metric in run $i" }
    ($b - $c) / $b
}

$random = [Random]::new(20261113)
$bootstrap = [double[]]::new($Seeds)
for ($seed = 0; $seed -lt $Seeds; $seed++) {
    $sum = 0.0
    for ($pick = 0; $pick -lt $deltas.Count; $pick++) { $sum += $deltas[$random.Next($deltas.Count)] }
    $bootstrap[$seed] = $sum / $deltas.Count
}
$sortedBoot = $bootstrap | Sort-Object
$low = $sortedBoot[[int][Math]::Floor($Seeds * 0.025)]
$high = $sortedBoot[[int][Math]::Floor($Seeds * 0.975)]
$meanImprovement = ($deltas | Measure-Object -Average).Average
$passed = $low -gt $Gate

$metricNames = 'meanMs', 'medianMs', 'p95Ms', 'fps', 'renderMeanMs', 'updateMeanMs', 'occlusionMs', 'allocatedPerFrame'
$baselineStats = @(foreach ($name in $metricNames) { Stat $base $name })
$candidateStats = @(foreach ($name in $metricNames) { Stat $cand $name })

$report = [pscustomobject]@{
    protocol      = 'paired bootstrap, 95% CI, gate = CI lower bound > 0'
    metric        = $Metric
    pairs         = $deltas.Count
    seeds         = $Seeds
    baseline      = $baselineStats
    candidate     = $candidateStats
    pairedDeltaPc = @($deltas | ForEach-Object { [Math]::Round($_ * 100, 3) })
    meanDeltaPc   = [Math]::Round($meanImprovement * 100, 3)
    ciLowPc       = [Math]::Round($low * 100, 3)
    ciHighPc      = [Math]::Round($high * 100, 3)
    passed        = $passed
}

$modeLabel = @{}
foreach ($group in @(@{ Name = 'baseline'; Runs = $base }, @{ Name = 'candidate'; Runs = $cand })) {
    $modeLabel[$group.Name] = $group.Runs
}
Write-Host ''
Write-Host ("{0,-22} {1,10} {2,10} {3,10} {4,10}" -f 'metric', 'base', 'cand', 'delta%', 'ci95')
foreach ($name in 'meanMs', 'medianMs', 'p95Ms', 'fps', 'renderMeanMs', 'updateMeanMs', 'occlusionMs', 'allocatedPerFrame') {
    $baseStat = $report.baseline | Where-Object Metric -eq $name | Select-Object -First 1
    $candStat = $report.candidate | Where-Object Metric -eq $name | Select-Object -First 1
    $delta = if ($baseStat.Median -ne 0) { [Math]::Round(($baseStat.Median - $candStat.Median) / $baseStat.Median * 100, 2) } else { 0 }
    Write-Host ("{0,-22} {1,10} {2,10} {3,10} {4,10}" -f $name, $baseStat.Median, $candStat.Median, $delta, '')
}
Write-Host ''
Write-Host ("paired {0}: {1:0.00}%  CI {2:0.00}% .. {3:0.00}%  gate {4}" -f $Metric, $report.meanDeltaPc, $report.ciLowPc, $report.ciHighPc, $(if ($passed) { 'PASS' } else { 'NOT PROVEN' }))

if ($Output) {
    $directory = Split-Path -Parent $Output
    if ($directory -and -not (Test-Path -LiteralPath $directory)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $Output -Encoding utf8
    Write-Host "Wrote $Output"
}
if (-not $passed) { Write-Host 'Effect is inside the noise floor or the runs were not interleaved; do not ship on this evidence.' }
$report
