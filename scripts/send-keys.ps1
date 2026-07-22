param(
    [string]$Keys = "echo KAMUI-OK",
    [switch]$Enter
)

$sig = @"
using System;
using System.Runtime.InteropServices;
public class FgApi {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    public static bool ForceForeground(IntPtr hWnd) {
        IntPtr fg = GetForegroundWindow();
        if (fg == hWnd) return true;
        uint fgPid;
        uint fgThread = GetWindowThreadProcessId(fg, out fgPid);
        uint curThread = GetCurrentThreadId();
        bool attached = false;
        if (fgThread != curThread)
            attached = AttachThreadInput(curThread, fgThread, true);
        ShowWindow(hWnd, 9); // SW_RESTORE
        BringWindowToTop(hWnd);
        bool ok = SetForegroundWindow(hWnd);
        if (attached) AttachThreadInput(curThread, fgThread, false);
        return ok;
    }
}
"@
Add-Type -TypeDefinition $sig
Add-Type -AssemblyName System.Windows.Forms

$p = Get-Process Kamui -ErrorAction SilentlyContinue
if (-not $p) { Write-Output "KAMUI NAO RODANDO"; exit 1 }

[FgApi]::ForceForeground($p.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 600

$fg = [FgApi]::GetForegroundWindow()
if ($fg -ne $p.MainWindowHandle) {
    Write-Output "ABORTADO: foreground nao e o Kamui (fg=$fg esperado=$($p.MainWindowHandle))"
    exit 1
}

Write-Output "foreground ok, enviando: $Keys"
$send = $Keys -replace "`r", "{ENTER}"
if ($Enter) { $send += "{ENTER}" }
[System.Windows.Forms.SendKeys]::SendWait($send)
Start-Sleep -Milliseconds 1200
Write-Output "enviado"
