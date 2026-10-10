// 多國語言：data/i18n/<語言>.json（扁平的 key → 文字），找不到的 key 用 zh-TW，再找不到就用程式裡給的預設文字
//
// 介面文字：I18n.T("menu.quit")、帶參數 I18n.T("today.keys", 1234) → "今天打了 {0} 下"
// 資料裡的名稱（商品、動作、角色、成就…）：I18n.Name("item.ball:beach", 原本的中文)，翻譯表沒有就用原文
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace DeskPet.Content;

public static class I18n
{
    public const string Default = "zh-TW";
    public static readonly (string Code, string Label)[] Languages = { ("zh-TW", "繁體中文"), ("en", "English"), ("ja", "日本語") };

    static Dictionary<string, string> current = new(), fallback = new();

    /// <summary>目前的語言代碼（zh-TW、en、ja）</summary>
    public static string Lang { get; private set; } = Default;

    /// <summary>auto = 依 Windows 語言：中文→繁中、日文→日文、其他→英文</summary>
    public static string Resolve(string setting)
    {
        if (setting != "auto") return setting;
        var locale = OS.GetLocaleLanguage();
        return locale switch { "zh" => "zh-TW", "ja" => "ja", _ => "en" };
    }

    public static void Load(string setting)
    {
        Lang = Resolve(setting);
        fallback = Read(Default);
        current = Lang == Default ? fallback : Read(Lang);
    }

    static Dictionary<string, string> Read(string lang)
    {
        var path = $"{ContentLoader.Root}/data/i18n/{lang}.json";
        if (!FileAccess.FileExists(path) && !System.IO.File.Exists(path)) return new();
        var d = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(ContentLoader.ReadText(path)) ?? new();
        var result = new Dictionary<string, string>();
        foreach (var (k, v) in d) if (v.ValueKind == JsonValueKind.String && !k.StartsWith('_')) result[k] = v.GetString()!;
        return result;
    }

    /// <summary>介面文字</summary>
    public static string T(string key, params object[] args)
    {
        var text = current.GetValueOrDefault(key) ?? fallback.GetValueOrDefault(key) ?? key;
        return args.Length > 0 ? string.Format(text, args) : text;
    }

    /// <summary>語言覆蓋檔的路徑：data/i18n/&lt;語言&gt;/&lt;相對路徑&gt;（繁中沒有覆蓋檔，回傳 null）</summary>
    public static string? OverlayPath(string relative) =>
        Lang == Default ? null : $"{ContentLoader.Root}/data/i18n/{Lang}/{relative}";

    // ---------- 資料名稱：載入後直接換成目前語言 ----------

    public static void LocalizeRigs(IEnumerable<RigData> rigs)
    {
        foreach (var rig in rigs)
        {
            rig.Name = Name($"char.{rig.Id}.name", rig.Name);
            rig.Description = Name($"char.{rig.Id}.desc", rig.Description);
            foreach (var (form, f) in rig.Forms ?? new())
            {
                f.Label = Name($"form.{form}", f.Label);
                if (f.Data != rig) { f.Data.Name = rig.Name; f.Data.Description = rig.Description; }
            }
        }
    }

    public static void LocalizeClips(IEnumerable<Dictionary<string, ClipData>> sets)
    {
        foreach (var set in sets)
            foreach (var clip in set.Values) clip.Label = Name($"anim.{clip.Name}", clip.Label);
    }

    public static void LocalizeProgression(ProgressionConfig cfg)
    {
        foreach (var (id, item) in cfg.Shop.Items) item.Label = Name($"item.{id}", item.Label);
        foreach (var cat in new List<string>(cfg.Shop.Categories.Keys)) cfg.Shop.Categories[cat] = Name($"cat.{cat}", cfg.Shop.Categories[cat]);
        foreach (var (key, rule) in cfg.Unlocks) if (rule.Hint != null) rule.Hint = Name($"hint.{key}", rule.Hint);
    }

    /// <summary>資料裡的名稱：翻譯表有就用翻譯，否則用原文（資料檔裡的繁中）</summary>
    public static string Name(string key, string original) =>
        Lang == Default ? original : current.GetValueOrDefault(key) ?? original;
}
