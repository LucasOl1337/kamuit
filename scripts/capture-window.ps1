param(
    [string]$ProcessName = "Kamui",
    [string]$OutFile = "C:\projetos\kamuit\debug-kamui-window.png"
)

$sig = @"
using System;
using System.Runtime.InteropServices;
public class WinApi {
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
}
"@
Add-Type -TypeDefinition $sig -ReferencedAssemblies System.Drawing

$p = Get-Process $ProcessName -ErrorAction SilentlyContinue
if (-not $p) { Write-Output "PROCESSO $ProcessName NAO ENCONTRADO"; exit 1 }

$HWND_TOPMOST = New-Object IntPtr(-1)
$HWND_NOTOPMOST = New-Object IntPtr(-2)
$SWP_NOMOVE = 0x0002
$SWP_NOSIZE = 0x0001

[WinApi]::SetWindowPos($p.MainWindowHandle, $HWND_TOPMOST, 0, 0, 0, 0, $SWP_NOMOVE -bor $SWP_NOSIZE) | Out-Null
Start-Sleep -Milliseconds 800

$r = New-Object WinApi+RECT
[WinApi]::GetWindowRect($p.MainWindowHandle, [ref]$r) | Out-Null
$w = $r.Right - $r.Left
$h = $r.Bottom - $r.Top
Write-Output "rect: $($r.Left),$($r.Top) ${w}x${h} visible=$([WinApi]::IsWindowVisible($p.MainWindowHandle)) title='$($p.MainWindowTitle)'"

Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)

# PrintWindow captura a janela especifica, ignorando z-order (DirectX pode sair preto)
$hdc = $g.GetHdc()
$ok = [WinApi]::PrintWindow($p.MainWindowHandle, $hdc, 2)
$g.ReleaseHdc($hdc)
$bmp.Save($OutFile)
$g.Dispose()
$bmp.Dispose()

[WinApi]::SetWindowPos($p.MainWindowHandle, $HWND_NOTOPMOST, 0, 0, 0, 0, $SWP_NOMOVE -bor $SWP_NOSIZE) | Out-Null
Write-Output "saved (PrintWindow=$ok): $OutFile"
