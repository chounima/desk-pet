# 查詢螢幕某點下方是哪個程式的視窗（用來驗證桌寵的點擊穿透）
# 用法：powershell -File tools/window_at.ps1 -X 200 -Y 1000
param([int]$X, [int]$Y)

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win {
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
}
"@
[Win]::SetProcessDPIAware() | Out-Null
$p = New-Object Win+POINT
$p.X = $X; $p.Y = $Y
$h = [Win]::GetAncestor([Win]::WindowFromPoint($p), 2)
$procId = 0
[Win]::GetWindowThreadProcessId($h, [ref]$procId) | Out-Null
"($X,$Y) -> $((Get-Process -Id $procId).ProcessName)"
