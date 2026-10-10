// 偵測前景視窗是否全螢幕（玩遊戲、看影片全螢幕、簡報），桌寵這時要自動躲起來
//
// 判斷方式：前景視窗的範圍蓋住它所在螢幕的整個範圍（包含工作列）。
// 最大化的一般視窗不會蓋住工作列，所以不算；桌面本身（Progman / WorkerW）也排除。
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DeskPet.Platform;

public static class FullscreenWatcher
{
    static readonly string[] IgnoredClasses = { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd" };

    /// <summary>目前是否有別的程式在全螢幕；self 是桌寵自己的視窗</summary>
    public static bool IsFullscreenActive(IntPtr self)
    {
        if (!OperatingSystem.IsWindows()) return false;
        var fg = GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == self) return false;
        var cls = new StringBuilder(64);
        GetClassName(fg, cls, cls.Capacity);
        if (Array.IndexOf(IgnoredClasses, cls.ToString()) >= 0) return false;
        if (!GetWindowRect(fg, out var r)) return false;
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST), ref info)) return false;
        var m = info.rcMonitor;
        return r.Left <= m.Left && r.Top <= m.Top && r.Right >= m.Right && r.Bottom >= m.Bottom;
    }

    const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO info);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hWnd, StringBuilder name, int max);
}
