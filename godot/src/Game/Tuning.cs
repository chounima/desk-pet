// 調整參數：讀 data/tuning.json 的 pet、auto（網頁預覽 preview/js/config.js 讀同一份）
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using DeskPet.Content;

namespace DeskPet.Game;

public static class Tuning
{
    /// <summary>data/tuning.json 整份（VolleyConfig 也從這裡讀）</summary>
    internal static readonly JsonNode Data = JsonNode.Parse(ContentLoader.ReadText($"{ContentLoader.Root}/data/tuning.json"))!;
    static readonly JsonNode Pet = Data["pet"]!;

    public static readonly float Scale = F(Pet, "scale");               // 角色縮放（「中」的大小）

    /// <summary>系統匣可選的大小（乘在 Scale 上）</summary>
    public static readonly (string Label, float Value)[] Sizes =
        Pet["sizes"]!.AsArray().Select(s => (s!["label"]!.GetValue<string>(), F(s, "value"))).ToArray();
    public static readonly float Gravity = F(Pet, "gravity");           // 掉落加速度 (px/s²)
    public static readonly float Crossfade = F(Pet, "crossfade");       // 動作切換的混合時間 (s)
    public static readonly float SleepAfter = F(Pet, "sleepAfter");     // 多久沒輸入就睡著 (s)
    public static readonly float TypingHold = F(Pet, "typingHold");     // 停止打字後多久收起筆電 (s)
    public static readonly int FocusRate = (int)F(Pet, "focusRate");    // 每秒幾下進入專注表情
    public static readonly float Margin = F(Pet, "margin");             // 走路時離螢幕邊緣的距離 (px)

    /// <summary>自動行為：權重越高越常出現；Time = 循環動作持續秒數範圍</summary>
    public static readonly Dictionary<string, (float Weight, float[]? Time)> Auto =
        Data["auto"]!.AsObject().ToDictionary(kv => kv.Key, kv => (F(kv.Value!, "weight"), Floats(kv.Value!["time"])));

    internal static float F(JsonNode node, string key) => node[key]!.GetValue<float>();

    internal static float[]? Floats(JsonNode? node) => node?.AsArray().Select(v => v!.GetValue<float>()).ToArray();
}
