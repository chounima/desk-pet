// 依正在用的程式反應（使用者同意才開）：類別穩定幾秒後說一句話，並讓某些自動動作更常出現
// 類別與程式對應在 data/apps.json；只比對執行檔名稱，不存任何紀錄
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using DeskPet.Content;

namespace DeskPet.Game;

public sealed class AppConfig
{
    public Dictionary<string, Category> Categories { get; set; } = new();
    public float StableSeconds { get; set; } = 5;
    public float LineCooldownMinutes { get; set; } = 15;

    public sealed class Category
    {
        public List<string> Processes { get; set; } = new();
        public string? Line { get; set; }
        public Dictionary<string, float> Boost { get; set; } = new();
    }

    public static AppConfig Load() =>
        JsonSerializer.Deserialize<AppConfig>(ContentLoader.ReadText($"{ContentLoader.Root}/data/apps.json"),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
}

public sealed class AppAwareness
{
    readonly AppConfig cfg;
    readonly List<(Regex Pattern, string Category)> patterns = new();
    readonly Dictionary<string, DateTime> lastLine = new();
    string? candidate;
    float stableFor;

    /// <summary>目前穩定的類別（null = 沒有對應的類別）</summary>
    public string? Category { get; private set; }
    public bool Enabled { get; set; }

    /// <summary>類別改變時要說的台詞 key</summary>
    public event Action<string>? Said;

    public AppAwareness(AppConfig cfg)
    {
        this.cfg = cfg;
        foreach (var (name, c) in cfg.Categories)
            foreach (var p in c.Processes)
                patterns.Add((new Regex("^" + Regex.Escape(p).Replace("\\*", ".*") + "$", RegexOptions.IgnoreCase), name));
    }

    public string? Classify(string? process) =>
        process == null ? null : patterns.FirstOrDefault(x => x.Pattern.IsMatch(process)).Category;

    /// <param name="process">前景程式名稱（ActiveApp.ProcessName）</param>
    public void Update(float dt, string? process)
    {
        if (!Enabled) { Category = null; return; }
        var cat = Classify(process);
        if (cat != candidate) { candidate = cat; stableFor = 0; return; }
        stableFor += dt;
        if (stableFor < cfg.StableSeconds || cat == Category) return;
        Category = cat;
        if (cat == null || cfg.Categories[cat].Line is not { } line) return;
        var now = DateTime.Now;
        if (lastLine.TryGetValue(cat, out var t) && (now - t).TotalMinutes < cfg.LineCooldownMinutes) return;
        lastLine[cat] = now;
        Said?.Invoke(line);
    }

    /// <summary>自動行為的權重倍率（目前類別有 boost 的動作更常出現）</summary>
    public float Weight(string action) =>
        Category != null && cfg.Categories[Category].Boost.TryGetValue(action, out var w) ? w : 1;
}
