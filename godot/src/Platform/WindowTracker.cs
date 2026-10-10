// 偵測桌面上的視窗，把每個視窗沒被擋住的頂邊當成角色可以站的平台
//
// 用 GetTopWindow / GetWindow 依 Z 順序（上→下）走訪，不用 EnumWindows：
// Godot 的可卸載組件環境裡，交給 Windows 的 C# 回呼不會被執行。
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Godot;

namespace DeskPet.Platform;

/// <summary>視窗頂邊的一段平台（視窗座標）。WinX = 視窗左緣，用來算視窗移動了多少。</summary>
public readonly record struct WindowPlatform(IntPtr Hwnd, float Y, float X0, float X1, float WinX)
{
    public bool Contains(float x, float margin = 0) => x >= X0 + margin && x <= X1 - margin;
}

public static class WindowTracker
{
    static readonly HashSet<string> IgnoredClasses = new() { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd" };
    static List<WindowPlatform> platforms = new();

    public static IReadOnlyList<WindowPlatform> Platforms => platforms;

    /// <summary>除錯：上次掃描各步驟剩下幾個視窗</summary>
    public static string Stats { get; private set; } = "";

    /// <param name="self">桌寵自己的視窗（略過）</param>
    /// <param name="origin">桌寵視窗左上角的螢幕座標（換算成視窗座標用）</param>
    /// <param name="minWidth">平台最短寬度</param>
    /// <param name="minTop">平台離畫面頂端至少多遠（太高角色會被切掉）</param>
    public static void Refresh(IntPtr self, Rect2I area, float minWidth, float minTop)
    {
        if (!OperatingSystem.IsWindows()) return;
        var result = new List<WindowPlatform>();
        var above = new List<RECT>();      // 已經走訪過（在上層）的視窗，會擋住下面的視窗
        int total = 0, visible = 0, bounded = 0, candidates = 0, iconic = 0, cloaked = 0;

        for (var h = GetTopWindow(IntPtr.Zero); h != IntPtr.Zero; h = GetWindow(h, GW_HWNDNEXT))
        {
            total++;
            if (h == self || !IsWindowVisible(h)) continue;
            if (IsIconic(h)) { iconic++; continue; }
            if (IsCloaked(h)) { cloaked++; continue; }
            visible++;
            if (!TryGetBounds(h, out var r) || r.Right - r.Left <= 0 || r.Bottom - r.Top <= 0) continue;
            if (IgnoredClasses.Contains(ClassName(h))) continue;
            bounded++;

            if (IsPlatformCandidate(h, r, minWidth))
            {
                candidates++;
                var y = r.Top - area.Position.Y;
                var segments = new List<(float a, float b)> { (Math.Max(r.Left, area.Position.X), Math.Min(r.Right, area.End.X)) };
                foreach (var o in above)
                    if (o.Top < r.Top && o.Bottom > r.Top) segments = Subtract(segments, o.Left, o.Right);
                if (y >= minTop)
                    foreach (var (a, b) in segments)
                        if (b - a >= minWidth * 0.5f)
                            result.Add(new WindowPlatform(h, y, a - area.Position.X, b - area.Position.X, r.Left - area.Position.X));
            }
            above.Add(r);
        }
        platforms = result;
        Stats = $"total={total} iconic={iconic} cloaked={cloaked} visible={visible} bounded={bounded} candidates={candidates} platforms={result.Count}";
    }

    // 有標題、不是工具視窗、沒最大化、夠大的視窗才算平台
    static bool IsPlatformCandidate(IntPtr h, RECT r, float minWidth)
    {
        if (GetWindowTextLength(h) == 0 || IsZoomed(h)) return false;
        var ex = GetWindowLongPtr(h, GWL_EXSTYLE).ToInt64();
        if ((ex & WS_EX_TOOLWINDOW) != 0) return false;
        return r.Right - r.Left >= minWidth && r.Bottom - r.Top >= 80;
    }

    static List<(float a, float b)> Subtract(List<(float a, float b)> segments, float lo, float hi)
    {
        var result = new List<(float a, float b)>();
        foreach (var (a, b) in segments)
        {
            if (hi <= a || lo >= b) { result.Add((a, b)); continue; }
            if (lo > a) result.Add((a, lo));
            if (hi < b) result.Add((hi, b));
        }
        return result;
    }

    // 用 DWM 的實際外框（不含透明陰影），比 GetWindowRect 準
    static bool TryGetBounds(IntPtr h, out RECT r) =>
        DwmGetWindowAttribute(h, DWMWA_EXTENDED_FRAME_BOUNDS, out r, Marshal.SizeOf<RECT>()) == 0;

    // 被隱藏的 UWP 視窗、其他虛擬桌面上的視窗是 cloaked
    static bool IsCloaked(IntPtr h) =>
        DwmGetWindowAttribute(h, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0;

    static string ClassName(IntPtr h)
    {
        var sb = new StringBuilder(64);
        GetClassName(h, sb, sb.Capacity);
        return sb.ToString();
    }

    // ---------- Win32 ----------

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    const uint GW_HWNDNEXT = 2;
    const int GWL_EXSTYLE = -20;
    const long WS_EX_TOOLWINDOW = 0x00000080;
    const int DWMWA_EXTENDED_FRAME_BOUNDS = 9, DWMWA_CLOAKED = 14;

    [DllImport("user32.dll")] static extern IntPtr GetTopWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hWnd, StringBuilder name, int max);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT value, int size);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out int value, int size);
}
