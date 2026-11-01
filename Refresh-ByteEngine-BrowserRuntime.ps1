<#
.SYNOPSIS
Refreshes only the browser runtime. Does not export a game.
.EXAMPLE
powershell -ep bypass -file .\Refresh-ByteEngine-BrowserRuntime.ps1
#>
[CmdletBinding()]
param([string]$SourceRoot='',[string]$InstallRoot='',[string]$ProjectFile='')
$ErrorActionPreference='Stop'
$arguments=@{BrowserOnly=$true}
if($SourceRoot){$arguments.SourceRoot=$SourceRoot}
if($InstallRoot){$arguments.InstallRoot=$InstallRoot}
if($ProjectFile){$arguments.ProjectFile=$ProjectFile}
& (Join-Path $PSScriptRoot 'Rebuild-ByteEngine-And-Refresh-Dist.ps1') @arguments
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
