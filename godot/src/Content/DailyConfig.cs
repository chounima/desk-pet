// data/daily.json（日常體驗參數）與 data/menu.json（右鍵選單的動作清單）
using System.Collections.Generic;
using System.Text.Json;

namespace DeskPet.Content;

public sealed class DailyConfig
{
    public List<Greeting> Greetings { get; set; } = new();
    public HourRange LateNight { get; set; } = new();
    public SitTooLongConfig SitTooLong { get; set; } = new();
    public float FullscreenCheckSeconds { get; set; } = 1;
    public float DisplayCheckSeconds { get; set; } = 2;
    public FitScreenConfig FitScreen { get; set; } = new();
    public float HideMinutes { get; set; } = 10;

    public sealed class Greeting
    {
        public string Key { get; set; } = "";
        public int From { get; set; }
        public int To { get; set; }
    }

    public sealed class HourRange
    {
        public int From { get; set; }
        public int To { get; set; } = 5;
        public float EveryMinutes { get; set; } = 30;
    }

    public sealed class SitTooLongConfig
    {
        public float ActiveMinutes { get; set; } = 50;
        public float BreakMinutes { get; set; } = 5;
    }

    public sealed class FitScreenConfig
    {
        public float ScreenRatio { get; set; } = 0.22f;
        public float BaseHeight { get; set; } = 270;
    }

    /// <summary>小時是否落在 [from, to)；from &gt; to 表示跨過午夜</summary>
    public static bool InHours(int hour, int from, int to) =>
        from <= to ? hour >= from && hour < to : hour >= from || hour < to;

    static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static DailyConfig Load() =>
        JsonSerializer.Deserialize<DailyConfig>(ContentLoader.ReadText($"{ContentLoader.Root}/data/daily.json"), Options) ?? new();
}

public sealed class MenuConfig
{
    public List<string> Actions { get; set; } = new();

    public static MenuConfig Load() =>
        JsonSerializer.Deserialize<MenuConfig>(ContentLoader.ReadText($"{ContentLoader.Root}/data/menu.json"),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
}
