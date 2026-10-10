// 各角色的排球能力與台詞（data/volley.json，與網頁預覽共用）
using System.Collections.Generic;
using System.Text.Json;
using DeskPet.Content;
using Godot;

namespace DeskPet.Game.Minigames;

public sealed class VolleySkill
{
    public string Title { get; set; } = "";
    public float Speed { get; set; } = 1;     // 跑速倍率
    public float Miss { get; set; } = 1;      // 漏接機率倍率
    public float Spike { get; set; } = 0.3f;  // 能殺球時選擇殺球的機率
    public float Dive { get; set; } = 0.6f;   // 來不及時嘗試魚躍的機率
    public float Power { get; set; } = 1;     // 殺球速度倍率
    public Dictionary<string, List<string>> Lines { get; set; } = new();

    public string? Pick(string key) =>
        Lines.TryGetValue(key, out var list) && list.Count > 0 ? list[(int)(GD.Randi() % (uint)list.Count)] : null;
}

public static class VolleySkills
{
    static readonly Dictionary<string, JsonElement> Data = new();
    static readonly Dictionary<string, JsonElement> Overlay = new();      // 目前語言的稱號與台詞（data/i18n/<語言>/volley.json）

    public static void Load()
    {
        Read($"{ContentLoader.Root}/data/volley.json", Data);
        if (I18n.OverlayPath("volley.json") is { } path && (Godot.FileAccess.FileExists(path) || System.IO.File.Exists(path)))
            Read(path, Overlay);
    }

    static void Read(string path, Dictionary<string, JsonElement> into)
    {
        using var doc = JsonDocument.Parse(ContentLoader.ReadText(path));
        foreach (var p in doc.RootElement.EnumerateObject())
            if (p.Value.ValueKind == JsonValueKind.Object) into[p.Name] = p.Value.Clone();
    }

    /// <summary>角色的能力：以 default 為底，再套用角色自己有寫的欄位</summary>
    public static VolleySkill For(string id)
    {
        var skill = new VolleySkill();
        if (Data.TryGetValue("default", out var d)) Apply(skill, d);
        if (Data.TryGetValue(id, out var c)) Apply(skill, c);
        if (Overlay.TryGetValue("default", out var od)) Apply(skill, od);
        if (Overlay.TryGetValue(id, out var oc)) Apply(skill, oc);
        return skill;
    }

    static void Apply(VolleySkill s, JsonElement e)
    {
        if (e.TryGetProperty("title", out var v)) s.Title = v.GetString() ?? s.Title;
        if (e.TryGetProperty("speed", out v)) s.Speed = v.GetSingle();
        if (e.TryGetProperty("miss", out v)) s.Miss = v.GetSingle();
        if (e.TryGetProperty("spike", out v)) s.Spike = v.GetSingle();
        if (e.TryGetProperty("dive", out v)) s.Dive = v.GetSingle();
        if (e.TryGetProperty("power", out v)) s.Power = v.GetSingle();
        if (e.TryGetProperty("lines", out v))
            foreach (var line in v.EnumerateObject())
                s.Lines[line.Name] = JsonSerializer.Deserialize<List<string>>(line.Value.GetRawText()) ?? new();
    }
}
