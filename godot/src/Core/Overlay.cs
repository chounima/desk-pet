// 疊加層：在動作的姿勢上加呼吸、重心晃動等週期性小動作（設定 data/motion.json；網頁版 preview/js/core/overlay.js 算法一樣）
//
// 每一層：value = amp × sin(2π × time / period + phase)，只在 actions 列出的動作套用，換動作時用 fade 秒淡入淡出。
// 純計算：時間由呼叫端累加（共用測試 tests/golden/overlay.json）。
using System;
using System.Collections.Generic;
using DeskPet.Content;

namespace DeskPet.Core;

public sealed class Overlay
{
    readonly MotionConfig config;
    readonly Dictionary<string, float> weights = new();
    double time;

    public Overlay(MotionConfig config) => this.config = config;

    /// <summary>前進 dt 秒，把疊加層加到 pose 上（目前動作是 action）</summary>
    public void Apply(Dictionary<string, float> pose, string action, float dt)
    {
        time += dt;
        foreach (var (name, layer) in config.Layers)
        {
            // 權重往 1（這個動作要套用）或 0 靠近
            var target = layer.Actions.Contains(action) ? 1f : 0f;
            var w = weights.TryGetValue(name, out var cur) ? cur : target;
            var rate = config.Fade > 0 ? dt / config.Fade : 1;
            w = w < target ? Math.Min(target, w + rate) : Math.Max(target, w - rate);
            weights[name] = w;
            if (w <= 0) continue;
            foreach (var (ch, wave) in layer.Tracks)
            {
                var delta = w * wave.Amp * (float)Math.Sin(2 * Math.PI * time / layer.Period + wave.Phase);
                pose[ch] = (pose.TryGetValue(ch, out var v) ? v : Animator.Rest(ch)) + delta;
            }
        }
    }

    /// <summary>測試用：某個時間點（權重 1）每條軌道加多少</summary>
    public static Dictionary<string, float> Deltas(MotionConfig config, string action, double time)
    {
        var result = new Dictionary<string, float>();
        foreach (var layer in config.Layers.Values)
        {
            if (!layer.Actions.Contains(action)) continue;
            foreach (var (ch, wave) in layer.Tracks)
                result[ch] = result.GetValueOrDefault(ch) + wave.Amp * (float)Math.Sin(2 * Math.PI * time / layer.Period + wave.Phase);
        }
        return result;
    }
}
