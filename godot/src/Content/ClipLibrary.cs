// 動作庫：{ 體型: { 動作名: clip } }，再加上角色專屬動作（網頁版 preview/js/core/loader.js 規則一樣）
//
// 找動作的順序（後面蓋前面）：共用 data/animations/ → 體型 data/animations/<體型>/ → 角色 data/animations/characters/<角色>/
// → 角色＋型態 data/animations/characters/<角色>/forms/<型態>/。哪些角色有專屬動作列在 data/animations/characters/index.json。
// 角色專屬檔可以是完整動作，也可以只寫差異：{ "extends": "walk", "tracks": { 只列要改的軌道 } }，其他沿用下一層的 walk。
// 合併結果依「角色｜型態」快取，所有用同一個角色的寵物（主角色、客人）共用。
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DeskPet.Content;

public sealed class ClipLibrary : Dictionary<string, Dictionary<string, ClipData>>
{
    /// <summary>{ 角色: { clips: [...], forms: { 型態: [...] } } }</summary>
    public sealed class CharacterEntry
    {
        public List<string> Clips { get; set; } = new();
        public Dictionary<string, List<string>> Forms { get; set; } = new();
    }

    static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    readonly string dir;
    readonly Dictionary<string, CharacterEntry> characters;
    readonly Dictionary<string, Dictionary<string, ClipData>> cache = new();

    public ClipLibrary(string animationsDir)
    {
        dir = animationsDir;
        var index = $"{dir}/characters/index.json";
        characters = new();
        if (!Godot.FileAccess.FileExists(index)) return;
        // _ 開頭的是說明
        foreach (var (id, entry) in JsonNode.Parse(ContentLoader.ReadText(index))!.AsObject())
            if (!id.StartsWith('_') && entry != null) characters[id] = entry.Deserialize<CharacterEntry>(Options)!;
    }

    /// <summary>有沒有專屬動作</summary>
    public bool HasCharacterClips(string id) => characters.ContainsKey(id);

    /// <summary>這個角色（與型態）要用的動作組</summary>
    public Dictionary<string, ClipData> For(RigData rig)
    {
        var body = this.GetValueOrDefault(rig.Body) ?? this["humanoid"];
        var id = rig.AnimSource ?? rig.Id;
        if (!characters.TryGetValue(id, out var entry)) return body;
        var key = $"{id}|{rig.FormName}|{rig.Body}";
        if (cache.TryGetValue(key, out var hit)) return hit;
        var set = new Dictionary<string, ClipData>(body);
        try
        {
            Apply(set, entry.Clips, $"{dir}/characters/{id}");
            if (rig.FormName != null && entry.Forms.TryGetValue(rig.FormName, out var formClips))
                Apply(set, formClips, $"{dir}/characters/{id}/forms/{rig.FormName}");
        }
        catch (System.Exception e)
        {
            // 檔案壞掉不要讓整個桌寵起不來：用體型的動作，提醒一下（tools/validate.py 會抓到）
            Godot.GD.PushWarning($"角色 {id} 的專屬動作讀取失敗，改用共用動作：{e.Message}");
            set = body;
        }
        cache[key] = set;
        return set;
    }

    void Apply(Dictionary<string, ClipData> set, List<string> names, string folder)
    {
        foreach (var name in names)
        {
            var node = JsonNode.Parse(ContentLoader.ReadText($"{folder}/{name}.json"))!.AsObject();
            var clip = node["extends"]?.GetValue<string>() is { } baseName
                ? Extend(set.GetValueOrDefault(baseName) ?? throw new InvalidDataException($"{folder}/{name}.json：extends 的 {baseName} 不存在"), node)
                : node.Deserialize<ClipData>(Options)!;
            clip.Name = name;
            set[name] = clip;
        }
    }

    /// <summary>差異合併：以 baseClip 為底，覆蓋 node 列出的欄位；tracks 只覆蓋列出的軌道（null = 拿掉那條軌道）</summary>
    public static ClipData Extend(ClipData baseClip, JsonObject node)
    {
        var merged = JsonSerializer.SerializeToNode(baseClip, Options)!.AsObject();
        foreach (var (k, v) in node)
        {
            if (k == "extends") continue;
            if (k == "tracks" && v is JsonObject tracks)
            {
                var into = merged["tracks"]!.AsObject();
                foreach (var (ch, keys) in tracks)
                {
                    if (keys == null) into.Remove(ch);
                    else into[ch] = keys.DeepClone();
                }
            }
            else merged[k] = v?.DeepClone();
        }
        return merged.Deserialize<ClipData>(Options)!;
    }
}
