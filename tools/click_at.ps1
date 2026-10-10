# 模擬滑鼠點擊螢幕上的某一點（實際像素座標，DPI 感知），用來測試右鍵選單等需要真的點擊的功能
# 用法：powershell -File tools/click_at.ps1 -X 1920 -Y 1900 [-Button right] [-Restore] [-MoveOnly]
#   -Restore：點完把游標移回原本的位置；-MoveOnly：只移動游標不點（例如讓子選單展開）
param([int]$X, [int]$Y, [ValidateSet("left", "right")][string]$Button = "left", [switch]$Restore, [switch]$MoveOnly)

Add-Type @"
using System.Runtime.InteropServices;
public static class Mouse {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, System.UIntPtr extra);
  public struct POINT { public int X, Y; }
}
"@
[Mouse]::SetProcessDPIAware() | Out-Null
$old = New-Object Mouse+POINT
[Mouse]::GetCursorPos([ref]$old) | Out-Null
[Mouse]::SetCursorPos($X, $Y) | Out-Null
Start-Sleep -Milliseconds 150        # 讓桌寵偵測到游標在角色上、關掉點擊穿透
if ($MoveOnly) { "moved to $X,$Y"; return }
$down, $up = if ($Button -eq "right") { 0x0008, 0x0010 } else { 0x0002, 0x0004 }
[Mouse]::mouse_event($down, 0, 0, 0, [System.UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[Mouse]::mouse_event($up, 0, 0, 0, [System.UIntPtr]::Zero)
if ($Restore) { Start-Sleep -Milliseconds 100; [Mouse]::SetCursorPos($old.X, $old.Y) | Out-Null }
"clicked $Button at $X,$Y"
