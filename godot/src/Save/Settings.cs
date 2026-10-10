// 使用者設定與統計，存在 user://settings.json
// （Windows：%APPDATA%\Godot\app_userdata\Desk Pet\settings.json）
//
// 🔒 每日按鍵只存「次數」，不存按了什麼。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace DeskPet.Save;

public sealed class Settings
{
    const int KeepDays = 30;

    static readonly string FilePath = ProjectSettings.GlobalizePath("user://settings.json");
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public string Character { get; set; } = "";
    /// <summary>每個角色選的型態 { "choupaca": "quadruped" }</summary>
    public Dictionary<string, string> Forms { get; set; } = new();
    /// <summary>大小倍率（見 Tuning.Sizes）</summary>
    public float Size { get; set; } = 1f;
    public bool Auto { get; set; } = true;
    public bool Sound { get; set; } = true;
    /// <summary>其他程式全螢幕時自動隱藏</summary>
    public bool AutoHideFullscreen { get; set; } = true;
    /// <summary>大小跟著螢幕高度（否則跟著 Windows 縮放比例）</summary>
    public bool FitScreen { get; set; }
    /// <summary>開發者模式：設定視窗多一個「開發者」分頁</summary>
    public bool DevMode { get; set; }
    /// <summary>音樂音量 0～1、隨機播放、播完從頭</summary>
    public float MusicVolume { get; set; } = 0.6f;
    public bool MusicShuffle { get; set; }
    public bool MusicLoop { get; set; } = true;
    /// <summary>專注／休息分鐘數（null = data/focus.json 的預設）</summary>
    public float? FocusWork { get; set; }
    public float? FocusBreak { get; set; }
    /// <summary>偵測正在用的程式（預設關閉，使用者同意才開）</summary>
    public bool AppAwareness { get; set; }
    /// <summary>每個角色穿的自訂衣服 { 角色: 衣服資料夾名 }</summary>
    public Dictionary<string, string> Outfits { get; set; } = new();
    /// <summary>顯示在哪個螢幕（-1 = 主螢幕）</summary>
    public int Screen { get; set; } = -1;
    /// <summary>每日按鍵次數 { "2026-10-10": 1234 }，保留最近 30 天</summary>
    public SortedDictionary<string, int> DailyKeys { get; set; } = new();

    bool dirty;

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Options) ?? new Settings();
        }
        catch (Exception e)
        {
            GD.PushWarning($"設定檔讀取失敗，改用預設值：{e.Message}");
        }
        return new Settings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Options));
        dirty = false;
    }

    public void MarkDirty() => dirty = true;

    public void SaveIfDirty()
    {
        if (dirty) Save();
    }

    static string Today => DateTime.Now.ToString("yyyy-MM-dd");

    [JsonIgnore]
    public int TodayKeys => DailyKeys.TryGetValue(Today, out var n) ? n : 0;

    public void AddKeys(int count)
    {
        if (count <= 0) return;
        DailyKeys[Today] = TodayKeys + count;
        while (DailyKeys.Count > KeepDays) DailyKeys.Remove(DailyKeys.Keys.First());
        dirty = true;
    }
}
