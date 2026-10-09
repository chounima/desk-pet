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
        var lines = new Lines { fallback = Read("default") ?? new() };
        foreach (var id in characterIds)
            if (Read(id) is { } d) lines.byCharacter[id] = d;
        return lines;
    }

    static Dictionary<string, List<string>>? Read(string name)
    {
        var path = $"{ContentLoader.Root}/data/lines/{name}.json";
        if (!Godot.FileAccess.FileExists(path) && !File.Exists(path)) return null;
        return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(ContentLoader.ReadText(path));
    }

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
