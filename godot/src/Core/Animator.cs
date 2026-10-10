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

    public Animator(Dictionary<string, ClipData> clips, float crossfade)
    {
        this.clips = clips;
        this.crossfade = crossfade;
    }

    static float Smooth(float t) => t * t * (3 - 2 * t);

    /// <summary>keys: [[時間, 數值], ...]，兩個關鍵影格之間平滑插值</summary>
    public static float SampleTrack(List<float[]> keys, float t)
    {
        if (keys.Count == 1 || t <= keys[0][0]) return keys[0][1];
        for (var i = 0; i < keys.Count - 1; i++)
        {
            var (t0, v0) = (keys[i][0], keys[i][1]);
            var (t1, v1) = (keys[i + 1][0], keys[i + 1][1]);
            if (t <= t1) return v0 + (v1 - v0) * Smooth((t - t0) / Math.Max(t1 - t0, 1e-6f));
        }
        return keys[^1][1];
    }

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

    public float LocalTime => Clip.Loop ? time % Clip.Duration : Math.Min(time, Clip.Duration);

    public Dictionary<string, float> Update(float dt)
    {
        time += dt;
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
