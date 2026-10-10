// 台詞：data/lines/<角色>.json，角色沒有的台詞改用 data/lines/default.json
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;

namespace DeskPet.Content;

public sealed class Lines
{
    readonly Dictionary<string, Dictionary<string, List<string>>> byCharacter = new();
    Dictionary<string, List<string>> fallback = new();

    public static Lines Load(IEnumerable<string> characterIds)
    {
        var lines = new Lines { fallback = Merge(Read("default"), ReadOverlay("default")) ?? new() };
        foreach (var id in characterIds)
            if (Merge(Read(id), ReadOverlay(id)) is { } d) lines.byCharacter[id] = d;
        return lines;
    }

    // 目前語言的台詞（data/i18n/<語言>/lines/<角色>.json）蓋在繁中上面；沒翻到的 key 還是用繁中
    static Dictionary<string, List<string>>? Merge(Dictionary<string, List<string>>? baseLines, Dictionary<string, List<string>>? overlay)
    {
        if (overlay == null) return baseLines;
        var merged = new Dictionary<string, List<string>>(baseLines ?? new());
        foreach (var (k, v) in overlay) merged[k] = v;
        return merged;
    }

    static Dictionary<string, List<string>>? ReadOverlay(string name) =>
        I18n.OverlayPath($"lines/{name}.json") is { } path ? ReadPath(path) : null;

    static Dictionary<string, List<string>>? Read(string name) => ReadPath($"{ContentLoader.Root}/data/lines/{name}.json");

    static Dictionary<string, List<string>>? ReadPath(string path)
    {
        if (!Godot.FileAccess.FileExists(path) && !File.Exists(path)) return null;
        return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(ContentLoader.ReadText(path));
    }

    /// <summary>自訂角色的台詞</summary>
    public void Set(string characterId, Dictionary<string, List<string>> lines) => byCharacter[characterId] = lines;

    /// <summary>隨機挑一句；角色和預設都沒有這個 key 時回傳 null</summary>
    public string? Pick(string characterId, string key)
    {
        if (byCharacter.TryGetValue(characterId, out var mine) && mine.TryGetValue(key, out var list) && list.Count > 0)
            return list[(int)(GD.Randi() % (uint)list.Count)];
        if (fallback.TryGetValue(key, out var common) && common.Count > 0)
            return common[(int)(GD.Randi() % (uint)common.Count)];
        return null;
    }
}
