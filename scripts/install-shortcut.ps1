# Canonical install the Windows Search / Start Menu entry uses.
# Keep in sync with AGENTS.md — agents must publish here after app changes.
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -ErrorAction SilentlyContinue
if (-not $root -or -not (Test-Path (Join-Path $PSScriptRoot "..\KamuiT.csproj"))) {
    $root = Resolve-Path (Join-Path $PSScriptRoot "..")
} else {
    $root = Resolve-Path (Join-Path $PSScriptRoot "..")
}
$exe = Join-Path $root "publish\KamuiT.exe"
$ico = Join-Path $root "kamuit.ico"
$lnkPath = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\KamuiT.lnk"

if (-not (Test-Path $exe)) {
    Write-Error "Missing $exe — run: dotnet publish -c Release -r win-x64 --self-contained false -o publish"
    exit 1
}

$ws = New-Object -ComObject WScript.Shell
$lnk = $ws.CreateShortcut($lnkPath)
$lnk.TargetPath = $exe
$lnk.WorkingDirectory = Split-Path $exe
$lnk.IconLocation = if (Test-Path $ico) { "$ico,0" } else { "$exe,0" }
$lnk.Description = "KamuiT - terminal workspace"
$lnk.Save()

Write-Output "Atalho criado: $lnkPath"
Write-Output "Target: $exe"
