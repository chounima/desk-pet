// 疊加層設定（data/motion.json）：呼吸、重心晃動（計算在 Core/Overlay.cs）
using System.Collections.Generic;
using System.Text.Json;

namespace DeskPet.Content;

public sealed class MotionConfig
{
    public float Fade { get; set; } = 0.5f;
    public Dictionary<string, MotionLayer> Layers { get; set; } = new();

    public sealed class MotionLayer
    {
        public List<string> Actions { get; set; } = new();
        public float Period { get; set; } = 3;
        public Dictionary<string, Wave> Tracks { get; set; } = new();
    }

    public sealed class Wave
    {
        public float Amp { get; set; }
        /// <summary>相位（弧度）</summary>
        public float Phase { get; set; }
    }

    static MotionConfig? cached;

    /// <summary>讀一次就快取（所有角色共用）</summary>
    public static MotionConfig Load() => cached ??=
        Godot.FileAccess.FileExists($"{ContentLoader.Root}/data/motion.json")
            ? JsonSerializer.Deserialize<MotionConfig>(ContentLoader.ReadText($"{ContentLoader.Root}/data/motion.json"),
                  new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new()
            : new();
}
