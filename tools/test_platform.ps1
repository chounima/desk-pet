# 開一個測試視窗當平台：MoveAt 秒後移到新位置，CloseAt 秒後關閉（測試桌寵站在視窗上、跟著移動、掉下來）
# 座標為實際像素（DPI 感知）。用法：powershell -File tools/test_platform.ps1 -X 2400 -Y 1200
param(
    [int]$X = 2400, [int]$Y = 1200, [int]$W = 900, [int]$H = 500,
    [int]$MoveX = 1400, [int]$MoveY = 1000, [int]$MoveAt = 12, [int]$CloseAt = 18
)

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Dpi {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("kernel32.dll")] public static extern IntPtr GetConsoleWindow();
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
}
"@
[Dpi]::SetProcessDPIAware() | Out-Null
# 藏起這個腳本自己的主控台視窗，畫面上只留測試平台（不然桌寵可能跳到主控台上）
[Dpi]::ShowWindow([Dpi]::GetConsoleWindow(), 0) | Out-Null
Add-Type -AssemblyName System.Windows.Forms

$form = New-Object System.Windows.Forms.Form
$form.Text = "DeskPet test platform"
$form.StartPosition = "Manual"
$form.Location = New-Object System.Drawing.Point $X, $Y
$form.Size = New-Object System.Drawing.Size $W, $H
$form.BackColor = [System.Drawing.Color]::FromArgb(60, 64, 90)

$start = Get-Date
$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 200
$timer.Add_Tick({
    $t = ((Get-Date) - $start).TotalSeconds
    if ($t -ge $MoveAt -and $form.Left -ne $MoveX) { $form.Location = New-Object System.Drawing.Point $MoveX, $MoveY }
    if ($t -ge $CloseAt) { $timer.Stop(); $form.Close() }
})
$timer.Start()
[System.Windows.Forms.Application]::Run($form)
"test platform closed"
