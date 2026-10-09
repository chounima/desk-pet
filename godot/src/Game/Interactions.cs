// 互動判定：點擊部位、連擊計數、按鍵連按 → 對應的規則
using System.Collections.Generic;
using System.Linq;
using DeskPet.Content;
using DeskPet.Platform;
using Godot;

namespace DeskPet.Game;

public sealed class Interactions
{
    readonly InteractionData data;
    string? lastZone;
    int combo;
    double lastClick;
    readonly Dictionary<GlobalInput.KeyKind, List<double>> keyTimes = new();

    public Interactions(InteractionData data) => this.data = data;

    static double Now() => Time.GetTicksMsec() / 1000.0;

    /// <summary>
    /// 點到的插槽 → 部位。頭部再依點擊位置分上下：上半部（頭頂、頭髮）= head，下半部 = face。
    /// </summary>
    /// <param name="headLocalY">點擊位置換算到頭部骨骼座標的 y</param>
    /// <param name="headTopY">頭頂在頭部骨骼座標的 y（負值）</param>
    public static string ZoneOf(string slot, float headLocalY, float headTopY) => slot switch
    {
        "head" or "face" or "hair_front" or "hair_back" => headLocalY < headTopY * 0.55f ? "head" : "face",
        "torso" or "hips" or "back" or "cape" => "body",
        _ when slot.StartsWith("arm") || slot.StartsWith("hand") => "hand",
        _ when slot.StartsWith("leg") || slot.StartsWith("foot") => "feet",
        "tail" => "tail",
        _ => "body",
    };

    /// <summary>點擊某部位：同一部位在時間內連點會累積連擊，回傳符合的最高規則</summary>
    public ReactionRule? OnClick(string zone)
    {
        var t = Now();
        combo = zone == lastZone && t - lastClick <= data.ComboWindow ? combo + 1 : 1;
        lastZone = zone;
        lastClick = t;
        if (!data.Click.TryGetValue(zone, out var rules)) return null;
        return rules.Where(r => r.Min <= combo).OrderBy(r => r.Min).LastOrDefault();
    }

    public int Combo => combo;

    public ReactionRule? Busy(string state) => data.Busy.GetValueOrDefault(state);

    /// <summary>按鍵：依機率或連按次數決定是否觸發</summary>
    public KeyRule? OnKey(GlobalInput.KeyKind kind)
    {
        if (!data.Keys.TryGetValue(kind.ToString(), out var rule)) return null;
        if (rule.Streak > 0)
        {
            var t = Now();
            var times = keyTimes.TryGetValue(kind, out var list) ? list : keyTimes[kind] = new List<double>();
            times.Add(t);
            times.RemoveAll(k => t - k > rule.Window);
            if (times.Count < rule.Streak) return null;
            times.Clear();          // 觸發後重新累計，避免每按一下都觸發
        }
        return GD.Randf() < rule.Chance ? rule : null;
    }
}
