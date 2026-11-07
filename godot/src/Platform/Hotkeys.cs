// 全域快捷鍵（半身模式的表情鍵，例如 Ctrl+Alt+1）：每幀用 GetAsyncKeyState 查設定裡用到的按鍵，偵測「剛按下」
// 只認設定的組合鍵，不記錄其他按鍵。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace DeskPet.Platform;

public sealed class Hotkeys
{
    const int VkShift = 0x10, VkCtrl = 0x11, VkAlt = 0x12;

    sealed record Combo(string Name, bool Ctrl, bool Alt, bool Shift, int Key);

    readonly List<Combo> combos = new();
    readonly HashSet<string> down = new();

    /// <summary>names：「Ctrl+Alt+1」「Shift+F5」這種字串；認不得的略過</summary>
    public Hotkeys(IEnumerable<string> names)
    {
        foreach (var name in names)
            if (Parse(name) is { } c) combos.Add(c);
    }

    public int Count => combos.Count;

    /// <summary>這一幀剛按下的組合鍵</summary>
    public List<string> Poll()
    {
        var pressed = new List<string>();
        if (!OperatingSystem.IsWindows()) return pressed;
        bool ctrl = Down(VkCtrl), alt = Down(VkAlt), shift = Down(VkShift);
        foreach (var c in combos)
        {
            var now = Down(c.Key) && c.Ctrl == ctrl && c.Alt == alt && c.Shift == shift;
            if (now && down.Add(c.Name)) pressed.Add(c.Name);
            else if (!now) down.Remove(c.Name);
        }
        return pressed;
    }

    static Combo? Parse(string name)
    {
        var parts = name.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;
        var mods = parts[..^1].Select(p => p.ToLowerInvariant()).ToHashSet();
        var key = parts[^1].ToUpperInvariant();
        int vk;
        if (key.Length == 1 && key[0] is >= '0' and <= '9' or >= 'A' and <= 'Z') vk = key[0];
        else if (key.StartsWith('F') && int.TryParse(key[1..], out var f) && f is >= 1 and <= 12) vk = 0x70 + f - 1;
        else return null;
        return new Combo(name, mods.Contains("ctrl"), mods.Contains("alt"), mods.Contains("shift"), vk);
    }

    static bool Down(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vKey);
}
