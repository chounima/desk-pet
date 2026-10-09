// 互動規則（data/interactions.json）：點哪裡、點幾下、在什麼情境、按什麼鍵 → 做什麼
using System.Collections.Generic;
using System.Text.Json;

namespace DeskPet.Content;

public sealed class InteractionData
{
    /// <summary>連續點擊的間隔上限（秒），超過就重新計算連擊</summary>
    public float ComboWindow { get; set; } = 1.5f;
    /// <summary>{ 部位: [規則…] }，依 Min（第幾下）挑最高的符合規則</summary>
    public Dictionary<string, List<ReactionRule>> Click { get; set; } = new();
    /// <summary>{ 情境（sleep、typing）: 規則 }，正在睡覺或打字時被點的反應</summary>
    public Dictionary<string, ReactionRule> Busy { get; set; } = new();
    /// <summary>{ 按鍵種類（Enter、Backspace、Save）: 規則 }</summary>
    public Dictionary<string, KeyRule> Keys { get; set; } = new();

    public static InteractionData Load() =>
        JsonSerializer.Deserialize<InteractionData>(
            ContentLoader.ReadText($"{ContentLoader.Root}/data/interactions.json"),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new InteractionData();
}

public sealed class ReactionRule
{
    public int Min { get; set; } = 1;          // 連擊第幾下起適用
    public string? Action { get; set; }        // 播放的動作
    public List<string>? Random { get; set; }  // 從中隨機挑一個動作（取代 Action）
    public string? Line { get; set; }          // 台詞 key（data/lines/）
    public string? Face { get; set; }          // 暫時換表情
    public float? Time { get; set; }           // 循環動作持續秒數
    public bool Flee { get; set; }             // 跑到螢幕另一邊
}

public sealed class KeyRule
{
    public float Chance { get; set; } = 1;     // 觸發機率
    public int Streak { get; set; }            // 在 Window 秒內連按幾下才觸發（0 = 每次）
    public float Window { get; set; } = 2;
    public string? Action { get; set; }
    public string? Line { get; set; }
    public string? Face { get; set; }
}
