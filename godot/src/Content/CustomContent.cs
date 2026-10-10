// 使用者自訂內容：文件\DeskPet\accessories（配件）與 outfits（衣服），由網頁預覽的編輯器（preview/editor.html）產生
//
// 配件  accessories\<名稱>\accessory.json  { "name", "slot": head|face|neck|back, "image": "hat.png", "box": [x0,y0,x1,y1] }
// 衣服  outfits\<名稱>\outfit.json        { "name", "character": "chouma", "parts": { "torso": { "image", "box" }, … } }
// 角色  characters\<名稱>\character.json  { "name", "description", "base": "tk01", "parts": {…同衣服},
//                                          "faces": { "box", "images": { "idle": "idle.png", … } }, "lines": { "greet": […] } }
// box 是圖片在錨點（配件）或部件（衣服、角色）座標裡的範圍，Q 版單位。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

namespace DeskPet.Content;

public sealed class OutfitData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Character { get; set; } = "";
    /// <summary>{ 部件圖名（torso、arm_u…）: 圖片 }</summary>
    public Dictionary<string, OutfitPart> Parts { get; set; } = new();
}

public sealed class OutfitPart
{
    public string Image { get; set; } = "";
    public float[] Box { get; set; } = { 0, 0, 1, 1 };
    /// <summary>圖片的完整路徑（載入後設定）</summary>
    public string Path { get; set; } = "";
}

public static class CustomContent
{
    /// <summary>文件\DeskPet；測試時可用環境變數 DESKPET_CUSTOM 指到別的資料夾</summary>
    public static string Root =>
        OS.GetEnvironment("DESKPET_CUSTOM") is { Length: > 0 } forced ? forced
        : System.IO.Path.Combine(OS.GetSystemDir(OS.SystemDir.Documents), "DeskPet");

    static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    sealed class AccessoryFile
    {
        public string Name { get; set; } = "";
        public string Slot { get; set; } = "head";
        public string Image { get; set; } = "";
        public float[] Box { get; set; } = { 0, 0, 1, 1 };
    }

    sealed class SlotInfo { public string Anchor { get; set; } = ""; public float Z { get; set; } }

    /// <summary>第一次使用時建立資料夾和說明</summary>
    public static void EnsureFolders()
    {
        foreach (var sub in new[] { "accessories", "outfits", "characters" })
            Directory.CreateDirectory(System.IO.Path.Combine(Root, sub));
        var readme = System.IO.Path.Combine(Root, "自訂內容說明.txt");
        if (!File.Exists(readme))
            File.WriteAllText(readme,
                "這裡放自己做的配件和衣服，桌寵啟動時會自動讀進來。\r\n\r\n" +
                "accessories\\<名稱>\\  一個資料夾一個配件：accessory.json＋圖片（png／svg）\r\n" +
                "outfits\\<名稱>\\      一個資料夾一套衣服：outfit.json＋各部件的圖片\r\n" +
                "characters\\<名稱>\\   一個角色：character.json＋部件與表情的圖片（重新啟動後出現）\r\n\r\n" +
                "用編輯器做最方便：在專案的網頁預覽打開 preview/editor.html，\r\n" +
                "上傳圖片、拖到角色身上對好位置，按「匯出」把 json 和圖片放進上面的資料夾就好。\r\n");
    }

    /// <summary>自訂配件：id 為 custom:&lt;資料夾名&gt;</summary>
    public static Dictionary<string, (AccessoryData Data, string Name)> LoadAccessories()
    {
        var result = new Dictionary<string, (AccessoryData, string)>();
        var dir = System.IO.Path.Combine(Root, "accessories");
        if (!Directory.Exists(dir)) return result;
        var slots = JsonSerializer.Deserialize<Dictionary<string, SlotInfo>>(
            ContentLoader.ReadText($"{ContentLoader.Root}/art/accessories/slots.json"), Options) ?? new();
        foreach (var folder in Directory.EnumerateDirectories(dir))
        {
            var json = System.IO.Path.Combine(folder, "accessory.json");
            try
            {
                if (!File.Exists(json)) continue;
                var f = JsonSerializer.Deserialize<AccessoryFile>(File.ReadAllText(json), Options)!;
                if (!slots.TryGetValue(f.Slot, out var slot)) throw new InvalidDataException($"不認識的部位 {f.Slot}");
                var image = System.IO.Path.Combine(folder, f.Image);
                if (!File.Exists(image)) throw new FileNotFoundException($"找不到圖片 {f.Image}");
                var id = $"custom:{System.IO.Path.GetFileName(folder)}";
                result[id] = (new AccessoryData { File = f.Image, Box = f.Box, Slot = f.Slot, Anchor = slot.Anchor, Z = slot.Z, Path = image.Replace('\\', '/') },
                              string.IsNullOrWhiteSpace(f.Name) ? System.IO.Path.GetFileName(folder) : f.Name);
            }
            catch (Exception e) { GD.PushWarning($"[DeskPet] 自訂配件 {folder} 讀取失敗：{e.Message}"); }
        }
        return result;
    }

