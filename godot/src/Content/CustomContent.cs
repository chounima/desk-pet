// 使用者自訂內容：文件\DeskPet\accessories（配件）與 outfits（衣服），由網頁預覽的編輯器（preview/editor.html）產生
//
// 配件  accessories\<名稱>\accessory.json  { "name", "slot": head|face|neck|back, "image": "hat.png", "box": [x0,y0,x1,y1] }
// 衣服  outfits\<名稱>\outfit.json        { "name", "character": "chouma", "parts": { "torso": { "image", "box" }, … } }
// box 是圖片在錨點（配件）或部件（衣服）座標裡的範圍，Q 版單位。
using System;
using System.Collections.Generic;
using System.IO;
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
        foreach (var sub in new[] { "accessories", "outfits" })
            Directory.CreateDirectory(System.IO.Path.Combine(Root, sub));
        var readme = System.IO.Path.Combine(Root, "自訂內容說明.txt");
        if (!File.Exists(readme))
            File.WriteAllText(readme,
                "這裡放自己做的配件和衣服，桌寵啟動時會自動讀進來。\r\n\r\n" +
                "accessories\\<名稱>\\  一個資料夾一個配件：accessory.json＋圖片（png／svg）\r\n" +
                "outfits\\<名稱>\\      一個資料夾一套衣服：outfit.json＋各部件的圖片\r\n\r\n" +
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
