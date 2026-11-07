// --selftest=vtuber：半身模式的聲音規則（直接餵聲音分析結果，不用麥克風、不開畫面）
using System.Collections.Generic;
using System;
using DeskPet.Content;
using DeskPet.Core.Voice;
using DeskPet.Game.Control;
using DeskPet.Game.Vtuber;
using Godot;

namespace DeskPet.Tests;

public static class VtuberSelfTest
{
    public static int Run()
    {
        var failures = new List<string>();
        void Check(bool ok, string what) { if (!ok) failures.Add(what); }

        double now = 0;
        var started = new List<string>();
        var control = new ControlArbiter(a => { started.Add(a); return true; }, () => now);
        var cfg = VtuberConfig.Load();
        var rules = new VoiceRules(cfg);
        var cal = new VoiceCalibration();
        const float dt = 1f / 60;

        void Run(float seconds, VoiceFrame f)
        {
            for (var t = 0f; t < seconds; t += dt)
            {
                now += dt;
                cal.Update(f);
                rules.Update(f, cal, dt, control, now);
            }
        }

        // 1. 安靜 2 秒：噪音底線跟著環境（-70 dB → 約 -67）
        Run(2, new VoiceFrame(-70, false, 0, null));
        Check(cal.Floor is > -68 and < -66, $"噪音底線 {cal.Floor:0.0}");
        Check(rules.MouthShape(new VoiceFrame(-70, false, 0, null), cal) == "closed", "安靜時閉嘴");

        // 2. 平常講話 3 秒：-30 dB、220 Hz → 平常音量、音調
        Run(3, new VoiceFrame(-30, true, 220, "a"));
        Check(System.Math.Abs(cal.BasePitch - 220) < 1 && System.Math.Abs(cal.SpeechDb + 30) < 0.1, $"平常音調 {cal.BasePitch}、音量 {cal.SpeechDb}");
        Check(rules.MouthShape(new VoiceFrame(-30, true, 220, "i"), cal) == "i", "認出母音就用母音嘴型");
        Check(rules.MouthShape(new VoiceFrame(-30, true, 0, null), cal) == "open", "大聲沒母音 = 全開");
        Check(rules.MouthShape(new VoiceFrame(-45, true, 0, null), cal) == "half", "小聲 = 半開");
        Check(control.Winner(Channel.Face) == null && started.Count == 0, $"平常講話不觸發任何規則：{control.Winner(Channel.Face)} {string.Join(",", started)}");

        // 3. 突然大聲 → 驚訝
        Run(dt, new VoiceFrame(-14, true, 220, "a"));
        Check(rules.LastFired == "surprise" && control.Winner(Channel.Face) == "surprised", $"突然大聲要驚訝：{rules.LastFired} face={control.Winner(Channel.Face)} mouth={control.Winner(Channel.Mouth)}");
        Run(1.5f, new VoiceFrame(-30, true, 220, "a"));

        // 4. 音調比平常高 5 個半音（約 294 Hz）0.6 秒以上 → 開心
        Run(0.8f, new VoiceFrame(-30, true, 294, "a"));
        Check(rules.LastFired == "happy" && control.Winner(Channel.Face) == "happy", $"高音要開心：{rules.LastFired} {control.Winner(Channel.Face)}");

        // 5. 持續大聲 2 秒以上 → 興奮（歡呼）
        Run(2.2f, new VoiceFrame(-20, true, 220, "a"));
        Check(started.Contains("cheer"), $"持續大聲要歡呼：{string.Join(",", started)}");

        // 6. 安靜 31 秒 → 伸懶腰
        started.Clear();
        Run(31, new VoiceFrame(-70, false, 0, null));
        Check(started.Contains("stretch"), $"安靜 30 秒要伸懶腰：{string.Join(",", started)}");

        // 7. 快捷鍵：data/vtuber.json 的組合鍵都要認得
        Check(new Platform.Hotkeys(cfg.Hotkeys.Keys).Count == cfg.Hotkeys.Count, "快捷鍵都要認得");
        Check(new Platform.Hotkeys(new[] { "Ctrl+Alt+F5", "Shift+Q", "Ctrl+?" }).Count == 2, "F5、字母可以，其他符號略過");

        // 8. 效能：10 秒的母音（48 kHz）分析＋校正＋規則，平均每幀（1/60 秒）不超過 1 ms
        var audio = VoiceAnalyzer.Synth("vowel", 180, 0.3, new double[] { 800, 1200 }, 10, 48000);
        var analyzer = new VoiceAnalyzer(48000);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 600; i++)
        {
            var frame = analyzer.Feed(audio.AsSpan(i * 800, 800));
            cal.Update(frame);
            rules.Update(frame, cal, dt, control, now += dt);
        }
        var perFrame = sw.Elapsed.TotalMilliseconds / 600;
        GD.Print($"[vtuber] 聲音分析每幀 {perFrame:0.000} ms");
        Check(perFrame < 1, $"聲音分析每幀 {perFrame:0.000} ms（要 < 1）");

        // 9. data/face.json 的表情調整：改既有表情、新增表情（從 idle 複製）、拿掉圖層
        var fc = System.Text.Json.JsonSerializer.Deserialize<FaceConfig>(
            "{\"expressions\":{\"tk01\":{\"happy\":{\"mouth\":\"va\",\"alpha\":{\"blush\":1}},\"wink\":{\"eyes\":\"arc\",\"extras\":\"\"}}}}",
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var tk = ContentLoader.LoadCharacters().rigs["tk01"];
        fc.Apply(tk, "tk01");
        var faceRig = tk.Face!;
        Check(faceRig.Expressions["happy"]["mouth"] == "va" && faceRig.Alpha!["happy"]["blush"] == 1, "改既有表情的嘴與腮紅");
        Check(faceRig.Expressions["wink"]["eyes"] == "arc" && faceRig.Expressions["wink"]["mouth"] == faceRig.Expressions["idle"]["mouth"]
              && !faceRig.Expressions["wink"].ContainsKey("extras") && faceRig.Alpha!.ContainsKey("wink"), "新表情從 idle 複製、空字串拿掉圖層");

        foreach (var f in failures) GD.Print($"[vtuber] ✗ {f}");
        GD.Print(failures.Count == 0 ? "[vtuber] 全部通過" : $"[vtuber] {failures.Count} 個失敗");
        return failures.Count == 0 ? 0 : 1;
    }
}
