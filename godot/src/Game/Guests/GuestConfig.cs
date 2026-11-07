// 客人角色的設定（data/guests.json）：人數上限、會議軟體、觸發規則（聊天關鍵字、表情反應 → 動作，格式見 Game/Rules/Rule.cs）
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DeskPet.Content;
using DeskPet.Game.Rules;

namespace DeskPet.Game.Guests;

public sealed class GuestConfig
{
    /// <summary>同時最多幾個客人（設定可以再調低）</summary>
    public int MaxGuests { get; set; } = 8;
    /// <summary>直播聊天室的客人多久沒講話就離開（分鐘）</summary>
    public float ChatIdleMinutes { get; set; } = 10;
    /// <summary>聊天泡泡最多顯示幾個字</summary>
    public int ChatMaxChars { get; set; } = 40;
    /// <summary>客人比主角色小一點，比較分得出來</summary>
    public float Scale { get; set; } = 0.85f;
    /// <summary>偵測音量的會議軟體 { 程式名稱（不含 .exe）: 顯示名稱 }</summary>
    public Dictionary<string, string> MeetingApps { get; set; } = new();
    /// <summary>音量（0～1 峰值）超過多少算在講話：麥克風、會議軟體</summary>
    public float MicThreshold { get; set; } = 0.06f;
    public float AppThreshold { get; set; } = 0.02f;
    /// <summary>觸發規則（依順序找第一個符合的）</summary>
    public List<Rule> Rules { get; set; } = new();

    // 舊格式（2026-11-07 前）：讀到時轉成 Rules
    public List<LegacyTrigger>? ChatTriggers { get; set; }
    public Dictionary<string, string>? Reactions { get; set; }

    public sealed class LegacyTrigger
    {
        public List<string> Match { get; set; } = new();
        public string Action { get; set; } = "";
    }

    RuleSet? set;
    public RuleSet RuleSet => set ??= new RuleSet(Rules);

    /// <summary>聊天內容對應的規則（第一個符合的）；key = 冷卻時間算在誰身上</summary>
    public Rule? ForChat(string text, string? key = null, double now = 0) => RuleSet.First("chat", text, key, now);

    public Rule? ForReaction(string emoji, string? key = null, double now = 0) => RuleSet.First("reaction", emoji, key, now);

    /// <summary>測試用：聊天內容對應的動作</summary>
    public string? ActionForChat(string text) => ForChat(text)?.Do.Action;

    public string? ActionForReaction(string emoji) => ForReaction(emoji)?.Do.Action;

    public static GuestConfig Load()
    {
        var cfg = JsonSerializer.Deserialize<GuestConfig>(ContentLoader.ReadText($"{ContentLoader.Root}/data/guests.json"),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        cfg.ConvertLegacy();
        return cfg;
    }

    void ConvertLegacy()
    {
        foreach (var t in ChatTriggers ?? new())
            Rules.Add(new Rule { When = new RuleWhen { Source = "chat", Match = t.Match }, Do = new RuleDo { Action = t.Action } });
        // "*" 是預設，放最後
        foreach (var (emoji, action) in (Reactions ?? new()).OrderBy(kv => kv.Key == "*"))
            Rules.Add(new Rule { When = new RuleWhen { Source = "reaction", Emoji = new() { emoji } }, Do = new RuleDo { Action = action } });
        ChatTriggers = null;
        Reactions = null;
    }
}
