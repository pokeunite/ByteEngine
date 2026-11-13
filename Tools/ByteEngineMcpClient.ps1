<#
.SYNOPSIS
Drives the ByteEngine MCP authoring server over stdio from any shell or agent.

.DESCRIPTION
Starts the published ByteEngine MCP server, performs the MCP handshake, sends one or more
tool calls against a single project session, and prints the tool JSON. Use it when a client
cannot host MCP itself (scripts, CI, another agent) but still needs semantic project authoring.

The server keeps one active project per process and its compact handles and revision tokens
live only for that process, so pass every call for a session to one invocation.

.PARAMETER Exe
Path to ByteEngine.Mcp.exe. Defaults to Dist\ByteEngine-MCP, then Release, then Debug builds.

.PARAMETER Project
Absolute .byteproject path. Sent as be_project/open before the other calls.

.PARAMETER Request
One or more calls. Accepts a "tool/op" string, a JSON request object, or a JSON array of
either. Arrays are the portable way to batch from powershell.exe -File, which cannot repeat
a named parameter.
JSON object form: {"tool":"be_scene","op":"inspect","args":{"scene":"Scenes/Main.bytescene"}}

.PARAMETER RequestFile
JSON file with an array of requests, or newline-delimited requests. Blank lines and lines
starting with # are ignored.

.PARAMETER ListTools
Print the server's tool names and descriptions.

.PARAMETER Json
Emit one machine-readable JSON object for the whole session instead of per-call text.

.PARAMETER TimeoutSeconds
Per-call response timeout. Defaults to 120 seconds.

.PARAMETER Trace
Write raw JSON-RPC traffic to stderr for debugging.

.EXAMPLE
Tools/ByteEngineMcpClient.ps1 -ListTools

.EXAMPLE
Tools/ByteEngineMcpClient.ps1 -Project C:/Games/Game/Game.byteproject -Request 'be_project/info'

