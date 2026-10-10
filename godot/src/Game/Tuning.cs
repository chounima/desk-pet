// 調整參數（對應 preview/js/config.js）
using DeskPet.Content;
using System.Collections.Generic;

namespace DeskPet.Game;

public static class Tuning
{
    public const float Scale = 0.8f;          // 角色縮放（「中」的大小）

    /// <summary>系統匣可選的大小（乘在 Scale 上）</summary>
    public static readonly (string Label, float Value)[] Sizes = { ("size.small", 0.7f), ("size.medium", 1f), ("size.large", 1.4f) };
    public const float Gravity = 2400;        // 掉落加速度 (px/s²)
    public const float Crossfade = 0.18f;     // 動作切換的混合時間 (s)
    public const float SleepAfter = 30;       // 多久沒輸入就睡著 (s)
    public const float TypingHold = 2.5f;     // 停止打字後多久收起筆電 (s)
    public const int FocusRate = 6;           // 每秒幾下進入專注表情
    public const float Margin = 80;           // 走路時離螢幕邊緣的距離 (px)

    /// <summary>自動行為：權重越高越常出現；Time = 循環動作持續秒數範圍</summary>
    public static readonly Dictionary<string, (float Weight, float[]? Time)> Auto = new()
    {
        ["idle"] = (3, new[] { 2f, 4f }),
        ["walk"] = (5, null),
        ["run"] = (1, null),
        ["sit"] = (2, new[] { 5f, 9f }),
        ["sitEdge"] = (1, new[] { 5f, 9f }),
        ["think"] = (1, new[] { 3f, 5f }),
        ["wave"] = (1, new[] { 2f, 3f }),
        ["dance"] = (1, new[] { 3f, 5f }),
        ["stretch"] = (1, null),
        ["jump"] = (1, null),
        ["peace"] = (1, new[] { 2f, 3f }),
        ["climb"] = (2, null),          // 跳到附近的視窗上（附近沒有視窗就改成待機）
        ["hopDown"] = (2, null),        // 從視窗上跳下來（站在地面時改成待機）
        ["volleyball"] = (1, new[] { 4f, 7f }),
        ["coffee"] = (1, new[] { 5f, 8f }),        // 商店商品，買了才會出現（Behavior.Allowed）
        ["read"] = (1, new[] { 6f, 10f }),
    };
}
