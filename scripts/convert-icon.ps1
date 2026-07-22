param(
    [string]$SourcePng = "C:\projetos\Kamui\brand\icon-512.png",
    [string]$OutIco = "C:\projetos\kamuit\kamuit.ico"
)

Add-Type -AssemblyName System.Drawing

# Redimensiona para 256x256 (max suportado pelo formato ICO) mantendo PNG como payload
$src = [System.Drawing.Image]::FromFile($SourcePng)
$bmp = New-Object System.Drawing.Bitmap 256, 256
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.DrawImage($src, 0, 0, 256, 256)
$g.Dispose()
$src.Dispose()

$ms = New-Object System.IO.MemoryStream
$bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
$pngBytes = $ms.ToArray()
$ms.Dispose()
$bmp.Dispose()

$fs = [System.IO.File]::Create($OutIco)
$bw = New-Object System.IO.BinaryWriter($fs)

# ICONDIR
$bw.Write([uint16]0)      # reserved
$bw.Write([uint16]1)      # type = icon
$bw.Write([uint16]1)      # count

# ICONDIRENTRY (width/height 0 = 256)
$bw.Write([byte]0)        # width  (0 = 256)
$bw.Write([byte]0)        # height (0 = 256)
$bw.Write([byte]0)        # colors
$bw.Write([byte]0)        # reserved
$bw.Write([uint16]1)      # planes
$bw.Write([uint16]32)     # bpp
$bw.Write([uint32]$pngBytes.Length)
$bw.Write([uint32]22)     # offset (6 + 16)
$bw.Write($pngBytes)

$bw.Dispose()
$fs.Dispose()
Write-Output "ICO criado: $OutIco ($($pngBytes.Length) bytes PNG payload)"
