# 錄下某角色某動作（桌寵與網頁兩邊），排成比較圖：升級前後對照用
# 用法：powershell -File tools/record_clip.ps1 -Char sera -Clip walk [-Form quadruped] [-Frames 12] [-Vx 300] [-Out build/record/<名稱>]
#   -Vx：前半段以這個速度平移、之後突然停下（看尾巴、披布的物理甩動）
# 產出：<Out>/godot.png、<Out>/web.png（每張是 N 格的比較圖），<Out>/godot/frame_XX.png
param(
    [string]$Char = "sera",
    [string]$Clip = "walk",
    [string]$Form = "",
    [int]$Frames = 12,
    [float]$Vx = 0,
    [string]$Out = ""
)
$root = Split-Path $PSScriptRoot -Parent
if (-not $Out) { $Out = Join-Path $root "build\record\$Char$(if ($Form) { "-$Form" })-$Clip" }
$Out = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Out))     # Godot 會從自己的專案資料夾解讀相對路徑
New-Item -ItemType Directory -Force $Out | Out-Null
$godot = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -Filter "Godot_v4*_mono_win64_console.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $godot) { throw "找不到 Godot .NET 版" }

# 桌寵
$frameDir = Join-Path $Out "godot"
Remove-Item -Recurse -Force $frameDir -ErrorAction SilentlyContinue
$argList = @("--path", "`"$root\godot`"", "--", "--sandbox", "--mute", "--no-single", "--char=$Char", "--action=$Clip", "--record=`"$frameDir`"", "--record-frames=$Frames")
if ($Form) { $argList += "--form=$Form" }
if ($Vx) { $argList += "--record-vx=$Vx" }
$p = Start-Process -FilePath $godot.FullName -ArgumentList $argList -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $Out "godot.log")
Wait-Process -Id $p.Id -Timeout 60 -ErrorAction SilentlyContinue
Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
python "$root\tools\contact_sheet.py" $frameDir (Join-Path $Out "godot.png") --height 300

# 網頁
$page = "preview/record.html?char=$Char&clip=$Clip&frames=$Frames$(if ($Form) { "&form=$Form" })$(if ($Vx) { "&vx=$Vx" })"
& powershell -NoProfile -ExecutionPolicy Bypass -File "$root\tools\web_test.ps1" -Page $page -Shot (Join-Path $Out "web.png") | Select-Object -First 1
Write-Output "→ $Out"
