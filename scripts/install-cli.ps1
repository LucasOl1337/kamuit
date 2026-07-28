# Installs `kamuit` on user PATH → %USERPROFILE%\.local\bin
$ErrorActionPreference = 'Stop'

$bin = Join-Path $env:USERPROFILE '.local\bin'
New-Item -ItemType Directory -Force -Path $bin | Out-Null

$srcDir = $PSScriptRoot
Copy-Item -Force (Join-Path $srcDir 'kamuit.ps1') (Join-Path $bin 'kamuit.ps1')
Copy-Item -Force (Join-Path $srcDir 'kamuit.cmd') (Join-Path $bin 'kamuit.cmd')

# Shim that always points to published app area as fallback discovery
$shim = @'
@echo off
setlocal
set "KAMUIT_PS=%USERPROFILE%\.local\bin\kamuit.ps1"
if exist "%KAMUIT_PS%" (
  pwsh -NoProfile -File "%KAMUIT_PS%" %*
  exit /b %ERRORLEVEL%
)
set "FALLBACK=C:\Projetos\KamuiT\scripts\kamuit.ps1"
if exist "%FALLBACK%" (
  pwsh -NoProfile -File "%FALLBACK%" %*
  exit /b %ERRORLEVEL%
)
echo kamuit.ps1 not found
exit /b 1
'@
Set-Content -Path (Join-Path $bin 'kamuit.cmd') -Value $shim -Encoding ASCII

# User PATH
$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
if (-not $userPath) { $userPath = '' }
$parts = $userPath -split ';' | Where-Object { $_ -and $_.Trim() }
if ($parts -notcontains $bin) {
    $newPath = ($parts + $bin) -join ';'
    [Environment]::SetEnvironmentVariable('Path', $newPath, 'User')
    $env:Path = $bin + ';' + $env:Path
    Write-Host "Added $bin to user PATH"
}
else {
    Write-Host "PATH already has $bin"
}

Write-Host "Installed: $(Join-Path $bin 'kamuit.cmd')"
Write-Host "Try: kamuit open grok"
Write-Host "(open a new terminal if PATH was just updated)"
