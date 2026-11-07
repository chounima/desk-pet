// 聲音 → 表情與動作（data/vtuber.json 的 rules）：每幀看聲音分析結果，條件成立夠久就透過 ControlArbiter 請求表情／動作
// 另外決定嘴型（Mouth 管道：closed、half、open、母音），分層臉換嘴巴圖層，只有整張臉的角色換成驚訝／開心。
using System.Collections.Generic;
using System.Linq;
using DeskPet.Content;
using DeskPet.Core.Voice;
using DeskPet.Game.Control;

namespace DeskPet.Game.Vtuber;

public sealed class VoiceRules
{
    readonly VtuberConfig cfg;
    readonly Dictionary<string, (float held, double cooldownUntil)> state = new();
    readonly Queue<double> recentDb = new();       // 前 1 秒「講話中」的音量（突然變大用；剛開口不算）
    float silentFor;

    /// <summary>最近一次觸發的規則（Hub 分頁顯示、測試用）</summary>
    public string? LastFired { get; private set; }

    public VoiceRules(VtuberConfig cfg) => this.cfg = cfg;

    /// <summary>這一幀的嘴型（Mouth 管道的值）</summary>
    public string MouthShape(VoiceFrame f, VoiceCalibration cal)
    {
        if (!f.Speaking || f.Db <= cal.Floor + VoiceAnalyzer.Open) return "closed";
        return f.Vowel ?? (f.Db >= cal.SpeechDb - cfg.Mouth.OpenDb ? "open" : "half");
    }

    public void Update(VoiceFrame f, VoiceCalibration cal, float dt, ControlArbiter control, double now)
    {
        silentFor = f.Speaking ? 0 : silentFor + dt;
        var avg = recentDb.Count >= VoiceAnalyzer.Fps / 2 ? recentDb.Average() : double.PositiveInfinity;

        foreach (var r in cfg.Rules)
        {
            var (held, cooldownUntil) = state.GetValueOrDefault(r.Id);
            var w = r.When;
            bool? ok = null;           // null = 這一幀不算（例如講話中但沒有音調），保持原本的累計
            if (w.JumpDb is { } jump) ok = f.Speaking && f.Db - avg >= jump;
            else if (w.LoudDb is { } loud) ok = f.Speaking && f.Db >= cal.SpeechDb + loud;
            else if (w.Semitones is { } st)
            {
                if (!f.Speaking || cal.BasePitch <= 0) ok = false;
                else if (f.Pitch > 0)
                {
                    var s = VoiceAnalyzer.Semitones(f.Pitch, cal.BasePitch);
                    ok = st >= 0 ? s >= st : s <= st;
                }
            }
            else if (w.SilentSeconds is { } quiet) ok = silentFor >= quiet;

            if (ok == true) held += dt;
            else if (ok == false) held = 0;
            if (held >= w.ForSeconds && ok == true && now >= cooldownUntil)
            {
                Fire($"voice:{r.Id}", r, r.Priority, control);
                LastFired = r.Id;
                held = 0;
                cooldownUntil = now + r.Cooldown;
            }
            state[r.Id] = (held, cooldownUntil);
        }

        if (f.Speaking) recentDb.Enqueue(f.Db);
        else if (silentFor > 0.5f) recentDb.Clear();
        while (recentDb.Count > VoiceAnalyzer.Fps) recentDb.Dequeue();
    }

    /// <summary>做一個反應（規則或快捷鍵）</summary>
    public static void Fire(string source, VoiceReaction r, int priority, ControlArbiter control)
    {
        if (r.Face != null) control.Request(source, Channel.Face, r.Face, priority, r.Seconds);
        if (r.Action != null) control.TryAct(source, r.Action, priority, r.Seconds);
    }
}
