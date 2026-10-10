// 成就：依統計數字判斷是否達成（data/achievements.json），解鎖後送點數，並通知外部平台（之後接 Steam）
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DeskPet.Content;

namespace DeskPet.Game.Progression;

public sealed class AchievementDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Desc { get; set; } = "";
    public string Stat { get; set; } = "";
    public int Goal { get; set; } = 1;
    public int Points { get; set; }
}

/// <summary>成就與統計要同步到的平台（Steam 等）。沒有接平台時用 LocalOnly</summary>
public interface IAchievementSink
{
    void Unlock(string id);
    void SetStat(string name, int value);
}

public sealed class LocalOnlySink : IAchievementSink
{
    public void Unlock(string id) { }
    public void SetStat(string name, int value) { }
}

public sealed class Achievements
{
    readonly Progress progress;
    public List<AchievementDef> All { get; }
    public IAchievementSink Sink { get; set; } = new LocalOnlySink();

    sealed class FileFormat { public List<AchievementDef> Achievements { get; set; } = new(); }

    public Achievements(Progress progress, List<AchievementDef>? defs = null)
    {
        this.progress = progress;
        All = defs ?? JsonSerializer.Deserialize<FileFormat>(ContentLoader.ReadText($"{ContentLoader.Root}/data/achievements.json"),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!.Achievements;
        progress.State.Achievements ??= new();
    }

    public bool IsUnlocked(string id) => progress.State.Achievements.Contains(id);

    /// <summary>統計項目目前的數字</summary>
    public int Value(string stat)
    {
        var s = progress.State;
        if (stat.StartsWith("unlock:")) return progress.IsUnlocked(stat[7..]) ? 1 : 0;
        return stat switch
        {
            "volleyBest" => s.Best.Values.DefaultIfEmpty(0).Max(),
            "itemsOwned" => s.Owned.Count,
            "affectionLevel" => s.Xp.Keys.Select(c => progress.Level(c).Level).DefaultIfEmpty(0).Max(),
            "streak" => s.Login.Streak,
            _ => s.Stats.GetValueOrDefault(stat),
        };
    }

    /// <summary>檢查所有成就；回傳這次新解鎖的</summary>
    public List<AchievementDef> Check()
    {
        var unlocked = new List<AchievementDef>();
        foreach (var a in All)
        {
            if (IsUnlocked(a.Id) || Value(a.Stat) < a.Goal) continue;
            progress.State.Achievements.Add(a.Id);
            progress.AddPoints(a.Points, "achievement");
            progress.Emit(new ProgressEvent("achievement", Key: a.Id));
            Sink.Unlock(a.Id);
            unlocked.Add(a);
        }
        if (unlocked.Count > 0) progress.Commit();
        return unlocked;
    }
}
