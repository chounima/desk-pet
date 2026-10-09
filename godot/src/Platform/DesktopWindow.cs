// 桌面視窗：鋪滿工作區、不出現在工作列；平常點擊穿透到桌面，游標移到角色身上才接收滑鼠
//
// 做法：視窗加上 WS_EX_LAYERED，再依游標位置切換 WS_EX_TRANSPARENT（滑鼠穿透）。
// 不用 Godot 的 mouse_passthrough 多邊形：它在 Windows 上會用 SetWindowRgn 裁切畫面，
// 區域更新後畫面不會正確重繪（角色會被切掉或停在舊的畫面）。
using System;
using System.Runtime.InteropServices;
using Godot;

namespace DeskPet.Platform;

public static class DesktopWindow
{
    static IntPtr hwnd;
    static bool? clickThrough;

    /// <summary>工作區在螢幕上的位置與大小（實際像素）</summary>
    public static Rect2I Area { get; private set; }

    /// <summary>Windows 顯示縮放倍率（例如 4K 螢幕設 300% → 3），角色大小、速度都要乘上它</summary>
    public static float UiScale { get; private set; } = 1;

    public static void Setup()
    {
        var screen = DisplayServer.GetPrimaryScreen();
        UiScale = Mathf.Max(1f, DisplayServer.ScreenGetDpi(screen) / 96f);
        // 高度少 1px：無邊框視窗和螢幕一樣大時，顯示卡驅動會把它當成獨佔全螢幕，透明視窗畫面就不會更新
        var usable = DisplayServer.ScreenGetUsableRect(screen);
        Area = new Rect2I(usable.Position, usable.Size - new Vector2I(0, 1));
        DisplayServer.WindowSetPosition(Area.Position);
        DisplayServer.WindowSetSize(Area.Size);

        if (!OperatingSystem.IsWindows()) return;
        hwnd = (IntPtr)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle);
        // 不出現在工作列；加上 layered 才能用 WS_EX_TRANSPARENT 讓滑鼠穿透
        UpdateExStyle(s => (s | WS_EX_TOOLWINDOW | WS_EX_LAYERED) & ~WS_EX_APPWINDOW);
        SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
        SetClickThrough(true);
    }

    /// <summary>true = 滑鼠點擊穿透到底下的程式；false = 這個視窗接收滑鼠</summary>
    public static void SetClickThrough(bool on)
    {
        if (hwnd == IntPtr.Zero || clickThrough == on) return;
        clickThrough = on;
        UpdateExStyle(s => on ? s | WS_EX_TRANSPARENT : s & ~WS_EX_TRANSPARENT);
    }

    /// <summary>滑鼠游標在視窗中的位置（不論視窗是否穿透）</summary>
    public static Vector2 CursorInWindow() => DisplayServer.MouseGetPosition() - DisplayServer.WindowGetPosition();

    /// <summary>滑鼠左鍵目前是否按著（全域，游標不在視窗上也查得到）</summary>
    public static bool LeftButtonDown() => OperatingSystem.IsWindows() && (GetAsyncKeyState(0x01) & 0x8000) != 0;

    static void UpdateExStyle(Func<long, long> change)
    {
        var style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(change(style)));
    }

    const int GWL_EXSTYLE = -20;
    const long WS_EX_TRANSPARENT = 0x00000020, WS_EX_TOOLWINDOW = 0x00000080;
    const long WS_EX_LAYERED = 0x00080000, WS_EX_APPWINDOW = 0x00040000;
    const uint LWA_ALPHA = 0x2;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vKey);
}
