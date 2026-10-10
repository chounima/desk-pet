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
