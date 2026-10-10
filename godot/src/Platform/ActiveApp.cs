// 前景程式的執行檔名稱（例如 code、chrome），只用來判斷類別，不讀視窗標題或內容
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeskPet.Platform;

public static class ActiveApp
{
    static uint lastPid;
    static string? lastName;

    /// <summary>前景視窗所屬程式的名稱（小寫、不含 .exe）；取不到時回傳 null</summary>
    public static string? ProcessName(IntPtr self)
    {
        if (!OperatingSystem.IsWindows()) return null;
        var fg = GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == self) return null;
        GetWindowThreadProcessId(fg, out var pid);
        if (pid == 0) return null;
        if (pid == lastPid) return lastName;          // 同一個程式就不用再查
        try
        {
            using var p = Process.GetProcessById((int)pid);
            lastName = p.ProcessName.ToLowerInvariant();
        }
        catch (Exception) { lastName = null; }        // 程式剛好結束、或權限不足
        lastPid = pid;
        return lastName;
    }

    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
}
