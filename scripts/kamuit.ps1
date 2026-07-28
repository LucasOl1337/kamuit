<#
.SYNOPSIS
  KamuiT CLI — agent-first control plane for the running KamuiT window.

.EXAMPLE
  kamuit open grok
  kamuit open claude -C C:\Projetos\riftbomb -n 2
  kamuit list
  kamuit focus 3
  kamuit type 2 "hello" -Enter
  kamuit show
  kamuit agents
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Command = "help",

    [Parameter(Position = 1)]
    [string]$Arg1,

    [Alias('a')]
    [string]$Agent,

    [Alias('C', 'Dir')]
    [string]$Cwd,

    [Alias('n')]
    [int]$Count = 1,

    [Alias('s')]
    [int]$Slot,

    [Alias('t')]
    [string]$Text,

    [switch]$Enter,

    [switch]$NoShow,

    [int]$TimeoutMs = 8000,

    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$PipeName = 'kamuit'

function Find-KamuiTExe {
    $candidates = @(
        'C:\Projetos\KamuiT\publish\KamuiT.exe',
        (Join-Path $PSScriptRoot '..\publish\KamuiT.exe'),
        (Join-Path $PSScriptRoot '..\bin\Release\net8.0-windows\KamuiT.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\KamuiT\KamuiT.exe')
    )
    foreach ($c in $candidates) {
        $full = [IO.Path]::GetFullPath($c)
        if (Test-Path -LiteralPath $full) { return $full }
    }
    return $null
}

function Send-KamuiRequest([hashtable]$req, [int]$timeoutMs) {
    $json = ($req | ConvertTo-Json -Compress -Depth 6)
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $lastErr = $null
    while ($sw.ElapsedMilliseconds -lt $timeoutMs) {
        try {
            $pipe = [IO.Pipes.NamedPipeClientStream]::new('.', $PipeName, [IO.Pipes.PipeDirection]::InOut)
            $remain = [Math]::Max(200, $timeoutMs - [int]$sw.ElapsedMilliseconds)
            $pipe.Connect($remain)
            try {
                $writer = [IO.StreamWriter]::new($pipe, [Text.UTF8Encoding]::new($false), 1024, $true)
                $writer.AutoFlush = $true
                $reader = [IO.StreamReader]::new($pipe, [Text.Encoding]::UTF8, $false, 1024, $true)
                $writer.WriteLine($json)
                $line = $reader.ReadLine()
                $writer.Dispose()
                $reader.Dispose()
                if ([string]::IsNullOrWhiteSpace($line)) {
                    throw 'empty response from KamuiT'
                }
                return ($line | ConvertFrom-Json)
            }
            finally {
                $pipe.Dispose()
            }
        }
        catch {
            $lastErr = $_
            Start-Sleep -Milliseconds 150
        }
    }
    throw "KamuiT pipe timeout (${timeoutMs}ms). Last: $lastErr"
}

function Ensure-KamuiTRunning {
    param([string[]]$StartArgs = @())
    $already = [bool](Get-Process -Name KamuiT -ErrorAction SilentlyContinue)
    if ($already) { return $false }

    $exe = Find-KamuiTExe
    if (-not $exe) {
        throw 'KamuiT.exe not found. Publish first: dotnet publish -c Release -o publish'
    }
    if ($StartArgs -and $StartArgs.Count -gt 0) {
        Start-Process -FilePath $exe -ArgumentList $StartArgs | Out-Null
    }
    else {
        Start-Process -FilePath $exe | Out-Null
    }
    # pipe sobe no Loaded do MainWindow
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    while ([DateTime]::UtcNow -lt $deadline) {
        try {
            $pipe = [IO.Pipes.NamedPipeClientStream]::new('.', $PipeName, [IO.Pipes.PipeDirection]::InOut)
            $pipe.Connect(200)
            $pipe.Dispose()
            return $true
        }
        catch {
            Start-Sleep -Milliseconds 150
        }
    }
    throw 'KamuiT started but pipe never came up'
}

function Show-Help {
    @"
KamuiT CLI — control the agent workspace

  kamuit open <agent> [-C <cwd>] [-n <count>]
  kamuit list
  kamuit focus <slot|id>
  kamuit type <slot> <text> [-Enter]
  kamuit close [slot]
  kamuit show
  kamuit agents
  kamuit ping

Agents: grok, claude, codex, pi, shell

Examples:
  kamuit open grok
  kamuit open claude -C C:\Projetos\riftbomb -n 2
  kamuit open grok -C C:\Projetos -n 4
  kamuit focus 2
  kamuit type 1 "resume the task" -Enter
"@
}

# --- parse command -----------------------------------------------------------
$cmd = $Command.ToLowerInvariant()
if ($cmd -in @('help', '-h', '--help', '/?')) {
    Show-Help
    exit 0
}

