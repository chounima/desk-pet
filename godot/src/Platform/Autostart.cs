// 開機自動啟動：寫入目前使用者的啟動項目（HKCU\Software\Microsoft\Windows\CurrentVersion\Run）
using System;
using Godot;
using Microsoft.Win32;

namespace DeskPet.Platform;

public static class Autostart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "DeskPet";

    public static bool IsEnabled()
    {
        if (!OperatingSystem.IsWindows()) return false;
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string;
    }

    public static void Set(bool on)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (on) key.SetValue(ValueName, Command());
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>
    /// 開機時要執行的指令。開發中：Godot 執行檔 + 專案路徑；匯出後：遊戲執行檔本身。
    /// 一律用沒有主控台視窗的版本，開機時才不會跳出黑色視窗。
    /// </summary>
    static string Command()
    {
        var exe = OS.GetExecutablePath().Replace("_console.exe", ".exe").Replace('/', '\\');
        if (OS.HasFeature("template")) return $"\"{exe}\"";
        var project = ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\').Replace('/', '\\');
        return $"\"{exe}\" --path \"{project}\"";
    }
}
