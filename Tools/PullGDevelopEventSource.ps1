# Checks out the GDevelop event-system folders from the sparse partial clone at
# D:\ue4 sources\gdevelop source, retrying flaky promisor fetches.
# Usage: powershell -File Tools/PullGDevelopEventSource.ps1
$ErrorActionPreference = 'Continue'
$repo = 'D:\ue4 sources\gdevelop source'
$log = Join-Path ([System.IO.Path]::GetTempPath()) 'gdevelop-sparse.log'
Remove-Item $log -ErrorAction SilentlyContinue

git -C $repo config http.postBuffer 524288000
git -C $repo config http.lowSpeedLimit 1000
git -C $repo config http.lowSpeedTime 300

$paths = @(
    'Core/GDCore/Events',
    'Core/GDCore/Project',
    'Core/GDCore/IDE/Events',
    'Core/GDCore/Extensions',
    'newIDE/app/src/EventsSheet',
    'newIDE/app/src/InstructionOrExpression'
)

$accumulated = @()
foreach ($path in $paths)
{
    $accumulated += $path
    $ok = $false
    for ($attempt = 1; $attempt -le 5; $attempt++)
    {
        "=== $path attempt $attempt ===" | Tee-Object -FilePath $log -Append
        git -C $repo sparse-checkout set @accumulated 2>&1 | Tee-Object -FilePath $log -Append
        if ($LASTEXITCODE -eq 0)
        {
            $ok = $true
            break
        }
        Start-Sleep -Seconds 5
    }
    "RESULT $path ok=$ok" | Tee-Object -FilePath $log -Append
    if (-not $ok)
    {
        break
    }
}

"FINISHED" | Tee-Object -FilePath $log -Append
