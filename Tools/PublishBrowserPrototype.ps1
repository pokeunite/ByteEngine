param(
    [string]$OutputParent = ''
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputParent)) {
    $OutputParent = Join-Path $repoRoot '.artifacts/browser-prototype'
}
$destination = Join-Path ([IO.Path]::GetFullPath($OutputParent)) ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $destination | Out-Null
# Never refresh Dist, kill the editor, or overwrite an existing export.
& dotnet publish (Join-Path $repoRoot 'Player/ByteEngine.Browser/ByteEngine.Browser.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Browser runtime publish failed.' }
$source = Join-Path $repoRoot 'Player/ByteEngine.Browser/bin/Release/net9.0/publish/wwwroot'
if (!(Test-Path (Join-Path $source '_framework/dotnet.js'))) { throw 'Missing WebAssembly bootstrap.' }
if (!(Test-Path (Join-Path $source 'index.html'))) { throw 'Missing browser entry page.' }
$site = Join-Path $destination 'site'
New-Item -ItemType Directory -Path $site | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $site -Recurse
# Match the real browser loader; this remains a deliberately marked demo.
Get-ChildItem -LiteralPath $site -Recurse -File | Where-Object { $_.Extension -in '.br','.gz' } |
    ForEach-Object { Remove-Item -LiteralPath $_.FullName }
$files = @(Get-ChildItem -LiteralPath (Join-Path $site 'Resources') -Recurse -File | ForEach-Object {
    @{ path=[IO.Path]::GetRelativePath($site,$_.FullName).Replace('\','/');
       size=$_.Length; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$manifest = @{ formatVersion=1; name='ByteEngine Backend Verification'; demo=$true; files=$files } | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText((Join-Path $site 'web-game.json'), $manifest)
$zip = Join-Path $destination 'ByteEngine-Browser-Backend-Test.zip'
Compress-Archive -Path (Join-Path $site '*') -DestinationPath $zip
Write-Host "Backend verification build (NOT a game-project export): $site"
Write-Host "Browser test ZIP: $zip"
Write-Host 'Serve site over HTTP, or upload ZIP as HTML on a private itch.io test page.'
