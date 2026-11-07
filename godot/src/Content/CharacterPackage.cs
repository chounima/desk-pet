// 角色部件包（character.json，格式見 schemas/character.schema.json）：正式角色、自訂角色用同一種格式、同一條讀取路徑
//
// 以 base 角色（可指定 form 型態）的骨架、比例、動作、錨點為範本，部件與表情換成自己的圖；
// 沒換的部件、表情沿用範本的。錨點、道具位置、頭頂可以覆寫。
// 還用不到的欄位（分層臉、頭髮分節、正面半身…）保留在 Extra，之後的階段再接上。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeskPet.Content;

public static class CharacterPackage
{
    static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public sealed class FileData
    {
        public int Version { get; set; } = 1;
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Base { get; set; } = "";
        public string? Form { get; set; }
        public Dictionary<string, PackagePart> Parts { get; set; } = new();
        public FaceSet? Faces { get; set; }
        public Dictionary<string, AnchorData>? Anchors { get; set; }
        public Dictionary<string, PropOverride>? PropOverrides { get; set; }
        public float[]? HeadTop { get; set; }
        public Dictionary<string, List<string>> Lines { get; set; } = new();
        [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    /// <summary>部件：圖片＋範圍，另外可以有替換圖（例如手的 peace）</summary>
    public sealed class PackagePart
    {
        public string Image { get; set; } = "";
        public float[] Box { get; set; } = { 0, 0, 1, 1 };
        public Dictionary<string, AltPart>? Alt { get; set; }
    }

    public sealed class AltPart
    {
        public string Image { get; set; } = "";
        public float[] Box { get; set; } = { 0, 0, 1, 1 };
    }

    public sealed class FaceSet
    {
        public float[] Box { get; set; } = { 0, 0, 1, 1 };
        public Dictionary<string, string> Images { get; set; } = new();
    }

    /// <summary>資料夾裡有沒有部件包</summary>
    public static bool Exists(string folder) => FileExists(Join(folder, "character.json"));

    /// <param name="folder">部件包資料夾（res://…、開發時的專案路徑、或文件\DeskPet\characters\…）</param>
    /// <param name="id">角色 id（正式角色 = 資料夾名；自訂角色 = custom_資料夾名）</param>
    /// <param name="rigs">已經讀好的角色（找 base 範本）</param>
    public static RigData Load(string folder, string id, IReadOnlyDictionary<string, RigData> rigs)
    {
        var c = JsonSerializer.Deserialize<FileData>(ReadText(Join(folder, "character.json")), Options)
                ?? throw new InvalidDataException("character.json 是空的");
        if (string.IsNullOrEmpty(c.Base)) c.Base = id;        // 沒寫 base = 換掉同名角色的美術
        if (!rigs.TryGetValue(c.Base, out var baseRig)) throw new InvalidDataException($"找不到範本角色 {c.Base}");
        var template = ContentLoader.FormRig(baseRig, c.Form);
        // 複製範本的骨架資料（序列化再讀回來就是一份全新的物件）
        var rig = JsonSerializer.Deserialize<RigData>(JsonSerializer.Serialize(template, Options), Options)!;
        rig.BasePath = template.BasePath;
        rig.Id = id;
        rig.AnimSource = template.AnimSource ?? template.Id;     // 動作沿用範本角色的（含專屬動作）
        rig.FormName = template.FormName;
        rig.Version = Math.Max(c.Version, rig.Version);
        rig.Name = string.IsNullOrWhiteSpace(c.Name) ? Path.GetFileName(folder.TrimEnd('/', '\\')) : c.Name;
        rig.Description = c.Description;
        // 換掉自己的美術：其他型態照舊（預設型態指向新的資料）；新角色只用範本的一個型態
        if (c.Base == id && template == baseRig && baseRig.Forms != null)
            rig.Forms = baseRig.Forms.ToDictionary(kv => kv.Key, kv => new FormData { Label = kv.Value.Label, Rig = kv.Value.Rig, Data = kv.Value.Rig == "" ? rig : kv.Value.Data });
        else rig.Forms = null;
        rig.DefaultOutfit = new Dictionary<string, OutfitPart>();
        foreach (var (name, part) in c.Parts)
        {
            var path = Join(folder, part.Image);
            if (!FileExists(path)) throw new FileNotFoundException($"找不到部件 {name} 的圖 {part.Image}");
            rig.DefaultOutfit[name] = new OutfitPart { Image = part.Image, Box = part.Box, Path = path };
            // 替換圖：用這張部件圖的插槽（近側、遠側）都換
            foreach (var (alt, a) in part.Alt ?? new())
                foreach (var slot in rig.Slots.Where(s => Path.GetFileNameWithoutExtension(s.File ?? "") == name))
                {
                    slot.Alt ??= new();
                    slot.Alt[alt] = new AltData { File = Join(folder, a.Image), Box = a.Box };
                }
        }
        if (c.Faces is { Images.Count: > 0 } faces)
        {
            var faceSlot = rig.Slots.Find(s => s.Variants != null) ?? throw new InvalidDataException($"範本角色 {c.Base} 沒有表情插槽");
            var idle = faces.Images.GetValueOrDefault("idle") ?? faces.Images.Values.First();
            // 每個表情都要有圖：沒做的用 idle
            faceSlot.Variants = faceSlot.Variants!.Keys.ToDictionary(k => k, k => Join(folder, faces.Images.GetValueOrDefault(k) ?? idle));
            faceSlot.Box = faces.Box;
            rig.Face = null;        // 部件包畫了整張臉：不用範本的分層臉
        }
        foreach (var (name, a) in c.Anchors ?? new()) rig.Anchors[name] = a;
        foreach (var (name, o) in c.PropOverrides ?? new()) rig.PropOverrides[name] = o;
        if (c.HeadTop is { Length: 2 } top) rig.HeadTop = top;
        rig.CustomLines = c.Lines.Count > 0 ? c.Lines : null;
        if (c.Extra != null)
        {
            rig.Extra ??= new();
            foreach (var (k, v) in c.Extra) rig.Extra[k] = v;
        }
        return rig;
    }

    // res:// 用 Godot 讀（匯出後在打包檔裡），其他路徑用一般檔案系統
    static string Join(string folder, string file) =>
        Path.IsPathRooted(file) || file.Contains("://") ? file : folder.TrimEnd('/', '\\') + "/" + file.Replace('\\', '/');

    static bool FileExists(string path) => path.StartsWith("res://") ? Godot.FileAccess.FileExists(path) : File.Exists(path);

    static string ReadText(string path) => path.StartsWith("res://") ? ContentLoader.ReadText(path) : File.ReadAllText(path);
}
