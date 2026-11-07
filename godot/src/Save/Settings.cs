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
    /// <summary>不要自動彈的曲子（Track.Id：song:twinkle、file:xxx.mp3）</summary>
    public HashSet<string> MusicDisabled { get; set; } = new();
    /// <summary>專注／休息分鐘數（null = data/focus.json 的預設）</summary>
    public float? FocusWork { get; set; }
    public float? FocusBreak { get; set; }
    /// <summary>偵測正在用的程式（預設關閉，使用者同意才開）</summary>
    public bool AppAwareness { get; set; }
    /// <summary>每個角色穿的自訂衣服 { 角色: 衣服資料夾名 }</summary>
    public Dictionary<string, string> Outfits { get; set; } = new();
    /// <summary>顯示在哪個螢幕（-1 = 主螢幕）</summary>
    public int Screen { get; set; } = -1;
    /// <summary>介面語言：auto（依 Windows）、zh-TW、en、ja</summary>
    public string Language { get; set; } = "auto";
    /// <summary>多人功能（直播／會議模式、Twitch、Discord、客人上限…）</summary>
    public GuestSettings Guests { get; set; } = new();
    /// <summary>半身 VTuber 模式</summary>
    public VtuberSettings Vtuber { get; set; } = new();
    /// <summary>還不認得或舊格式的欄位：讀進來保留（存檔時寫回去），舊格式由 Migrate 搬到各功能的段落</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Other { get; set; }
    /// <summary>每日按鍵次數 { "2026-10-10": 1234 }，保留最近 30 天</summary>
    public SortedDictionary<string, int> DailyKeys { get; set; } = new();

    bool dirty;

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return Parse(File.ReadAllText(FilePath));
        }
        catch (Exception e)
        {
            GD.PushWarning($"設定檔讀取失敗，改用預設值：{e.Message}");
        }
        return new Settings();
    }

    /// <summary>從 JSON 讀設定，舊格式自動搬到新的段落</summary>
    public static Settings Parse(string json)
    {
        var settings = JsonSerializer.Deserialize<Settings>(json, Options) ?? new Settings();
        settings.Migrate();
        return settings;
    }

    // 2026-11-07 前多人功能的設定攤平放在最上層：搬到 Guests
    static readonly Dictionary<string, string> LegacyGuestKeys = new()
    {
        ["LiveMode"] = "LiveMode", ["TwitchChannel"] = "TwitchChannel", ["DiscordClientId"] = "DiscordClientId",
        ["DiscordClientSecret"] = "DiscordClientSecret", ["DiscordAccessToken"] = "DiscordAccessToken", ["DiscordRefreshToken"] = "DiscordRefreshToken",
        ["MicTalk"] = "MicTalk", ["MeetingAudio"] = "MeetingAudio", ["MaxGuests"] = "MaxGuests", ["GuestCharacters"] = "Characters",
    };

    void Migrate()
    {
        if (Other == null) return;
        var moved = new System.Text.Json.Nodes.JsonObject();
        foreach (var (oldKey, newKey) in LegacyGuestKeys)
            if (Other.Remove(oldKey, out var value)) moved[newKey] = System.Text.Json.Nodes.JsonNode.Parse(value.GetRawText());
        if (moved.Count == 0) return;
        // 舊的值蓋在預設值上（新段落已經有的欄位以新段落為準）
        var current = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(Guests, Options))!.AsObject();
        foreach (var (k, v) in moved) current[k] = v?.DeepClone();
        Guests = JsonSerializer.Deserialize<GuestSettings>(current.ToJsonString(), Options) ?? new GuestSettings();
        if (Other.Count == 0) Other = null;
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
