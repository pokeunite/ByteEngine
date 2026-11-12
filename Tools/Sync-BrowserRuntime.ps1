<#
.SYNOPSIS
    Mirrors the canonical browser runtime into the editor build folders.

.DESCRIPTION
    Dist/ByteEngine/BrowserRuntime is the canonical, published copy of the web
    runtime. The editor's bin/Debug and bin/Release folders receive generated
    copies during publish, and those copies previously drifted between builds.

    This script treats Dist as the single source and mirrors it to every editor
    output folder that already exists or is requested. It removes stale
    fingerprinted framework files from the destination instead of merging them.

.PARAMETER Source
    Canonical browser runtime folder. Defaults to
    Dist/ByteEngine/BrowserRuntime.

.PARAMETER Target
    One or more destination BrowserRuntime folders. Defaults to every
    Editor/ByteEngine.Editor/bin/*/net*/win-*/BrowserRuntime folder.

.PARAMETER Check
    Report size and file differences without copying.

.EXAMPLE
    powershell -ep bypass -file .\Tools\Sync-BrowserRuntime.ps1

.EXAMPLE
    powershell -ep bypass -file .\Tools\Sync-BrowserRuntime.ps1 -Check
#>
[CmdletBinding()]
param(
    [string]$Source = '',
    [string[]]$Target = @(),
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if (-not (Test-Path -LiteralPath (Join-Path $root 'ByteEngine.sln'))) {
    throw "Sync-BrowserRuntime must live in a ByteEngine checkout (missing ByteEngine.sln in $root)."
}

if (-not $Source) {
    $Source = Join-Path $root 'Dist/ByteEngine/BrowserRuntime'
}
$Source = [IO.Path]::GetFullPath($Source)

if (-not (Test-Path -LiteralPath $Source -PathType Container)) {
    throw "Canonical browser runtime was not found: $Source. Run Tools/PublishBrowser.ps1 first."
}
if (-not (Test-Path -LiteralPath (Join-Path $Source 'index.html'))) {
    throw "Canonical browser runtime is incomplete (no index.html): $Source"
}

if ($Target.Count -eq 0) {
    $Target = @(
        Get-ChildItem -LiteralPath (Join-Path $root 'Editor/ByteEngine.Editor/bin') -Directory -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -eq 'BrowserRuntime' } |
            ForEach-Object FullName
    )
}

if ($Target.Count -eq 0) {
    Write-Host 'No editor BrowserRuntime output folders exist yet. Build or publish the editor first.'
    return
}

function Get-RelativeTo([string]$base, [string]$path) {
    $baseFull = [IO.Path]::GetFullPath($base).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $full = [IO.Path]::GetFullPath($path)
    if ($full.StartsWith($baseFull, [StringComparison]::OrdinalIgnoreCase)) {
        return $full.Substring($baseFull.Length)
    }
    return $full
}

function Get-FolderStats([string]$path) {
    $files = @(Get-ChildItem -LiteralPath $path -Recurse -File -Force -ErrorAction SilentlyContinue)
    $bytes = ($files | Measure-Object Length -Sum).Sum
    if ($null -eq $bytes) { $bytes = 0 }
    $relative = $files | ForEach-Object { Get-RelativeTo $path $_.FullName } | Sort-Object
    return [pscustomobject]@{
        Path  = $path
        Count = $files.Count
        Bytes = $bytes
        Files = $relative
    }
}

$sourceStats = Get-FolderStats $Source
Write-Host ("Canonical: {0} ({1} files, {2:N1} MB)" -f $Source, $sourceStats.Count, ($sourceStats.Bytes / 1MB))

$failures = 0
foreach ($destination in $Target) {
    $destination = [IO.Path]::GetFullPath($destination)
    if (-not $destination.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to sync outside the repository: $destination"
    }
    if ([IO.Path]::GetFileName($destination) -ne 'BrowserRuntime') {
        throw "Destination must be a BrowserRuntime folder: $destination"
    }
    if ($destination.Equals($Source, [StringComparison]::OrdinalIgnoreCase)) {
        continue
    }

    if (-not (Test-Path -LiteralPath $destination -PathType Container)) {
        Write-Host ("Missing:   {0} (will be created)" -f $destination)
        if (-not $Check) {
            New-Item -ItemType Directory -Path $destination -Force | Out-Null
        }
        else {
            $failures++
            continue
        }
    }

    $targetStats = if (Test-Path -LiteralPath $destination -PathType Container) {
        Get-FolderStats $destination
    }
    else {
        [pscustomobject]@{ Path = $destination; Count = 0; Bytes = 0; Files = @() }
    }

    $onlyInSource = @($sourceStats.Files | Where-Object { $_ -notin $targetStats.Files })
    $onlyInTarget = @($targetStats.Files | Where-Object { $_ -notin $sourceStats.Files })
    $changed = @($sourceStats.Files | Where-Object {
            $candidate = Join-Path $destination $_
            $canonical = Join-Path $Source $_
            if (-not (Test-Path -LiteralPath $candidate)) { return $true }
            (Get-FileHash -LiteralPath $candidate).Hash -ne (Get-FileHash -LiteralPath $canonical).Hash
        })

    Write-Host ("Target:    {0} ({1} files, {2:N1} MB)" -f $destination, $targetStats.Count, ($targetStats.Bytes / 1MB))
    Write-Host ("           {0} to add/update, {1} stale file(s)" -f $changed.Count, $onlyInTarget.Count)

    if ($Check) {
        if ($changed.Count -or $onlyInTarget.Count) {
            $failures++
        }
        continue
    }

    if ($changed.Count -eq 0 -and $onlyInTarget.Count -eq 0) {
        Write-Host '           Already in sync.'
        continue
    }

    if (Get-Process -Name 'ByteEngine.Editor' -ErrorAction SilentlyContinue) {
        throw 'Close ByteEngine before refreshing the browser runtime. No files were copied.'
    }

    foreach ($relative in @($changed + $onlyInSource | Sort-Object -Unique)) {
        $from = Join-Path $Source $relative
        $to = Join-Path $destination $relative
        $parent = Split-Path -Parent $to
        if ($parent) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
        Copy-Item -LiteralPath $from -Destination $to -Force
    }

    # Remove generated files that the canonical runtime no longer ships, such
    # as old fingerprinted _framework assemblies.
    foreach ($relative in $onlyInTarget) {
        $stale = Join-Path $destination $relative
        if (Test-Path -LiteralPath $stale -PathType Leaf) {
            Remove-Item -LiteralPath $stale -Force
        }
    }

    # Drop directories left empty by the prune.
    Get-ChildItem -LiteralPath $destination -Directory -Recurse -Force -ErrorAction SilentlyContinue |
        Sort-Object { $_.FullName.Length } -Descending |
        ForEach-Object {
            if (-not (Get-ChildItem -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue)) {
                Remove-Item -LiteralPath $_.FullName -Force
            }
        }

    Write-Host '           Synced.'
}

if ($Check -and $failures -gt 0) {
    throw "Browser runtime drift detected in $failures target(s). Run without -Check to repair."
}

Write-Host 'Browser runtime targets match the canonical Dist copy.'
