// 收集系統：點數 💎、好感度 ❤、解鎖（規則與網頁版 preview/js/game/progress.js 相同）
// 數值來自 data/progression.json；只負責規則與狀態，畫面由 Main.Progress.cs 處理
using System;
using System.Collections.Generic;
using System.Linq;
using DeskPet.Content;

namespace DeskPet.Game.Progression;

/// <summary>Type：points（Amount, Reason）/ levelUp（Char, Level）/ unlock（Key）/ buy、equip（ItemId）</summary>
public readonly record struct ProgressEvent(string Type, int Amount = 0, string? Reason = null,
                                           string? Char = null, int Level = 0, string? Key = null, string? ItemId = null);

public sealed class Progress
{
    public ProgressionConfig Cfg { get; set; }
    public ProgressState State { get; private set; }
    readonly Func<DateTime> clock;
    readonly Action<ProgressState> save;
    float companion;                 // 累積的陪伴秒數（滿一分鐘才算）
    string? volleyChar;
    int roundBest;

    public event Action<ProgressEvent>? Event;

    public Progress(ProgressionConfig cfg, ProgressState state, Action<ProgressState> save, Func<DateTime>? clock = null)
    {
        Cfg = cfg;
        State = state;
        this.save = save;
        this.clock = clock ?? (() => DateTime.Now);
    }

    static string DateKey(DateTime d) => d.ToString("yyyy-MM-dd");

    internal void Emit(ProgressEvent e) => Event?.Invoke(e);

    public void Commit() => save(State);

    // ---------- 每日 ----------

    /// <summary>換日時重置每日上限，並發每日登入獎勵；每次計算前都會呼叫</summary>
    public void CheckDay()
    {
        var now = clock();
        var date = DateKey(now);
        var s = State;
        if (s.Day.Date == date && s.Login.Last == date) return;
        if (s.Day.Date != date) s.Day = new ProgressState.DayInfo { Date = date };
        if (s.Login.Last != date)
        {
            s.Login.Streak = s.Login.Last == DateKey(now.AddDays(-1)) ? s.Login.Streak + 1 : 1;
            s.Login.Last = date;
            var L = Cfg.Points.Login;
            AddPoints(L.Base + Math.Min(L.MaxBonus, L.StreakBonus * (s.Login.Streak - 1)), "login");
        }
        Commit();
    }

    // ---------- 點數 ----------

    public void AddPoints(int amount, string reason)
    {
        if (amount <= 0) return;
        State.Points += amount;
        Emit(new ProgressEvent("points", amount, reason));
    }

    int Capped(string kind, int amount, int max)
    {
        var pts = State.Day.Pts;
        var used = pts.GetValueOrDefault(kind);
        var give = Math.Max(0, Math.Min(amount, max - used));
        pts[kind] = used + give;
        AddPoints(give, kind);
        return give;
    }

    /// <summary>只計次數，不記錄按了哪個鍵</summary>
    public void OnKeys(int n)
    {
        if (n <= 0) return;
        CheckDay();
        var T = Cfg.Points.Typing;
        var d = State.Day;
        d.Keys += n;
        var earned = d.Keys / T.Every * T.Gain;
        var given = d.Pts.GetValueOrDefault("typing");
        if (earned > given) Capped("typing", earned - given, T.DailyMax);
        Commit();
    }

    public void OnInteraction(string ch)
    {
        CheckDay();
        Capped("interaction", Cfg.Points.Interaction.Gain, Cfg.Points.Interaction.DailyMax);
        AddXp(ch, Cfg.Affection.Interaction, "interaction", Cfg.Affection.InteractionDailyMax);
        Commit();
    }

    /// <summary>排球每次來回數變化時呼叫（rally = 目前連續來回數）</summary>
    public void OnVolley(string ch, int rally)
    {
        CheckDay();
        var P = Cfg.Points.Volley;
        var A = Cfg.Affection;
        if (rally > 0 && rally % P.EveryRally == 0) Capped("volley", P.Gain, P.DailyMax);
        if (rally > 0 && rally % A.VolleyEveryRally == 0) AddXp(ch, A.VolleyGain, "volley", A.VolleyDailyMax);
        var best = State.Best.GetValueOrDefault(ch);
        // 新的一局：記下開局時的紀錄，整局只在超過它的那一球給一次獎勵；第一次玩不算破紀錄
        if (rally <= 1 || volleyChar != ch)
        {
            volleyChar = ch;
            roundBest = best;
        }
        if (rally > best) State.Best[ch] = rally;
        if (roundBest > 0 && rally == roundBest + 1) AddPoints(P.RecordBonus, "record");
        Commit();
    }

