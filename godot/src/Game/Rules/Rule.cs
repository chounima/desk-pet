// 觸發規則（統一格式，schemas/rules.schema.json）：{ when: { source, 條件… }, do: { action / face / line / fx }, cooldown, priority }
//
// 來源與條件：
//   chat      match: [關鍵字…]   英數字要整個字相符（hi 不會配到 this），中日文出現就算
//   reaction  emoji: [emoji…]    "*" = 其他所有 emoji（放在最後當預設）
// 之後的聲音規則（vtuber.json）、快捷鍵也用這個格式，各自加自己的條件欄位（沒用到的欄位保留在 Extra）。
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DeskPet.Game.Rules;

public sealed class RuleWhen
{
    public string Source { get; set; } = "";
    public List<string>? Match { get; set; }
    public List<string>? Emoji { get; set; }
    /// <summary>其他來源的條件（之後的聲音規則：volumeAbove、pitchAbove…）</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class RuleDo
{
    public string? Action { get; set; }
    public string? Face { get; set; }
    /// <summary>台詞 key（data/lines）</summary>
    public string? Line { get; set; }
    /// <summary>特效文字（例如 emoji，從頭上飄出來）</summary>
    public string? Fx { get; set; }
    /// <summary>表情維持幾秒</summary>
    public float Seconds { get; set; } = 2.5f;
}

public sealed class Rule
{
    public RuleWhen When { get; set; } = new();
    public RuleDo Do { get; set; } = new();
    /// <summary>同一個對象觸發後多久內不再觸發（秒）</summary>
    public float Cooldown { get; set; }
    /// <summary>優先順序（Control.Priority），預設是「反應」</summary>
    public int Priority { get; set; } = Control.Priority.Reaction;

    Regex? regex;

    public bool Matches(string source, string text)
    {
        if (source != When.Source) return false;
        return source switch
        {
            "chat" => When.Match is { Count: > 0 } && (regex ??= KeywordRegex(When.Match)).IsMatch(text),
            "reaction" => When.Emoji is { } e && (e.Contains(text) || e.Contains(text.TrimEnd('️')) || e.Contains("*")),
            _ => false,
        };
    }

    // 英數字的關鍵字只看前後是不是英數字：「hi大家」也算 hi，「this」不算
    static Regex KeywordRegex(IEnumerable<string> words) =>
        new(string.Join("|", words.Select(Pattern)), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    static string Pattern(string word)
    {
        var p = Regex.Escape(word);
        if (word.Length > 0 && char.IsAsciiLetterOrDigit(word[0])) p = "(?<![A-Za-z0-9])" + p;
        if (word.Length > 0 && char.IsAsciiLetterOrDigit(word[^1])) p += "(?![A-Za-z0-9])";
        return p;
    }
}

/// <summary>一組規則：依順序找第一個符合的；同一個對象（key）在冷卻時間內不重複觸發</summary>
public sealed class RuleSet
{
    readonly List<Rule> rules;
    readonly Dictionary<(Rule, string), double> lastFired = new();

    public RuleSet(List<Rule> rules) => this.rules = rules;

    public IReadOnlyList<Rule> All => rules;

    /// <param name="key">冷卻計算的對象（例如客人的 key）；null = 不算冷卻</param>
    public Rule? First(string source, string text, string? key = null, double now = 0)
    {
        foreach (var r in rules)
        {
            if (!r.Matches(source, text)) continue;
            if (key != null && r.Cooldown > 0)
            {
                if (lastFired.TryGetValue((r, key), out var t) && now - t < r.Cooldown) continue;
                lastFired[(r, key)] = now;
            }
            return r;
        }
        return null;
    }
}
