// 全域鍵盤偵測：在任何程式打字，寵物都收得到
//
// 做法：背景執行緒每 5ms 用 GetAsyncKeyState 查一次所有按鍵，偵測「剛按下」的瞬間。
// 不用 SetWindowsHookEx：Godot 把遊戲程式載入可卸載的組件環境（為了腳本熱重載），
// 在這種環境裡交給 Windows 的 C# 回呼不會被執行（Hook 裝得起來但永遠收不到事件）。
//
// 🔒 隱私：按鍵「當下」分類成少數幾種（一般字元、Enter、Backspace、Ctrl+S），
//    分類完就丟掉，不記錄是哪個鍵、不組成文字、不存檔、不上傳。
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;

namespace DeskPet.Platform;

public static class GlobalInput
{
    public enum KeyKind { Char, Enter, Backspace, Save }

    const int PollMs = 5;

    static readonly ConcurrentQueue<KeyKind> Queue = new();
    static readonly bool[] Down = new bool[256];      // 只用來偵測「剛按下」與過濾按住不放
    static Thread? thread;
    static volatile bool running;

    public static bool Running => running;

    public static void Start()
    {
        if (thread != null || !OperatingSystem.IsWindows()) return;
        running = true;
        thread = new Thread(Run) { IsBackground = true, Name = "GlobalInput" };
        thread.Start();
    }

    public static void Stop() => running = false;

    public static bool TryDequeue(out KeyKind kind) => Queue.TryDequeue(out kind);

    static void Run()
    {
        while (running)
        {
            for (var vk = 0x08; vk < 0xFF; vk++)
            {
                if (IsMouseButton(vk)) continue;
                var state = GetAsyncKeyState(vk);
                var pressed = (state & 0x8000) != 0;
                // 低位元 = 上次查詢後曾被按過：補抓 5ms 內按下又放開的極快按鍵
                var tapped = (state & 0x0001) != 0;
                if ((pressed || tapped) && !Down[vk] && Classify(vk) is { } kind) Queue.Enqueue(kind);
                Down[vk] = pressed;
            }
            Thread.Sleep(PollMs);
        }
    }

    static bool IsMouseButton(int vk) => vk is 0x01 or 0x02 or 0x04 or 0x05 or 0x06;

    static KeyKind? Classify(int vk)
    {
        var ctrl = Down[0x11] || Down[0xA2] || Down[0xA3];
        return vk switch
        {
            0x0D => KeyKind.Enter,
            0x08 => KeyKind.Backspace,
            0x53 when ctrl => KeyKind.Save,                          // Ctrl+S
            0x10 or 0x11 or 0x12 or 0x14 or 0x5B or 0x5C => null,   // Shift/Ctrl/Alt/CapsLock/Win 不算
            >= 0xA0 and <= 0xA5 => null,                             // 左右 Shift/Ctrl/Alt
            _ => KeyKind.Char,
        };
    }

    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vKey);
}