    /// <summary>陪伴時間：目前顯示的角色每分鐘加好感度</summary>
    public void Tick(float dt, string ch)
    {
        companion += dt;
        if (companion < 60) return;
        companion -= 60;
        CheckDay();
        AddXp(ch, Cfg.Affection.CompanionPerMinute, "companion", Cfg.Affection.CompanionDailyMax);
        Commit();
    }

    // ---------- 好感度 ----------

    public void AddXp(string ch, int amount, string? kind = null, int dailyMax = int.MaxValue)
    {
        if (kind != null)
        {
            if (!State.Day.Xp.TryGetValue(kind, out var used)) State.Day.Xp[kind] = used = new();
            var u = used.GetValueOrDefault(ch);
            amount = Math.Max(0, Math.Min(amount, dailyMax - u));
            used[ch] = u + amount;
        }
        if (amount <= 0) return;
        var before = Level(ch).Level;
        State.Xp[ch] = State.Xp.GetValueOrDefault(ch) + amount;
        var after = Level(ch).Level;
        for (var lv = before + 1; lv <= after; lv++)
        {
            Emit(new ProgressEvent("levelUp", Char: ch, Level: lv));
            AddPoints(Cfg.Points.LevelUp, "levelUp");
        }
        if (after > before) CheckUnlocks();
    }

    /// <summary>To = 下一級門檻（滿級時為 null）</summary>
    public (int Level, int Xp, int From, int? To) Level(string ch)
    {
        var xp = State.Xp.GetValueOrDefault(ch);
        var L = Cfg.Affection.Levels;
        var level = 0;
        while (level < L.Count && xp >= L[level]) level++;
        return (level, xp, level > 0 ? L[level - 1] : 0, level < L.Count ? (int?)L[level] : null);
    }

    // ---------- 解鎖 ----------

    /// <summary>條件達成就自動解鎖（cost 類型要自己買）</summary>
    public void CheckUnlocks()
    {
        foreach (var (key, rule) in Cfg.Unlocks)
        {
            if (State.Unlocked.Contains(key) || rule.Affection == null) continue;
            if (Level(rule.Affection.Char).Level >= rule.Affection.Level)
            {
                State.Unlocked.Add(key);
                Emit(new ProgressEvent("unlock", Key: key));
            }
        }
    }

    /// <summary>沒有列在 unlocks 裡的一律開放；角色另外看 start.characters</summary>
    public bool IsUnlocked(string key)
    {
        if (State.Unlocked.Contains(key)) return true;
        if (key.StartsWith("character:")) return Cfg.Start.Characters.Contains(key[10..]) || !Cfg.Unlocks.ContainsKey(key);
        return !Cfg.Unlocks.ContainsKey(key);
    }

    public bool CharacterUnlocked(string id) => IsUnlocked($"character:{id}");

    /// <summary>角色的預設型態一律開放</summary>
    public bool FormUnlocked(string id, string form, string? defaultForm) => form == defaultForm || IsUnlocked($"form:{id}/{form}");

    /// <summary>還沒解鎖時的說明，例如「Chouma 好感度 Lv3 時…（Chouma ❤ Lv1 / 3）」；names 把角色 id 轉成名稱</summary>
    public string? LockHint(string key, Func<string, string> names)
    {
        if (IsUnlocked(key) || !Cfg.Unlocks.TryGetValue(key, out var rule)) return null;
        var hint = rule.Hint ?? "尚未解鎖";
        if (rule.Cost is { } cost) return $"{hint}（💎 {cost}）";
        var a = rule.Affection!;
        return $"{hint}（{names(a.Char)} ❤ Lv{Level(a.Char).Level} / {a.Level}）";
    }

    // ---------- 開發者模式 ----------

    public void Reset()
    {
        State = new ProgressState();
        CheckDay();
    }

    /// <summary>把存檔裡的日期往前推一天，下一次 CheckDay 就會當作換日</summary>
    public void SimulateNextDay()
    {
        string? Back(string? d) => d == null ? null : DateKey(DateTime.Parse(d).AddDays(-1));
        State.Day.Date = Back(State.Day.Date);
        State.Login.Last = Back(State.Login.Last);
        CheckDay();
    }

    /// <summary>目前所有解鎖規則的 key（收藏視窗用）</summary>
    public string[] UnlockKeys => Cfg.Unlocks.Keys.ToArray();
}
