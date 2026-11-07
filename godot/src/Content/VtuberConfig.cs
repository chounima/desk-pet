// 半身模式設定（data/vtuber.json）：取景、嘴型門檻、聲音觸發規則、快捷鍵
using System.Collections.Generic;
using System.Text.Json;

namespace DeskPet.Content;

public sealed class VtuberConfig
{
    public BustConfig Bust { get; set; } = new();
    public MouthConfig Mouth { get; set; } = new();
    public List<VoiceRule> Rules { get; set; } = new();
    /// <summary>{ "Ctrl+Alt+1": 反應 }</summary>
    public Dictionary<string, VoiceReaction> Hotkeys { get; set; } = new();

    public sealed class BustConfig
    {
        /// <summary>預設高度（px，再乘 UI 縮放）</summary>
        public float Height { get; set; } = 420;
        /// <summary>頭頂上面留多少（取景高度的比例）</summary>
        public float Margin { get; set; } = 0.1f;
    }

    public sealed class MouthConfig
    {
        /// <summary>比平常講話小不到這麼多 dB 算「全開」，更小聲算「半開」（沒認出母音時）</summary>
        public double OpenDb { get; set; } = 6;
    }

    static VtuberConfig? cached;

    public static VtuberConfig Load() => cached ??=
        Godot.FileAccess.FileExists($"{ContentLoader.Root}/data/vtuber.json")
            ? JsonSerializer.Deserialize<VtuberConfig>(ContentLoader.ReadText($"{ContentLoader.Root}/data/vtuber.json"),
                  new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new()
            : new();
}

/// <summary>觸發後做什麼：表情、動作、持續幾秒</summary>
public class VoiceReaction
{
    public string? Face { get; set; }
    public string? Action { get; set; }
    public float Seconds { get; set; } = 1.5f;
}

public sealed class VoiceRule : VoiceReaction
{
    public string Id { get; set; } = "";
    public VoiceCondition When { get; set; } = new();
    public float Cooldown { get; set; } = 3;
    public int Priority { get; set; } = Game.Control.Priority.Reaction;
}

public sealed class VoiceCondition
{
    /// <summary>音量比前 1 秒的平均突然大這麼多 dB</summary>
    public double? JumpDb { get; set; }
    /// <summary>比平常講話大這麼多 dB</summary>
    public double? LoudDb { get; set; }
    /// <summary>比平常高（正）或低（負）幾個半音以上</summary>
    public double? Semitones { get; set; }
    /// <summary>條件要持續幾秒</summary>
    public float ForSeconds { get; set; }
    /// <summary>安靜幾秒</summary>
    public float? SilentSeconds { get; set; }
}
