// 動作播放器：取樣關鍵影格，切換動作時做交叉淡化（移植自 preview/js/core/animator.js）
using System;
using System.Collections.Generic;
using DeskPet.Content;

namespace DeskPet.Core;

public sealed class Animator
{
    static readonly Dictionary<string, float> NoMods = new();

    Dictionary<string, ClipData> clips;
    readonly float crossfade;
    Dictionary<string, float> from = new();
    Dictionary<string, float> mods = NoMods;
    float blend = 1;
    float time;

    public ClipData Clip { get; private set; } = null!;
    public string Name => Clip.Name;
    public bool Finished { get; private set; }
    public Dictionary<string, float> Pose { get; private set; } = new();

    /// <summary>目前使用的動作組（依角色體型切換）</summary>
    public Dictionary<string, ClipData> Clips
    {
        get => clips;
        set => clips = value;
    }

    /// <summary>播放速度倍率（彈吉他時依歌曲 BPM 調整）</summary>
    public float Speed { get; set; } = 1;

    /// <summary>把目前動作拉回開頭（不做淡化），讓刷弦對準拍點</summary>
    public void Rephase() => time = 0;

    public Animator(Dictionary<string, ClipData> clips, float crossfade)
    {
        this.clips = clips;
        this.crossfade = crossfade;
    }

    static float Smooth(float t) => t * t * (3 - 2 * t);

    /// <summary>keys: [[時間, 數值, 曲線], ...]，兩個關鍵影格之間依前一個的曲線插值（預設平滑）</summary>
    public static float SampleTrack(List<Keyframe> keys, float t)
    {
        if (keys.Count == 1 || t <= keys[0].T) return keys[0].V;
        for (var i = 0; i < keys.Count - 1; i++)
        {
            var (k0, k1) = (keys[i], keys[i + 1]);
            if (t <= k1.T) return k0.V + (k1.V - k0.V) * Ease(k0.Curve, (t - k0.T) / Math.Max(k1.T - k0.T, 1e-6f));
        }
        return keys[^1].V;
    }

    // back：先超過一點再回來（easeOutBack，最多超過約 10%）；hold：停在前一個值，到下一個關鍵影格才跳過去
    const float BackC1 = 1.70158f, BackC3 = BackC1 + 1;

    public static float Ease(Curve curve, float u) => curve switch
    {
        Curve.Linear => u,
        Curve.EaseIn => u * u,
        Curve.EaseOut => 1 - (1 - u) * (1 - u),
        Curve.Back => 1 + BackC3 * (u - 1) * (u - 1) * (u - 1) + BackC1 * (u - 1) * (u - 1),
        Curve.Hold => u >= 1 ? 1 : 0,
        _ => Smooth(u),
    };

    /// <param name="mods">播放時縮放某些軌道，例如 { "root.y": 0.5 }</param>
    public void Play(string name, bool restart = false, Dictionary<string, float>? mods = null)
    {
        if (!clips.TryGetValue(name, out var clip) || (Clip == clip && !restart)) return;
        from = new Dictionary<string, float>(Pose);
        blend = 0;
        Clip = clip;
        this.mods = mods ?? NoMods;
        time = 0;
        Finished = false;
    }

    /// <summary>直接跳到某個時間點（不做淡化）：錄影、截圖用</summary>
    public void Seek(float t)
    {
        time = t;
        blend = 1;
    }

    public float LocalTime => Clip.Loop ? time % Clip.Duration : Math.Min(time, Clip.Duration);

    public Dictionary<string, float> Update(float dt)
    {
        time += dt * Speed;
        if (!Clip.Loop && time >= Clip.Duration) Finished = true;

        var t = LocalTime;
        var target = new Dictionary<string, float>(Clip.Tracks.Count);
        foreach (var (ch, keys) in Clip.Tracks)
            target[ch] = SampleTrack(keys, t) * (mods.TryGetValue(ch, out var m) ? m : 1);

        blend = Math.Min(1, blend + dt / crossfade);
        var w = Smooth(blend);
        var pose = new Dictionary<string, float>(target);
        foreach (var (ch, a) in from)
        {
            target.TryGetValue(ch, out var b);
            pose[ch] = a + (b - a) * w;
        }
        foreach (var (ch, b) in target)
            if (!from.ContainsKey(ch)) pose[ch] = b * w;
        Pose = pose;
        return pose;
    }

    public string Face => Clip.FaceAt(LocalTime);
    public IReadOnlyList<string> Props => Clip.Props ?? (IReadOnlyList<string>)Array.Empty<string>();
    public IReadOnlyDictionary<string, string> Alt => Clip.Alt ?? (IReadOnlyDictionary<string, string>)new Dictionary<string, string>();
    public string? Fx => Clip.Fx;
    public float Move => Clip.Move;
}
