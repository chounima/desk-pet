// 音效播放：一組播放器輪流使用，可以同時發出好幾個聲音；音高稍微隨機，聽起來比較自然
using System.Collections.Generic;
using Godot;

namespace DeskPet.Audio;

public partial class Sfx : Node
{
    const int Voices = 8;
    const float VolumeDb = -8;

    /// <summary>台詞 key（data/lines/）→ 一起播放的音效與音高</summary>
    public static readonly Dictionary<string, (string sound, float pitch)> LineSounds = new()
    {
        ["greet"] = ("pop", 1), ["wake"] = ("pop", 0.9f), ["enter"] = ("pop", 1.2f),
        ["pat"] = ("pop", 1.3f), ["patMany"] = ("pop", 1.45f), ["poke"] = ("pop", 0.8f), ["highFive"] = ("pop", 1.1f),
        ["annoyed"] = ("bonk", 1), ["tail"] = ("bonk", 0.9f), ["oops"] = ("bonk", 1.15f),
        ["startle"] = ("boing", 1), ["flee"] = ("boing", 1.2f), ["climb"] = ("boing", 1), ["hopDown"] = ("boing", 0.9f),
        ["dragged"] = ("squeak", 1), ["land"] = ("thud", 1), ["windowGone"] = ("fall", 1), ["save"] = ("chime", 1),
    };

    public void PlayForLine(string key)
    {
        if (LineSounds.TryGetValue(key, out var s)) Play(s.sound, s.pitch);
    }

    readonly List<AudioStreamPlayer> players = new();
    Dictionary<string, AudioStreamWav> sounds = new();
    int next;

    public bool Enabled { get; set; } = true;

    /// <summary>除錯：各音效播放次數</summary>
    public Dictionary<string, int> Played { get; } = new();

    public override void _Ready()
    {
        sounds = Synth.BuildAll();
        for (var i = 0; i < Voices; i++)
        {
            var p = new AudioStreamPlayer { VolumeDb = VolumeDb };
            AddChild(p);
            players.Add(p);
        }
    }

    public void Play(string name, float pitch = 1)
    {
        if (!Enabled || !sounds.TryGetValue(name, out var stream)) return;
        var p = players[next];
        next = (next + 1) % players.Count;
        p.Stream = stream;
        p.PitchScale = pitch * (0.95f + GD.Randf() * 0.1f);
        p.Play();
        Played[name] = Played.GetValueOrDefault(name) + 1;
    }
}
