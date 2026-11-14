# Downloads GDevelop events-editor documentation screenshots for UI research.
# Usage: powershell -File Tools/PullGDevelopDocImages.ps1
$ErrorActionPreference = 'Continue'
$root = Join-Path $PSScriptRoot '..\Docs\Research\GDevelopEventsEditor-2026-11-14'
$root = [System.IO.Path]::GetFullPath($root)
New-Item -ItemType Directory -Force -Path $root | Out-Null

$pages = @(
    'gdevelop5/interface/events-editor',
    'gdevelop5/events/standard',
    'gdevelop5/events/else',
    'gdevelop5/events/repeat',
    'gdevelop5/events/while',
    'gdevelop5/events/foreach',
    'gdevelop5/events/group',
    'gdevelop5/events/link',
    'gdevelop5/events/object-picking',
    'gdevelop5/events/expressions',
    'gdevelop5/events/advanced',
    'gdevelop5/interface/scene-editor'
)

$rows = New-Object System.Collections.Generic.List[object]
foreach ($p in $pages)
{
    $page = 'https://wiki.gdevelop.io/' + $p + '/'
    try
    {
        $html = (Invoke-WebRequest -Uri $page -UseBasicParsing -TimeoutSec 60).Content
    }
    catch
    {
        Write-Output "PAGE FAILED $page"
        continue
    }

    $sources = [regex]::Matches($html, '<img alt="[^"]*" src="([^"]+)"') |
        ForEach-Object { $_.Groups[1].Value } |
        Where-Object { $_ -notmatch 'logo' -and $_ -notmatch '^data:' } |
        Sort-Object -Unique

    foreach ($src in $sources)
    {
        $imgUrl = [uri]::new([uri]$page, $src).AbsoluteUri
        $leaf = [System.IO.Path]::GetFileName(($src -split '\?')[0])
        $name = ($p -replace '/', '_') + '__' + $leaf
        $out = Join-Path $root $name
        try
        {
            Invoke-WebRequest -Uri $imgUrl -OutFile $out -UseBasicParsing -TimeoutSec 60
            $rows.Add([pscustomobject]@{ Page = $p; File = $name; Url = $imgUrl; Bytes = (Get-Item $out).Length })
        }
        catch
        {
            Write-Output "IMAGE FAILED $imgUrl"
        }
    }
}

$rows | Sort-Object Page, File | Format-Table -AutoSize | Out-String -Width 300
"Downloaded $($rows.Count) images into $root"
