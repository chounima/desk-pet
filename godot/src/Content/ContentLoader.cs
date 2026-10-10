// 讀取角色、動作、道具資料
//
// 開發時直接讀專案根目錄（godot/ 的上一層）的 art/ 與 data/，網頁預覽和桌寵共用同一份資料；
// 匯出後則讀 res://content/（打包前由 tools/sync_content.py 複製進來）。
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            var dir = $"{Root}/art/characters/{id}/";
            var rig = ReadJson<RigData>($"{dir}rig.json");
            rig.BasePath = dir;
            rigs[id] = rig;
            foreach (var form in rig.Forms?.Values ?? Enumerable.Empty<FormData>())
            {
                if (form.Rig == "") { form.Data = rig; continue; }
                form.Data = ReadJson<RigData>($"{dir}{form.Rig}rig.json");
                form.Data.BasePath = $"{dir}{form.Rig}";
            }
        }
        return (ids, rigs);
    }

    /// <summary>角色某個型態的骨架；沒有這個型態就用預設的</summary>
    public static RigData FormRig(RigData rig, string? form) =>
        form != null && rig.Forms != null && rig.Forms.TryGetValue(form, out var f) ? f.Data : rig;

    /// <summary>預設型態的名稱（沒有型態時為 null）</summary>
    public static string? DefaultForm(RigData rig) => rig.Forms?.Keys.FirstOrDefault();

    /// <summary>
    /// 共用動作＋各體型的專屬動作：sets[體型][動作名]。
    /// 體型專屬版放在 data/animations/&lt;體型&gt;/，沒有的依 alias 借用相近的姿勢。
    /// </summary>
    public static (List<string> names, Dictionary<string, Dictionary<string, ClipData>> sets) LoadAnimations(IEnumerable<string> bodies)
    {
        var dir = $"{Root}/data/animations";
        var names = ReadJson<List<string>>($"{dir}/index.json");
        var clips = new Dictionary<string, ClipData>();
        foreach (var name in names)
        {
            var clip = ReadJson<ClipData>($"{dir}/{name}.json");
            clip.Name = name;
            clips[name] = clip;
        }
        var sets = new Dictionary<string, Dictionary<string, ClipData>> { ["humanoid"] = clips };
        foreach (var body in bodies.Distinct().Where(b => b != "humanoid"))
        {
            var index = ReadJson<BodyIndex>($"{dir}/{body}/index.json");
            var set = new Dictionary<string, ClipData>(clips);
            foreach (var name in index.Clips)
            {
                var clip = ReadJson<ClipData>($"{dir}/{body}/{name}.json");
                clip.Name = name;
                set[name] = clip;
            }
            foreach (var (name, target) in index.Alias)
                set[name] = set[target].AliasAs(name, clips.GetValueOrDefault(name));
            sets[body] = set;
        }
        return (names, sets);
    }

    public static Dictionary<string, PropData> LoadProps()
    {
        var props = ReadJson<Dictionary<string, PropData>>($"{Root}/art/props/props.json");
        foreach (var p in props.Values) p.Path = $"{Root}/art/props/{p.File}";
        return props;
    }
}
