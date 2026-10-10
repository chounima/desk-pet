// 依時間反應：啟動時依時段打招呼、深夜提醒去睡覺、連續使用太久提醒起來動一動
// 參數在 data/daily.json，台詞 key 見 data/lines（morning、goSleep、stretchRemind…）
using System;
using DeskPet.Content;

namespace DeskPet.Game;

public sealed class DailyLife
{
    readonly DailyConfig cfg;
    readonly Behavior behavior;
    readonly Func<DateTime> clock;

    float greetDelay = 2.5f;            // 啟動後等一下再打招呼
    DateTime? lastLateNight;
    DateTime? sessionStart;             // 這一段連續使用的開始時間
    DateTime lastActivity = DateTime.MinValue;
    bool remindedThisSession;

    /// <param name="clock">可替換的時間來源（測試用）</param>
    public DailyLife(DailyConfig cfg, Behavior behavior, Func<DateTime>? clock = null)
    {
        this.cfg = cfg;
        this.behavior = behavior;
        this.clock = clock ?? (() => DateTime.Now);
    }

    /// <summary>使用者有在用電腦（打字、點滑鼠）</summary>
    public void OnActivity()
    {
        var now = clock();
        // 停下來超過休息時間 → 這一段結束，重新計算
        if (sessionStart == null || (now - lastActivity).TotalMinutes >= cfg.SitTooLong.BreakMinutes)
        {
            sessionStart = now;
            remindedThisSession = false;
        }
        lastActivity = now;
    }

    public void Update(float dt)
    {
        var now = clock();
        if (greetDelay > 0 && (greetDelay -= dt) <= 0) Greet(now);

        var late = cfg.LateNight;
        if (DailyConfig.InHours(now.Hour, late.From, late.To) && (lastLateNight == null || (now - lastLateNight.Value).TotalMinutes >= late.EveryMinutes))
        {
            // 啟動時的招呼已經講過深夜了，第一次提醒延後一個週期
            if (lastLateNight != null) behavior.Say("goSleep");
            lastLateNight = now;
        }

        if (sessionStart != null && !remindedThisSession
            && (now - lastActivity).TotalMinutes < cfg.SitTooLong.BreakMinutes
            && (now - sessionStart.Value).TotalMinutes >= cfg.SitTooLong.ActiveMinutes)
        {
            remindedThisSession = true;
            behavior.Remind("stretchRemind", "stretch");
        }
    }

    void Greet(DateTime now)
    {
        foreach (var g in cfg.Greetings)
            if (DailyConfig.InHours(now.Hour, g.From, g.To)) { behavior.Say(g.Key); return; }
        behavior.Say("greet");
    }
}
