// 表情、眨眼、視線、講話動嘴的參數（data/face.json，網頁 config.js 的 FACE 讀同一份）。說明見 docs/CUSTOMIZE.md
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DeskPet.Content;

public sealed class FaceConfig
{
    public BlinkConfig Blink { get; set; } = new();
    public GazeConfig Gaze { get; set; } = new();
    public TalkConfig Talk { get; set; } = new();
    /// <summary>{ "*" 或角色 id: { 表情: { 圖層: 圖名, alpha: { 圖層: 不透明度 } } } }（底線開頭的 key 不用）</summary>
    public JsonObject? Expressions { get; set; }

    public sealed class BlinkConfig
    {
        public float IntervalMin { get; set; } = 2.5f;
        public float IntervalMax { get; set; } = 5.5f;
        public float Duration { get; set; } = 0.12f;
        public List<string> WholeFaceExpressions { get; set; } = new() { "idle", "focus" };
    }

    public sealed class GazeConfig
    {
        public float HeadFollow { get; set; } = 6;
        public float EyeFollow { get; set; } = 12;
        public float BodyFollow { get; set; } = 2;
        public float HeadRatio { get; set; } = 0.4f;
        public float HeadMin { get; set; } = -12;
        public float HeadMax { get; set; } = 15;
        public float BodyRatio { get; set; } = 0.3f;
        public float FaceShiftX { get; set; } = 3;
        public float FaceShiftY { get; set; } = 2.5f;
        public float Range { get; set; } = 900;
        public float LookDistance { get; set; } = 300;
        public float StillLimit { get; set; } = 8;
        public float GlanceMin { get; set; } = 6;
        public float GlanceMax { get; set; } = 14;
        public float GlanceTimeMin { get; set; } = 0.5f;
        public float GlanceTimeMax { get; set; } = 1;
        public HashSet<string> LookActions { get; set; } = new() { "idle", "sit", "sitEdge", "think", "wave", "peace", "cheer" };
    }

    public sealed class TalkConfig
    {
        public float OpenMin { get; set; } = 0.1f;
        public float OpenMax { get; set; } = 0.22f;
        public float ClosedMin { get; set; } = 0.08f;
        public float ClosedMax { get; set; } = 0.18f;
        public float HalfChance { get; set; } = 0.4f;
        public Dictionary<string, string> WholeFace { get; set; } = new()
        {
            ["closed"] = "happy", ["half"] = "surprised", ["open"] = "surprised",
            ["a"] = "surprised", ["i"] = "surprised", ["u"] = "surprised", ["e"] = "surprised", ["o"] = "surprised",
        };
    }

    static FaceConfig? cached;

    public static FaceConfig Load() => cached ??=
        Godot.FileAccess.FileExists($"{ContentLoader.Root}/data/face.json")
            ? JsonSerializer.Deserialize<FaceConfig>(ContentLoader.ReadText($"{ContentLoader.Root}/data/face.json"),
                  new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new()
            : new();

    /// <summary>把表情調整套到分層臉上（"*" 先、角色 id 後）；新的表情名從 idle 複製一份再改。和網頁 loader.applyFaceOverrides 一樣</summary>
    public void Apply(RigData rig, string id)
    {
        if (rig.Face is not { } face || Expressions == null) return;
        foreach (var scope in new[] { "*", id })
        {
            if (Expressions[scope] is not JsonObject exprs) continue;
            foreach (var (expr, node) in exprs)
            {
                if (expr.StartsWith('_') || node is not JsonObject spec) continue;
                if (!face.Expressions.TryGetValue(expr, out var layers))
                    face.Expressions[expr] = layers = new Dictionary<string, string>(face.Expressions.GetValueOrDefault("idle") ?? new());
                foreach (var (layer, value) in spec)
                {
                    if (layer == "alpha" && value is JsonObject alpha)
                    {
                        face.Alpha ??= new();
                        if (!face.Alpha.TryGetValue(expr, out var a))
                            face.Alpha[expr] = a = new Dictionary<string, float>(face.Alpha.GetValueOrDefault("idle") ?? new());
                        foreach (var (l, v) in alpha) a[l] = v!.GetValue<float>();
                    }
                    else if (value?.GetValue<string>() is { Length: > 0 } art) layers[layer] = art;
                    else layers.Remove(layer);
                }
                // 新表情沒寫 alpha：沿用 idle 的
                if (face.Alpha != null && !face.Alpha.ContainsKey(expr) && face.Alpha.TryGetValue("idle", out var idleAlpha))
                    face.Alpha[expr] = new Dictionary<string, float>(idleAlpha);
            }
        }
    }
}
