# 截取整個主螢幕（DPI 感知，4K / 高縮放也能截到完整畫面），可選擇裁切與縮小
# 用法：powershell -File tools/screenshot.ps1 -Out shot.png [-Crop "x,y,w,h"] [-MaxWidth 1600]
param(
    [Parameter(Mandatory = $true)][string]$Out,
    [string]$Crop = "",
    [int]$MaxWidth = 1600
)

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System.Runtime.InteropServices;
public static class Dpi { [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); }
"@
[Dpi]::SetProcessDPIAware() | Out-Null
Add-Type -AssemblyName System.Windows.Forms

$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$full = New-Object System.Drawing.Bitmap $b.Width, $b.Height
$g = [System.Drawing.Graphics]::FromImage($full)
$g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
$g.Dispose()

$src = New-Object System.Drawing.Rectangle 0, 0, $b.Width, $b.Height
if ($Crop) {
    $c = $Crop.Split(",") | ForEach-Object { [int]$_ }
    $src = New-Object System.Drawing.Rectangle $c[0], $c[1], $c[2], $c[3]
}
$scale = [Math]::Min(1.0, $MaxWidth / $src.Width)
$w = [int]($src.Width * $scale); $h = [int]($src.Height * $scale)
$outBmp = New-Object System.Drawing.Bitmap $w, $h
$g2 = [System.Drawing.Graphics]::FromImage($outBmp)
$g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g2.DrawImage($full, (New-Object System.Drawing.Rectangle 0, 0, $w, $h), $src, [System.Drawing.GraphicsUnit]::Pixel)
$outBmp.Save($Out)
$g2.Dispose(); $outBmp.Dispose(); $full.Dispose()
"screen $($b.Width)x$($b.Height) -> $Out ($w x $h)"
