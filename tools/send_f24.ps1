# 送出 F24 按鍵（一般鍵盤沒有、沒有程式會用的鍵），用來測試桌寵的全域鍵盤偵測而不會打字到其他程式
# 用法：powershell -File tools/send_f24.ps1 -Count 20 -IntervalMs 80
param([int]$Count = 20, [int]$IntervalMs = 80)

Add-Type @"
using System.Runtime.InteropServices;
public static class Kb { [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, System.UIntPtr extra); }
"@
$VK_F24 = 0x87
for ($i = 0; $i -lt $Count; $i++) {
    [Kb]::keybd_event($VK_F24, 0, 0, [System.UIntPtr]::Zero)        # 按下
    [Kb]::keybd_event($VK_F24, 0, 2, [System.UIntPtr]::Zero)        # 放開
    Start-Sleep -Milliseconds $IntervalMs
}
"sent $Count x F24"
