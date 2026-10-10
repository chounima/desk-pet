// 讀取角色、動作、道具資料
//
// 開發時直接讀專案根目錄（godot/ 的上一層）的 art/ 與 data/，網頁預覽和桌寵共用同一份資料；
// 匯出後則讀 res://content/（打包前由 tools/sync_content.py 複製進來）。
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;

namespace DeskPet.Content;

public static class ContentLoader
{
    static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static string Root { get; } = ResolveRoot();

    static string ResolveRoot()
    {
        if (OS.HasFeature("template")) return "res://content";     // 匯出後的版本
        var forced = OS.GetEnvironment("DESKPET_CONTENT");          // 測試用：開發版也強制讀 res://content
        if (!string.IsNullOrEmpty(forced)) return forced;
        var project = ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\');
        var parent = Path.GetDirectoryName(project);
        if (parent != null && Directory.Exists(Path.Combine(parent, "art")) && Directory.Exists(Path.Combine(parent, "data")))
            return parent.Replace('\\', '/');
        return "res://content";
    }

    public static string ReadText(string path)
    {
        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read)
            ?? throw new FileNotFoundException($"讀不到 {path}：{Godot.FileAccess.GetOpenError()}");
        return file.GetAsText();
    }

    static T ReadJson<T>(string path) =>
        JsonSerializer.Deserialize<T>(ReadText(path), Options) ?? throw new InvalidDataException(path);

    public static (List<string> ids, Dictionary<string, RigData> rigs) LoadCharacters()
    {
        var ids = ReadJson<List<string>>($"{Root}/art/characters/index.json");
        var rigs = new Dictionary<string, RigData>();
        foreach (var id in ids)
        {
            var rig = ReadJson<RigData>($"{Root}/art/characters/{id}/rig.json");
            rig.BasePath = $"{Root}/art/characters/{id}/";
            rigs[id] = rig;
        }
        return (ids, rigs);
    }

    public static (List<string> names, Dictionary<string, ClipData> clips) LoadAnimations()
    {
        var names = ReadJson<List<string>>($"{Root}/data/animations/index.json");
        var clips = new Dictionary<string, ClipData>();
        foreach (var name in names)
        {
            var clip = ReadJson<ClipData>($"{Root}/data/animations/{name}.json");
            clip.Name = name;
            clips[name] = clip;
        }
        return (names, clips);
    }

    public static Dictionary<string, PropData> LoadProps()
    {
        var props = ReadJson<Dictionary<string, PropData>>($"{Root}/art/props/props.json");
        foreach (var p in props.Values) p.Path = $"{Root}/art/props/{p.File}";
        return props;
    }
}
