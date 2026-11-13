#requires -Version 5.1
<#
.SYNOPSIS
    Interleaved A/B performance runner for the spider stress benchmark.

.DESCRIPTION
    Repeats the stress scene N times per mode in ABBA/BAAB order so thermal and
    background drift land on both arms, then hands the artifacts to
    Aggregate-Performance.ps1 for paired medians and a bootstrap CI.

    Each repetition runs in its own directory because the app writes
    artifacts/spider-stress-*.json relative to its working directory.

.EXAMPLE
    ./Measure-Performance.ps1 -Reps 8 -Modes baseline,no-occlusion
.EXAMPLE
    ./Measure-Performance.ps1 -Reps 8 -Modes baseline,adaptive-open,baseline -DryRun
#>
param(
    [string]$Exe = 'Tools/MiningPrototype/bin/Release/net9.0/MiningPrototype.exe',
    [string[]]$Modes = @('baseline', 'no-occlusion'),
    [ValidateRange(2, 1000)][int]$Reps = 8,
    [int]$Count = 120,
    [string]$ExtraArgs = '',
    [string]$Output,
    [string]$Work = '',
    [int]$TimeoutSeconds = 900,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (-not $Output) { $Output = Join-Path $root ('.artifacts/perf-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
# The benchmark authors/loads its scene relative to the repository root, so it must run there.
if (-not $Work) { $Work = $root }
$exePath = if ([IO.Path]::IsPathRooted($Exe)) { $Exe } else { Join-Path $root $Exe }
$aggregate = Join-Path $PSScriptRoot 'Aggregate-Performance.ps1'

if ($Modes.Count -lt 2) { throw 'Pass at least two modes, for example baseline,no-occlusion' }
$quartet = $Modes.Count -eq 2
if ($quartet -and $Reps % 4 -ne 0) {
    $rounded = [int][Math]::Ceiling($Reps / 4.0) * 4
    Write-Host "Reps $Reps rounded to $rounded so every arm gets complete ABBA/BAAB quartets."
    $Reps = $rounded
}
if (-not $DryRun -and -not (Test-Path -LiteralPath $exePath)) {
    $found = Get-ChildItem -Path (Join-Path $root 'Tools/MiningPrototype/bin') -Recurse -Filter 'MiningPrototype.exe' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($found) { $exePath = $found.FullName; Write-Host "Discovered benchmark: $exePath" }
    else { throw "Benchmark executable not found under Tools/MiningPrototype/bin.`nBuild order: dotnet build Plugins/KarlsonDemo/KarlsonDemo.Plugin.csproj -c Release, then dotnet build Tools/MiningPrototype/MiningPrototype.csproj -c Release" }
}

function Get-Order([int]$rep) {
    if (-not $quartet) {
        # Block design: rotate the mode order so no mode always runs first.
        # Each cycle visits every mode exactly once, rotating which starts.
        return $Modes[([int][Math]::Floor($rep / $Modes.Count) + ($rep % $Modes.Count)) % $Modes.Count]
    }
    $first = $Modes[0]; $second = $Modes[1]
    $quartetIndex = [int][Math]::Floor($rep / 4)
    $position = $rep % 4
    if ($quartetIndex % 2 -eq 0) { return @($first, $second, $second, $first)[$position] }
    return @($second, $first, $first, $second)[$position]
}

$runs = @{}
foreach ($mode in $Modes) { $runs[$mode] = [System.Collections.Generic.List[string]]::new() }
$index = @{}
foreach ($mode in $Modes) { $index[$mode] = 0 }

$totalRuns = $Reps * $Modes.Count
for ($rep = 0; $rep -lt $totalRuns; $rep++) {
    $mode = Get-Order $rep
    $index[$mode]++
    $directory = Join-Path $Output ('{0}-{1:d2}' -f $mode, $index[$mode])
    $arguments = @('--stress-spiders', "--probe=$mode", "--count=$Count", '--skip-stress-screenshot')
    if ($ExtraArgs) { $arguments += $ExtraArgs.Split(' ', [StringSplitOptions]::RemoveEmptyEntries) }
    Write-Host ("[{0}/{1}] {2} -> {3}" -f ($rep + 1), $totalRuns, $mode, $directory)
    Write-Host ("    {0} {1}" -f $exePath, ($arguments -join ' '))
    if ($DryRun) { continue }

    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $started = Get-Date
    $process = Start-Process -FilePath $exePath -ArgumentList $arguments -WorkingDirectory $work -PassThru -NoNewWindow
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        try { $process.Kill($true) } catch { }
        throw "$mode repetition $($index[$mode]) timed out after $TimeoutSeconds s"
    }
    if ($process.ExitCode -ne 0) { throw "$mode repetition $($index[$mode]) exited with code $($process.ExitCode)" }
    $artifact = Get-ChildItem -Path (Join-Path $work 'artifacts') -Filter 'spider-stress-*.json' -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notlike '*.log*' -and $_.LastWriteTime -ge $started } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $artifact) { throw "$mode repetition $($index[$mode]) produced no spider-stress artifact in $(Join-Path $work 'artifacts')" }
    $saved = Join-Path $directory 'artifact.json'
    Copy-Item -LiteralPath $artifact.FullName -Destination $saved -Force
    $runs[$mode].Add($saved)
    $json = Get-Content -LiteralPath $artifact.FullName -Raw | ConvertFrom-Json
    Write-Host ("    mean {0:0.00} ms  median {1:0.00}  p95 {2:0.00}  {3:0.0} fps" -f $json.meanMs, $json.medianMs, $json.p95Ms, $json.fps)
}

Write-Host ''
$summary = @{}
foreach ($mode in $Modes) {
    if ($DryRun) { continue }
    $values = @($runs[$mode] | ForEach-Object { [double](Get-Content -LiteralPath $_ -Raw | ConvertFrom-Json).meanMs })
    $sorted = $values | Sort-Object
    $median = if ($sorted.Count % 2 -eq 1) { $sorted[[int][Math]::Floor($sorted.Count / 2)] } else { ($sorted[$sorted.Count / 2 - 1] + $sorted[$sorted.Count / 2]) / 2 }
    $summary[$mode] = [pscustomobject]@{ Mode = $mode; Runs = $sorted.Count; MedianMs = [Math]::Round($median, 3); MinMs = [Math]::Round($sorted[0], 3); MaxMs = [Math]::Round($sorted[-1], 3); Fps = [Math]::Round(1000 / $median, 2) }
}
if (-not $DryRun) {
    $summary.Values | Sort-Object MedianMs | Format-Table -AutoSize | Out-String | Write-Host
    if ($quartet) {
        & $aggregate -Baseline $runs[$Modes[0]] -Candidate $runs[$Modes[1]] -Output (Join-Path $Output 'summary.json')
    } else {
        Write-Host 'More than two modes: reporting medians only. Re-run with two modes for a gated comparison.'
    }
}
Write-Host "Artifacts: $Output"