    sealed class CharacterFile
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Base { get; set; } = "";
        public Dictionary<string, OutfitPart> Parts { get; set; } = new();
        public FaceSet? Faces { get; set; }
        public Dictionary<string, List<string>> Lines { get; set; } = new();
    }

    sealed class FaceSet
    {
        public float[] Box { get; set; } = { 0, 0, 1, 1 };
        public Dictionary<string, string> Images { get; set; } = new();
    }

    /// <summary>
    /// 自訂角色：以 base 角色的骨架與動作為範本（複製它的 rig），部件與表情換成自己的圖。
    /// id 為 custom_&lt;資料夾名&gt;；沒換的部件、表情沿用範本的。
    /// </summary>
    public static List<RigData> LoadCharacters(IReadOnlyDictionary<string, RigData> builtin)
    {
        var result = new List<RigData>();
        var dir = System.IO.Path.Combine(Root, "characters");
        if (!Directory.Exists(dir)) return result;
        foreach (var folder in Directory.EnumerateDirectories(dir))
        {
            var json = System.IO.Path.Combine(folder, "character.json");
            try
            {
                if (!File.Exists(json)) continue;
                var c = JsonSerializer.Deserialize<CharacterFile>(File.ReadAllText(json), Options)!;
                if (!builtin.TryGetValue(c.Base, out var template)) throw new InvalidDataException($"找不到範本角色 {c.Base}");
                // 複製範本的骨架資料（序列化再讀回來就是一份全新的物件）
                var rig = JsonSerializer.Deserialize<RigData>(JsonSerializer.Serialize(template, Options), Options)!;
                rig.BasePath = template.BasePath;
                rig.Id = $"custom_{System.IO.Path.GetFileName(folder)}";
                rig.Name = string.IsNullOrWhiteSpace(c.Name) ? System.IO.Path.GetFileName(folder) : c.Name;
                rig.Description = string.IsNullOrWhiteSpace(c.Description) ? "自訂角色" : c.Description;
                rig.Forms = null;
                foreach (var part in c.Parts.Values)
                {
                    part.Path = System.IO.Path.Combine(folder, part.Image).Replace('\\', '/');
                    if (!File.Exists(part.Path)) throw new FileNotFoundException($"找不到圖片 {part.Image}");
                }
                rig.DefaultOutfit = c.Parts;
                if (c.Faces is { Images.Count: > 0 } faces)
                {
                    var faceSlot = rig.Slots.Find(s => s.Variants != null)!;
                    var idle = faces.Images.GetValueOrDefault("idle") ?? System.Linq.Enumerable.First(faces.Images.Values);
                    // 每個表情都要有圖：沒做的用 idle
                    faceSlot.Variants = faceSlot.Variants!.Keys.ToDictionary(
                        k => k, k => System.IO.Path.Combine(folder, faces.Images.GetValueOrDefault(k) ?? idle).Replace('\\', '/'));
                    faceSlot.Box = faces.Box;
                }
                rig.CustomLines = c.Lines;
                result.Add(rig);
            }
            catch (Exception e) { GD.PushWarning($"[DeskPet] 自訂角色 {folder} 讀取失敗：{e.Message}"); }
        }
        return result;
    }

    public static List<OutfitData> LoadOutfits()
    {
        var result = new List<OutfitData>();
        var dir = System.IO.Path.Combine(Root, "outfits");
        if (!Directory.Exists(dir)) return result;
        foreach (var folder in Directory.EnumerateDirectories(dir))
        {
            var json = System.IO.Path.Combine(folder, "outfit.json");
            try
            {
                if (!File.Exists(json)) continue;
                var o = JsonSerializer.Deserialize<OutfitData>(File.ReadAllText(json), Options)!;
                o.Id = System.IO.Path.GetFileName(folder);
                if (string.IsNullOrWhiteSpace(o.Name)) o.Name = o.Id;
                foreach (var part in o.Parts.Values)
                {
                    part.Path = System.IO.Path.Combine(folder, part.Image).Replace('\\', '/');
                    if (!File.Exists(part.Path)) throw new FileNotFoundException($"找不到圖片 {part.Image}");
                }
                result.Add(o);
            }
            catch (Exception e) { GD.PushWarning($"[DeskPet] 自訂衣服 {folder} 讀取失敗：{e.Message}"); }
        }
        return result;
    }
}
