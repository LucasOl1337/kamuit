$exe = "C:\projetos\kamuit\bin\Debug\net8.0-windows\KamuiT.exe"
$lnkPath = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\KamuiT.lnk"

$ws = New-Object -ComObject WScript.Shell
$lnk = $ws.CreateShortcut($lnkPath)
$lnk.TargetPath = $exe
$lnk.WorkingDirectory = Split-Path $exe
$lnk.IconLocation = "C:\projetos\kamuit\kamuit.ico,0"
$lnk.Description = "KamuiT - terminal workspace"
$lnk.Save()

Write-Output "Atalho criado: $lnkPath"
