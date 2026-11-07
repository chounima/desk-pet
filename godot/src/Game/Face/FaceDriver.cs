// 分層臉的表情驅動：決定每個圖層顯示哪張圖、不透明度、位移（和 preview/js/game/faceDriver.js 同一套規則，tests/golden/face.json 比對）
//   expression：表情名（沒有的用 idle）
//   blink：這一刻在眨眼（只有睜眼的表情會閉眼；睜眼 = 眼睛圖有跟隨圖層的同名圖，例如瞳孔）
//   mouth：嘴巴管道的值（closed、half、open、a、i、u、e、o），透過 face.talk 換成嘴型圖；closed 或 null = 用表情的嘴
//   eyes：眼睛管道的值、或動作的 eyes 軌道（直接指定眼睛圖）
//   mouthArt：動作的 mouth 軌道（直接指定嘴巴圖；講話時被 mouth 蓋過）
//   look：視線 -1～1（右、下為正），瞳孔依圖層的 move [左, 上, 右, 下] 位移
using System.Collections.Generic;
using System.Linq;
using DeskPet.Content;

namespace DeskPet.Game.Face;

public readonly record struct FaceInput(string Expression, bool Blink = false, string? Mouth = null, string? Eyes = null, float LookX = 0, float LookY = 0, string? MouthArt = null);

/// <summary>一個圖層這一刻的樣子；File = null 表示不顯示</summary>
public readonly record struct FaceLayerState(string Name, string? File, float Alpha, float Dx, float Dy);

public static class FaceDriver
{
    public static List<FaceLayerState> Layers(FaceRig face, FaceInput input)
    {
        var exprName = face.Expressions.ContainsKey(input.Expression) ? input.Expression : "idle";
        var chosen = new Dictionary<string, string>(face.Expressions[exprName]);
        var alpha = face.Alpha?.GetValueOrDefault(exprName);
        var eyesLayer = face.Layers.FirstOrDefault(l => l.Name == "eyes");

        if (input.Eyes != null && eyesLayer?.Variants.ContainsKey(input.Eyes) == true) chosen["eyes"] = input.Eyes;
        if (input.Blink && eyesLayer?.Variants.ContainsKey("line") == true && IsOpen(face, chosen.GetValueOrDefault("eyes"))) chosen["eyes"] = "line";
        var mouthLayer = face.Layers.FirstOrDefault(l => l.Name == "mouth");
        if (input.MouthArt != null && mouthLayer?.Variants.ContainsKey(input.MouthArt) == true) chosen["mouth"] = input.MouthArt;
        if (input.Mouth is { } m && m != "closed" && face.Talk?.GetValueOrDefault(m) is { } talk) chosen["mouth"] = talk;

        var result = new List<FaceLayerState>(face.Layers.Count);
        foreach (var l in face.Layers)
        {
            var variant = chosen.GetValueOrDefault(l.Follow ?? l.Name);
            var file = variant != null ? l.Variants.GetValueOrDefault(variant) : null;
            float dx = 0, dy = 0;
            if (l.Move is { Length: 4 } move && file != null)
            {
                dx = input.LookX < 0 ? -input.LookX * move[0] : input.LookX * move[2];
                dy = input.LookY < 0 ? -input.LookY * move[1] : input.LookY * move[3];
            }
            result.Add(new FaceLayerState(l.Name, file, alpha?.GetValueOrDefault(l.Name, 1f) ?? 1f, dx, dy));
        }
        return result;
    }

    // 睜開的眼睛：有圖層跟著眼睛、而且有這個眼睛的同名圖（瞳孔、眼線）
    static bool IsOpen(FaceRig face, string? eyes) =>
        eyes != null && face.Layers.Any(l => l.Follow == "eyes" && l.Variants.ContainsKey(eyes));
}