.EXAMPLE
Tools/ByteEngineMcpClient.ps1 -Project C:/Games/Game/Game.byteproject `
    -Request '[{"tool":"be_scene","op":"inspect","args":{"scene":"Scenes/Main.bytescene"}},"be_runtime/validate"]' -Json
#>
param(
    [string]$Exe,
    [string]$Project,
    [string[]]$Request,
    [string]$RequestFile,
    [switch]$ListTools,
    [switch]$Json,
    [int]$TimeoutSeconds = 120,
    [switch]$Trace
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot

function Resolve-ServerExe {
    param([string]$Requested)
    if ($Requested) {
        $full = [System.IO.Path]::GetFullPath($Requested)
        if (-not (Test-Path -LiteralPath $full)) { throw "MCP server not found: $full" }
        return $full
    }
    $candidates = @(
        (Join-Path $repositoryRoot "Dist\ByteEngine-MCP\ByteEngine.Mcp.exe"),
        (Join-Path $repositoryRoot "Tools\ByteEngine.Mcp\bin\Release\net9.0\ByteEngine.Mcp.exe"),
        (Join-Path $repositoryRoot "Tools\ByteEngine.Mcp\bin\Debug\net9.0\ByteEngine.Mcp.exe")
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }
    throw "No ByteEngine MCP build found. Run Tools/PublishMcp.ps1 first, or pass -Exe."
}

function ConvertTo-Request {
    param($Item)
    if ($Item -is [string]) {
        $text = $Item.Trim()
        if ($text.Length -eq 0) { return $null }
        if ($text.StartsWith("{")) { return ConvertTo-Request (ConvertFrom-Json $text) }
        if ($text.Contains("/")) {
            $parts = $text.Split("/", 2)
            return [pscustomobject]@{ tool = $parts[0].Trim(); op = $parts[1].Trim(); args = $null }
        }
        throw "Request '$text' must be a JSON object or a 'tool/op' string."
    }
    $tool = [string]$Item.tool
    if (-not $tool) { $tool = [string]$Item.name }
    if (-not $tool) { throw "Each request needs a 'tool' (or 'name') field." }
    return [pscustomobject]@{ tool = $tool; op = $Item.op; args = $Item.args }
}

function Expand-Request {
    param([string]$Text)
    $trimmed = $Text.Trim()
    if ($trimmed.Length -eq 0) { return @() }
    if ($trimmed.StartsWith("[")) {
        # PowerShell 5.1 cannot parse a top-level JSON array of primitives, so wrap it.
        $items = New-Object System.Collections.ArrayList
        foreach ($entry in (ConvertFrom-Json ('{"items":' + $trimmed + '}')).items) {
            [void]$items.Add((ConvertTo-Request $entry))
        }
        return $items
    }
    return @((ConvertTo-Request $trimmed))
}

function Read-RequestFile {
    param([string]$Path)
    $full = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $full)) { throw "Request file not found: $full" }
    $text = Get-Content -LiteralPath $full -Raw
    $results = New-Object System.Collections.ArrayList
    if ($text.TrimStart().StartsWith("[")) {
        # See Expand-Request: 5.1 needs the array wrapped before parsing.
        foreach ($item in (ConvertFrom-Json ('{"items":' + $text.Trim() + '}')).items) {
            [void]$results.Add((ConvertTo-Request $item))
        }
        return $results
    }
    foreach ($line in ($text -split "`r?`n")) {
        $trimmed = $line.Trim()
        if ($trimmed.Length -eq 0 -or $trimmed.StartsWith("#")) { continue }
        [void]$results.Add((ConvertTo-Request $trimmed))
    }
    return $results
}


function Start-Server {
    param([string]$Path)
    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = $Path
    $info.RedirectStandardInput = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.UseShellExecute = $false
    if (-not $env:BYTEENGINE_ROOT) {
        $info.EnvironmentVariables["BYTEENGINE_ROOT"] = $repositoryRoot
    }
    return [System.Diagnostics.Process]::Start($info)
}

function Send-Message {
    param($Target, [string]$Json)
    if ($Trace) { [Console]::Error.WriteLine(">> $Json") }
    $Target.StandardInput.WriteLine($Json)
    $Target.StandardInput.Flush()
}

function Receive-Message {
    param($Target, [int]$TimeoutMs)
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    while ($true) {
        $remaining = [int]($deadline - [DateTime]::UtcNow).TotalMilliseconds
        if ($remaining -le 0) { throw "Timed out after $([math]::Round($TimeoutMs / 1000))s waiting for the MCP server." }
        $read = $Target.StandardOutput.ReadLineAsync()
        if (-not $read.Wait($remaining)) { throw "Timed out after $([math]::Round($TimeoutMs / 1000))s waiting for the MCP server." }
        $line = $read.Result
        if ($null -eq $line) { throw "The MCP server closed its output stream." }
        if ($line.Trim().Length -eq 0) { continue }
        if ($Trace) { [Console]::Error.WriteLine("<< $line") }
        $message = ConvertFrom-Json $line
        if ($null -ne $message.method -and $null -ne $message.id) {
            $result = if ($message.method -eq "roots/list") { '{"roots":[]}' } else { '{}' }
            Send-Message $Target ('{"jsonrpc":"2.0","id":' + $message.id + ',"result":' + $result + '}')
            continue
        }
        if ($null -eq $message.id) { continue }
        return $message
    }
}

function Receive-Response {
    param($Target, [int]$Id, [int]$TimeoutMs)
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    while ($true) {
        $remaining = [int]([Math]::Max(1000, ($deadline - [DateTime]::UtcNow).TotalMilliseconds))
        $message = Receive-Message $Target $remaining
        if ([int]$message.id -eq $Id) { return $message }
        if ($Trace) { [Console]::Error.WriteLine("-- ignored response id $($message.id)") }
    }
}

function Invoke-Session {
    param($Target, $Requests, [bool]$IncludeTools)

    $startup = [Math]::Max(30, $TimeoutSeconds) * 1000
    Send-Message $Target '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"byteengine-shell-client","version":"1.0.0"}}}'
    $handshake = Receive-Response $Target 1 $startup
    Send-Message $Target '{"jsonrpc":"2.0","method":"notifications/initialized"}'

    $session = [pscustomobject]@{
        protocol = $handshake.result.protocolVersion
        server   = $handshake.result.serverInfo.name
        version  = $handshake.result.serverInfo.version
        tools    = @()
        results  = @()
    }

    if ($IncludeTools) {
        Send-Message $Target '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}'
        $listing = Receive-Response $Target 2 $startup
        $session.tools = @($listing.result.tools | ForEach-Object {
            [pscustomobject]@{ name = $_.name; description = $_.description }
        })
    }

    $id = 10
    $index = 0
    foreach ($request in $Requests) {
        $arguments = @{ op = $request.op }
        if ($null -ne $request.args) { $arguments["args"] = $request.args }
        $payload = @{ jsonrpc = "2.0"; id = $id; method = "tools/call";
            params = @{ name = $request.tool; arguments = $arguments } } |
            ConvertTo-Json -Depth 20 -Compress
        Send-Message $Target $payload
        $response = Receive-Response $Target $id ($TimeoutSeconds * 1000)
        $index++

        $text = $null
        $isError = $false
        if ($null -ne $response.result) {
            $isError = [bool]$response.result.isError
            if ($response.result.content -and $response.result.content.Count -gt 0) {
                $text = [string]$response.result.content[0].text
            }
        }
        if ($response.error) {
            $text = ConvertTo-Json $response.error -Depth 10 -Compress
            $isError = $true
        }

        $ok = -not $isError
        $code = $null
        $data = $null
        if ($text) {
            try {
                $data = ConvertFrom-Json $text
                if ($null -ne $data.ok) { $ok = [bool]$data.ok; if (-not $ok) { $isError = $true } }
                if ($data.code) { $code = [string]$data.code }
            }
            catch { }
        }

        $session.results += [pscustomobject]@{
            index    = $index
            tool     = $request.tool
            op       = $request.op
            ok       = $ok
            is_error = $isError
            code     = $code
            text     = $text
        }
        $id++
    }

    return $session
}

$requests = New-Object System.Collections.ArrayList
if ($Project) {
    if (-not [System.IO.Path]::IsPathRooted($Project)) {
        throw "-Project must be an absolute .byteproject path."
    }
    [void]$requests.Add([pscustomobject]@{
        tool = "be_project"; op = "open"; args = @{ path = [System.IO.Path]::GetFullPath($Project) } })
}
foreach ($item in $Request) {
    foreach ($converted in (Expand-Request $item)) {
        if ($null -ne $converted) { [void]$requests.Add($converted) }
    }
}
if ($RequestFile) {
    foreach ($item in (Read-RequestFile $RequestFile)) { [void]$requests.Add($item) }
}

if (-not $ListTools -and $requests.Count -eq 0) {
    Write-Host "ByteEngine MCP shell client"
    Write-Host ""
    Write-Host "  Tools/ByteEngineMcpClient.ps1 -ListTools"
    Write-Host "  Tools/ByteEngineMcpClient.ps1 -Project C:/Games/Game/Game.byteproject -Request be_project/info"
    Write-Host "  Tools/ByteEngineMcpClient.ps1 -Project ... -Request '{`"tool`":`"be_scene`",`"op`":`"inspect`",`"args`":{`"scene`":`"Scenes/Main.bytescene`"}}'"
    Write-Host "  Tools/ByteEngineMcpClient.ps1 -RequestFile calls.json -Json"
    Write-Host ""
    Write-Host "Pass -Exe to override the server binary and -Trace to dump JSON-RPC traffic to stderr."
    exit 0
}

