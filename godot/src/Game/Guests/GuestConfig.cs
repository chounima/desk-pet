// 客人角色的規則（data/guests.json）：人數上限、聊天關鍵字 → 動作、表情反應 → 動作
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using DeskPet.Content;

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
    public List<Trigger> ChatTriggers { get; set; } = new();
    /// <summary>{ emoji: 動作 }，"*" = 其他 emoji</summary>
    public Dictionary<string, string> Reactions { get; set; } = new();

    public sealed class Trigger
    {
        public List<string> Match { get; set; } = new();
        public string Action { get; set; } = "";
        Regex? regex;

        // 英數字的關鍵字要整個字相符（hi 不會配到 this），中日文直接找有沒有出現
        public bool IsMatch(string text) => (regex ??= new Regex(string.Join("|", Match.Select(Pattern)),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).IsMatch(text);

        static string Pattern(string word)
        {
            var p = Regex.Escape(word);
            // 只看前後是不是英數字：「hi大家」也算 hi
            if (word.Length > 0 && char.IsAsciiLetterOrDigit(word[0])) p = "(?<![A-Za-z0-9])" + p;
            if (word.Length > 0 && char.IsAsciiLetterOrDigit(word[^1])) p += "(?![A-Za-z0-9])";
            return p;
        }
    }

    /// <summary>聊天內容對應的動作（第一個符合的關鍵字）；沒有就回傳 null</summary>
    public string? ActionForChat(string text) => ChatTriggers.FirstOrDefault(t => t.IsMatch(text))?.Action;

    public string? ActionForReaction(string emoji) =>
        Reactions.GetValueOrDefault(emoji) ?? Reactions.GetValueOrDefault(emoji.TrimEnd('️')) ?? Reactions.GetValueOrDefault("*");

    public static GuestConfig Load() =>
        JsonSerializer.Deserialize<GuestConfig>(ContentLoader.ReadText($"{ContentLoader.Root}/data/guests.json"),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
}
