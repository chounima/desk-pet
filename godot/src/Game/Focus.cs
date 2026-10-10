// 專注模式（番茄鐘）：專注 → 短休息 → 專注 …，每 N 輪換一次長休息
// 只負責計時與狀態，畫面與角色反應由 Main.Focus.cs 處理
using System;
using System.Collections.Generic;
using System.Text.Json;
using DeskPet.Content;

namespace DeskPet.Game;

public sealed class FocusConfig
{
    public float WorkMinutes { get; set; } = 25;
    public float ShortBreakMinutes { get; set; } = 5;
    public float LongBreakMinutes { get; set; } = 15;
    public int LongBreakEvery { get; set; } = 4;
    public List<string> CalmActions { get; set; } = new();
    public List<string> BreakActions { get; set; } = new();
    public RewardConfig Reward { get; set; } = new();

    public sealed class RewardConfig
    {
        public int Points { get; set; } = 5;
        public int Affection { get; set; } = 3;
    }

    public static FocusConfig Load() =>
        JsonSerializer.Deserialize<FocusConfig>(ContentLoader.ReadText($"{ContentLoader.Root}/data/focus.json"),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
}

public sealed class Focus
{
    public enum Phase { Off, Work, Break }

    readonly FocusConfig cfg;
    public Phase Current { get; private set; } = Phase.Off;
    /// <summary>這一段剩下的秒數</summary>
    public float Remaining { get; private set; }
    public float Total { get; private set; }
    /// <summary>這次連續完成了幾輪專注（決定長休息）</summary>
    public int Rounds { get; private set; }

    /// <summary>使用者自訂的時間（分鐘）；null 用 focus.json 的預設</summary>
    public float? WorkMinutes { get; set; }
    public float? BreakMinutes { get; set; }

    public event Action? WorkStarted;
    /// <summary>完成一輪專注（給獎勵），參數 = 是否長休息</summary>
    public event Action<bool>? WorkCompleted;
    public event Action? BreakEnded;
    public event Action? Stopped;

    public Focus(FocusConfig cfg) => this.cfg = cfg;

    public bool Working => Current == Phase.Work;

    public void Start()
    {
        Rounds = 0;
        BeginWork();
    }

    public void Stop()
    {
        if (Current == Phase.Off) return;
        Current = Phase.Off;
        Stopped?.Invoke();
    }

    /// <summary>直接跳到下一段（專注中按 = 提早休息，但不算完成）</summary>
    public void Skip()
    {
        if (Current == Phase.Work) BeginBreak(long_: false);
        else if (Current == Phase.Break) { BreakEnded?.Invoke(); BeginWork(); }
    }

    void BeginWork()
    {
        Current = Phase.Work;
        Total = Remaining = (WorkMinutes ?? cfg.WorkMinutes) * 60;
        WorkStarted?.Invoke();
    }

    void BeginBreak(bool long_)
    {
        Current = Phase.Break;
        var minutes = long_ ? cfg.LongBreakMinutes : BreakMinutes ?? cfg.ShortBreakMinutes;
        Total = Remaining = minutes * 60;
    }

    public void Update(float dt)
    {
        if (Current == Phase.Off) return;
        Remaining -= dt;
        if (Remaining > 0) return;
        if (Current == Phase.Work)
        {
            Rounds++;
            var long_ = cfg.LongBreakEvery > 0 && Rounds % cfg.LongBreakEvery == 0;
            BeginBreak(long_);
            WorkCompleted?.Invoke(long_);
        }
        else
        {
            BreakEnded?.Invoke();
            BeginWork();
        }
    }

    /// <summary>倒數文字，例如 🍅 24:59</summary>
    public string Label()
    {
        var s = Math.Max(0, (int)Math.Ceiling(Remaining));
        var icon = Current == Phase.Work ? "🍅" : "☕";
        return $"{icon} {s / 60:00}:{s % 60:00}";
    }
}
