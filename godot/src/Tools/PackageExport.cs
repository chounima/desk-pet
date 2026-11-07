// --export-package=角色[@型態] --out=資料夾 [--export-scale=4]：把佔位角色（SVG）輸出成部件包（PNG＋character.json）
//
// 當作「假的 AI 產出」走完整條美術流程（匯入 → 檢查 → 綁骨 → 播放），之後換成精細圖只要替換這些 PNG。
// 圖片解析度 = 部件範圍（Q 版單位）× export-scale；範圍和佔位角色一樣，所以換上去位置完全對得上。
// 角色有其他視角（正面、背面…）時，也輸出到 views/<視角>/（同樣的格式），換那個視角的圖就改那裡。
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using DeskPet.Content;
using Godot;

namespace DeskPet.Tools;

public static class PackageExport
{
    public static int Run(string spec, string outDir, float scale)
    {
        var parts = spec.Split('@');
        var (_, rigs) = ContentLoader.LoadCharacters();
        if (!rigs.TryGetValue(parts[0], out var baseRig))
        {
            GD.Print($"[export] 找不到角色 {parts[0]}");
            return 1;
        }
        var form = parts.Length > 1 ? parts[1] : null;
        var rig = ContentLoader.FormRig(baseRig, form);
        outDir = ProjectSettings.GlobalizePath(outDir);
        Export(rig, outDir, scale, parts[0], form, null);
        foreach (var (name, view) in rig.Views ?? new())
            if (view.Rig != "") Export(view.Data, Path.Combine(outDir, "views", name), scale, parts[0], form, name);
        return 0;
    }

    static void Export(RigData rig, string outDir, float scale, string baseId, string? form, string? view)
    {
        Directory.CreateDirectory(Path.Combine(outDir, "parts"));

        var partsJson = new JsonObject();
        foreach (var slot in rig.Slots.Where(s => s.File != null && s.Variants == null))
        {
            var name = Path.GetFileNameWithoutExtension(slot.File!);
            if (partsJson.ContainsKey(name)) continue;          // 近側、遠側共用同一張圖
            var part = new JsonObject { ["image"] = $"parts/{name}.png", ["box"] = Box(slot.Box) };
            Save(rig.Resolve(slot.File!), Path.Combine(outDir, "parts", $"{name}.png"), scale);
            if (slot.Alt is { Count: > 0 } alts)
            {
                var altJson = new JsonObject();
                foreach (var (alt, a) in alts)
                {
                    Save(rig.Resolve(a.File), Path.Combine(outDir, "parts", $"{name}@{alt}.png"), scale);
                    altJson[alt] = new JsonObject { ["image"] = $"parts/{name}@{alt}.png", ["box"] = Box(a.Box) };
                }
                part["alt"] = altJson;
            }
            partsJson[name] = part;
        }

        var doc = new JsonObject
        {
            ["version"] = 1,
            ["name"] = rig.Name,
            ["description"] = rig.Description,
            ["base"] = baseId,
            ["parts"] = partsJson,
        };
        // 表情（背面等沒有臉的視角就沒有）
        var faces = new JsonObject();
        if (rig.Slots.FirstOrDefault(s => s.Variants != null) is { } faceSlot)
        {
            Directory.CreateDirectory(Path.Combine(outDir, "faces"));
            foreach (var (expr, file) in faceSlot.Variants!)
            {
                Save(rig.Resolve(file), Path.Combine(outDir, "faces", $"{expr}.png"), scale);
                faces[expr] = $"faces/{expr}.png";
            }
            doc["faces"] = new JsonObject { ["box"] = Box(faceSlot.Box), ["images"] = faces };
        }
        doc["source"] = new JsonObject { ["method"] = "generated", ["tools"] = new JsonArray("tools/artgen（佔位角色）", "--export-package") };
        if (form != null) doc["form"] = form;
        File.WriteAllText(Path.Combine(outDir, "character.json"),
            doc.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
        GD.Print($"[export] {baseId}{(view != null ? $" 視角 {view}" : "")} → {outDir}（{partsJson.Count} 個部件、{faces.Count} 個表情，×{scale}）");
    }

    static JsonArray Box(float[] b) => new(b.Select(v => (JsonNode)v).ToArray());

    static void Save(string svgPath, string pngPath, float scale)
    {
        var image = new Image();
        var err = image.LoadSvgFromString(ContentLoader.ReadText(svgPath), scale);
        if (err != Error.Ok) GD.PushError($"SVG 轉換失敗 {svgPath}: {err}");
        image.SavePng(pngPath);
    }
}