# shortcuts: `kamuit grok` == `kamuit open grok`
if ($cmd -in @('grok', 'claude', 'codex', 'pi', 'shell') -and -not $Agent) {
    $Agent = $cmd
    $cmd = 'open'
}

$req = @{ op = $cmd }

switch ($cmd) {
    'open' {
        $agentName = if ($Agent) { $Agent } elseif ($Arg1) { $Arg1 } else { 'shell' }
        $req.op = 'open'
        $req.agent = $agentName
        $req.count = [Math]::Max(1, $Count)
        if ($Cwd) { $req.cwd = $Cwd }
        if ($NoShow) { $req.show = $false } else { $req.show = $true }
    }
    'list' { $req.op = 'list' }
    'tabs' { $req.op = 'list' }
    'show' { $req.op = 'show' }
    'summon' { $req.op = 'show' }
    'ping' { $req.op = 'ping' }
    'agents' { $req.op = 'agents' }
    'focus' {
        $req.op = 'focus'
        $target = if ($Slot) { $Slot } elseif ($Arg1) { $Arg1 } else { $null }
        if (-not $target) { throw 'focus needs a slot number or tab id' }
        if ($target -match '^\d+$') { $req.slot = [int]$target }
        else { $req.id = [string]$target }
        if ($NoShow) { $req.show = $false } else { $req.show = $true }
    }
    'close' {
        $req.op = 'close'
        $target = if ($Slot) { $Slot } elseif ($Arg1) { $Arg1 } else { $null }
        if ($target -match '^\d+$') { $req.slot = [int]$target }
        elseif ($target) { $req.id = [string]$target }
    }
    'type' {
        $req.op = 'type'
        if ($Slot) { $req.slot = $Slot }
        elseif ($Arg1 -match '^\d+$') { $req.slot = [int]$Arg1; if (-not $Text -and $args.Count -eq 0) { } }
        $textVal = $Text
        if (-not $textVal -and $Arg1 -and $Arg1 -notmatch '^\d+$') { $textVal = $Arg1 }
        if (-not $textVal -and $Agent) { $textVal = $Agent } # misuse guard
        # Position: kamuit type 2 "hello"
        if (-not $textVal) {
            # PowerShell binds leftover poorly; accept -Text
            throw 'type needs -Text "..." (and optional -Slot N)'
        }
        $req.text = $textVal
        if ($Slot) { $req.slot = $Slot }
        elseif ($Arg1 -match '^\d+$') { $req.slot = [int]$Arg1 }
        if ($Enter) { $req.enter = $true }
    }
    default {
        Show-Help
        Write-Error "Unknown command: $Command"
        exit 1
    }
}

try {
    # Se o app ainda nao esta up e o comando e `open`, ja nasce com o agente (sem aba shell extra).
    $bootArgs = @()
    if ($cmd -eq 'open') {
        $bootArgs = @('open', [string]$req.agent)
        if ($req.count) { $bootArgs += @('-n', [string]$req.count) }
        if ($req.cwd) { $bootArgs += @('-C', [string]$req.cwd) }
        if ($req.show -eq $false) { $bootArgs += '--no-show' }
    }
    $fresh = Ensure-KamuiTRunning -StartArgs $bootArgs
    if ($fresh -and $cmd -eq 'open') {
        # Args ja aplicados no boot; so lista estado.
        $resp = Send-KamuiRequest @{ op = 'list' } $TimeoutMs
    }
    else {
        $resp = Send-KamuiRequest $req $TimeoutMs
    }
}
catch {
    Write-Error $_
    exit 2
}

if ($Json) {
    $resp | ConvertTo-Json -Depth 6
    if (-not $resp.ok) { exit 1 }
    exit 0
}

if (-not $resp.ok) {
    Write-Host "ERR: $($resp.error)" -ForegroundColor Red
    exit 1
}

if ($resp.message) { Write-Host $resp.message -ForegroundColor Green }

if ($resp.tabs) {
    $resp.tabs | ForEach-Object {
        $mark = if ($_.active) { '*' } else { ' ' }
        $agent = if ($_.agent) { $_.agent } else { 'shell' }
        $slot = if ($null -ne $_.slot) { $_.slot } else { '-' }
        Write-Host ("{0} #{1,-2} {2,-8} {3}" -f $mark, $slot, $agent, $_.title)
    }
}

if ($resp.limbo -and @($resp.limbo).Count -gt 0) {
    Write-Host '-- limbo --' -ForegroundColor DarkGray
    $resp.limbo | ForEach-Object {
        $agent = if ($_.agent) { $_.agent } else { 'shell' }
        Write-Host ("  ~ {0,-8} {1}" -f $agent, $_.title) -ForegroundColor DarkGray
    }
}

exit 0
