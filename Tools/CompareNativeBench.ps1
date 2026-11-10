param(
    [Parameter(Mandatory=$true)][string]$Before,
    [Parameter(Mandatory=$true)][string]$After,
    [double]$TolerancePercent = 3
)
$ErrorActionPreference = 'Stop'
$baseline = @(Import-Csv -LiteralPath $Before)
$candidate = @(Import-Csv -LiteralPath $After)
if (!$baseline.Count -or !$candidate.Count) { throw 'Benchmark CSV is empty.' }
$failed = $false
$rows = foreach ($old in $baseline) {
    # Configuration includes the cache toggle; compare the same viewport size.
    $key = $old.Configuration -replace '-material-cache-.*$', ''
    $matches = @($candidate | Where-Object { ($_.Configuration -replace '-material-cache-.*$', '') -eq $key })
    if ($matches.Count -ne 1) { throw "Expected exactly one candidate for $key." }
    foreach ($metric in @('CpuP50Ms','CpuP95Ms','GpuP50Ms','UpdateAllocatedBytesPerFrame','Gen0Collections','MaterialBindsPerFrame')) {
        if ($old.$metric -eq '' -or $matches[0].$metric -eq '') { continue }
        $a = [double]::Parse($old.$metric, [Globalization.CultureInfo]::InvariantCulture)
        $b = [double]::Parse($matches[0].$metric, [Globalization.CultureInfo]::InvariantCulture)
        $delta = if ($a -eq 0) { if ($b -eq 0) { 0 } else { [double]::PositiveInfinity } } else { ($b/$a-1)*100 }
        if ($delta -gt $TolerancePercent) { $failed = $true }
        [pscustomobject]@{ Configuration=$key; Metric=$metric; Before=$a; After=$b; DeltaPercent=[math]::Round($delta,2) }
    }
}
$rows | Format-Table -AutoSize
if ($failed) { throw "Native benchmark regression exceeds $TolerancePercent%." }