$serverExe = Resolve-ServerExe $Exe
$process = Start-Server $serverExe
$stderrTask = $process.StandardError.ReadToEndAsync()
$session = $null
$failure = $null
try {
    $session = Invoke-Session $process $requests ($ListTools.IsPresent)
}
catch {
    $failure = $_
}
finally {
    try {
        if (-not $process.HasExited) { $process.StandardInput.Close() }
        if (-not $process.WaitForExit(5000)) { $process.Kill() }
    }
    catch { }
}

if ($failure) {
    $details = ""
    try {
        if ($stderrTask.Wait(2000)) { $details = $stderrTask.Result.Trim() }
    }
    catch { }
    [Console]::Error.WriteLine("ByteEngine MCP session failed: " + $failure.Exception.Message)
    if ($details) { [Console]::Error.WriteLine("--- server stderr ---`n" + $details) }
    exit 2
}

if ($Json) {
    $document = [pscustomobject]@{
        ok       = -not ($session.results | Where-Object { -not $_.ok })
        protocol = $session.protocol
        server   = $session.server
        version  = $session.version
        tools    = $session.tools
        results  = $session.results
    }
    ConvertTo-Json $document -Depth 20
}
else {
    Write-Output ("server {0} {1} (protocol {2})" -f $session.server, $session.version, $session.protocol)
    if ($ListTools) {
        Write-Output ("tools ({0}): {1}" -f $session.tools.Count, (($session.tools | ForEach-Object { $_.name }) -join ", "))
        foreach ($tool in $session.tools) {
            Write-Output ("  {0}  {1}" -f $tool.name, $tool.description)
        }
    }
    foreach ($result in $session.results) {
        $status = if ($result.ok) { "ok" } elseif ($result.code) { "FAILED " + $result.code } else { "FAILED" }
        Write-Output ("[{0}] {1}/{2} {3}" -f $result.index, $result.tool, $result.op, $status)
        if ($result.text) { Write-Output $result.text }
    }
}

if ($session.results | Where-Object { -not $_.ok }) { exit 1 }
exit 0

