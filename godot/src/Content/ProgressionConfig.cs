// data/progression.json：收集系統與商店的所有數值（與網頁預覽共用）
// 開發者模式改過的參數存在 user://progression.override.json，載入時優先使用
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;

namespace DeskPet.Content;

public sealed class ProgressionConfig
{
    public PointsConfig Points { get; set; } = new();
    public AffectionConfig Affection { get; set; } = new();
    public StartConfig Start { get; set; } = new();
    /// <summary>解鎖條件：character:&lt;id&gt; 或 form:&lt;id&gt;/&lt;型態&gt;（_ 開頭的是說明，載入後移除）</summary>
    public Dictionary<string, UnlockRule> Unlocks { get; set; } = new();
    public ShopConfig Shop { get; set; } = new();

    public sealed class PointsConfig
    {
        public TypingRule Typing { get; set; } = new();
        public InteractionRule Interaction { get; set; } = new();
        public VolleyRule Volley { get; set; } = new();
        public LoginRule Login { get; set; } = new();
        public int LevelUp { get; set; } = 20;
    }

    public sealed class TypingRule { public int Every { get; set; } = 200; public int Gain { get; set; } = 1; public int DailyMax { get; set; } = 50; }
    public sealed class InteractionRule { public int Gain { get; set; } = 1; public int DailyMax { get; set; } = 10; }
    public sealed class VolleyRule { public int EveryRally { get; set; } = 5; public int Gain { get; set; } = 1; public int RecordBonus { get; set; } = 5; public int DailyMax { get; set; } = 30; }
    public sealed class LoginRule { public int Base { get; set; } = 10; public int StreakBonus { get; set; } = 2; public int MaxBonus { get; set; } = 20; }

    public sealed class AffectionConfig
    {
        public int CompanionPerMinute { get; set; } = 1;
        public int CompanionDailyMax { get; set; } = 120;
        public int Interaction { get; set; } = 2;
        public int InteractionDailyMax { get; set; } = 30;
        public int VolleyEveryRally { get; set; } = 5;
        public int VolleyGain { get; set; } = 1;
        public int VolleyDailyMax { get; set; } = 30;
        public List<int> Levels { get; set; } = new() { 0, 60, 180, 400, 800, 1500 };
    }

    public sealed class StartConfig { public List<string> Characters { get; set; } = new(); }

    public sealed class AffectionReq { public string Char { get; set; } = ""; public int Level { get; set; } }

    public sealed class UnlockRule
    {
        public AffectionReq? Affection { get; set; }
        public int? Cost { get; set; }
        public string? Hint { get; set; }
    }

    public sealed class ShopConfig
    {
        public Dictionary<string, string> Categories { get; set; } = new();
        public Dictionary<string, string> Defaults { get; set; } = new();
        public Dictionary<string, ShopItem> Items { get; set; } = new();
    }

    public sealed class ShopItem
    {
        public string Category { get; set; } = "";
        public string Label { get; set; } = "";
        public int Price { get; set; }
        public string? Prop { get; set; }
        public string? Symbol { get; set; }
        public string? Color { get; set; }
        public string? Style { get; set; }
        public string? Action { get; set; }
        public string? Icon { get; set; }
        public string? Song { get; set; }
        public string? Accessory { get; set; }
        public string? Slot { get; set; }
        public UnlockRule? Requires { get; set; }
    }

    // ---------- 載入 ----------

    public const string OverridePath = "user://progression.override.json";
    static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>原始的 data/progression.json 文字</summary>
    public static string DefaultText() => ContentLoader.ReadText($"{ContentLoader.Root}/data/progression.json");

    /// <summary>目前生效的文字：有開發者覆寫就用覆寫</summary>
    public static string CurrentText()
    {
        var path = ProjectSettings.GlobalizePath(OverridePath);
        return File.Exists(path) ? File.ReadAllText(path) : DefaultText();
    }

    /// <summary>解析失敗會丟出 JsonException（開發者模式顯示錯誤訊息用）</summary>
    public static ProgressionConfig Parse(string json)
    {
        // 說明欄位（"_說明"，值是字串）放在字典裡會反序列化失敗，先全部拿掉
        var node = JsonNode.Parse(json) ?? throw new JsonException("空的 JSON");
        StripNotes(node);
        var cfg = node.Deserialize<ProgressionConfig>(Options) ?? new();
        I18n.LocalizeProgression(cfg);
        return cfg;
    }

    static void StripNotes(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(kv => kv.Key).Where(k => k.StartsWith('_')).ToList()) obj.Remove(key);
            foreach (var (_, child) in obj) if (child != null) StripNotes(child);
        }
        else if (node is JsonArray arr)
            foreach (var child in arr) if (child != null) StripNotes(child);
    }

    public static ProgressionConfig Load()
    {
        try { return Parse(CurrentText()); }
        catch (JsonException e)
        {
            GD.PushWarning($"[DeskPet] 開發者覆寫的參數壞掉了，改用預設：{e.Message}");
            return Parse(DefaultText());
        }
    }

    public static void SaveOverride(string json) => File.WriteAllText(ProjectSettings.GlobalizePath(OverridePath), json);

    public static void ClearOverride()
    {
        var path = ProjectSettings.GlobalizePath(OverridePath);
        if (File.Exists(path)) File.Delete(path);
    }
}
