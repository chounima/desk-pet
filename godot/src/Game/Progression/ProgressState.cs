// 收集系統的存檔內容（與網頁版 localStorage 的格式相同），存在 user://progress.json
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;

namespace DeskPet.Game.Progression;

public sealed class ProgressState
{
    public int Points { get; set; }
    public Dictionary<string, int> Xp { get; set; } = new();
    public List<string> Unlocked { get; set; } = new();
    public List<string> Owned { get; set; } = new();
    public Dictionary<string, string> Equipped { get; set; } = new();
    /// <summary>配件：{ 角色: { 部位: 商品 id } }</summary>
    public Dictionary<string, Dictionary<string, string>> Wear { get; set; } = new();
    public Dictionary<string, int> Best { get; set; } = new();
    public LoginInfo Login { get; set; } = new();
    public DayInfo Day { get; set; } = new();

    public sealed class LoginInfo
    {
        public string? Last { get; set; }
        public int Streak { get; set; }
    }

    public sealed class DayInfo
    {
        public string? Date { get; set; }
        public int Keys { get; set; }
        public Dictionary<string, int> Pts { get; set; } = new() { ["typing"] = 0, ["interaction"] = 0, ["volley"] = 0 };
        /// <summary>今日各來源給了多少好感度：{ companion: { 角色: 數量 }, interaction: …, volley: … }</summary>
        public Dictionary<string, Dictionary<string, int>> Xp { get; set; } = NewXp();

        public static Dictionary<string, Dictionary<string, int>> NewXp() =>
            new() { ["companion"] = new(), ["interaction"] = new(), ["volley"] = new() };
    }

    // ---------- 存讀檔 ----------

    const string FilePath = "user://progress.json";
    static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static ProgressState Load()
    {
        var path = ProjectSettings.GlobalizePath(FilePath);
        try
        {
            if (File.Exists(path)) return JsonSerializer.Deserialize<ProgressState>(File.ReadAllText(path), Options) ?? new();
        }
        catch (Exception e)
        {
            // 存檔壞掉：備份一份再重新開始，避免直接蓋掉
            GD.PushWarning($"[DeskPet] 收集進度讀取失敗，重新開始：{e.Message}");
            File.Copy(path, path + ".broken", overwrite: true);
        }
        return new();
    }

    public void Save()
    {
        var path = ProjectSettings.GlobalizePath(FilePath);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, Options));
        File.Move(tmp, path, overwrite: true);       // 先寫暫存檔再換掉，寫到一半當機也不會壞
    }
}
