// 收集系統與商店的自我測試（--selftest=progress）：和 preview/tests/progress.html 相同的情境，
// 確認 C# 版的規則和網頁版一致。不讀寫存檔，用固定的日期。
using System;
using System.Collections.Generic;
using System.Linq;
using DeskPet.Content;
using Godot;

namespace DeskPet.Game.Progression;

public static class ProgressSelfTest
{
    public static int Run()
    {
        var cfg = ProgressionConfig.Parse(ProgressionConfig.DefaultText());
        var fails = 0;
        void Check(string name, bool ok, string detail = "")
        {
            if (!ok) fails++;
            GD.Print($"{(ok ? "✓" : "✗")} {name}{(detail != "" ? $"　({detail})" : "")}");
        }

        var day = new DateTime(2026, 1, 1, 12, 0, 0);
        var events = new List<ProgressEvent>();
        var p = new Progress(cfg, new ProgressState(), _ => { }, () => day);
        p.Event += events.Add;

        p.CheckDay();
        Check("第一次登入送基本點數", p.State.Points == cfg.Points.Login.Base, $"points={p.State.Points}");
        var before = p.State.Points;
        p.OnKeys(100000);
        Check("打字點數有每日上限", p.State.Points - before == cfg.Points.Typing.DailyMax, $"+{p.State.Points - before}");
        var b2 = p.State.Points;
        for (var i = 0; i < 50; i++) p.OnInteraction("tk01");
        Check("互動點數有每日上限", p.State.Points - b2 == cfg.Points.Interaction.DailyMax, $"+{p.State.Points - b2}");
        Check("互動好感度有每日上限", p.State.Xp["tk01"] == cfg.Affection.InteractionDailyMax);
        Check("一開始 Choupaca 鎖住", !p.CharacterUnlocked("choupaca"));
        Check("四足型態鎖住", !p.FormUnlocked("choupaca", "quadruped", "upright"));

        var need = cfg.Unlocks["character:choupaca"].Affection!;
        p.AddXp("chouma", cfg.Affection.Levels[need.Level - 1]);
        Check("Chouma 好感度到了就解鎖 Choupaca", p.CharacterUnlocked("choupaca"));
        Check("發出解鎖事件", events.Any(e => e.Type == "unlock" && e.Key == "character:choupaca"));
        var nf = cfg.Unlocks["form:choupaca/quadruped"].Affection!;
        p.AddXp("choupaca", cfg.Affection.Levels[nf.Level - 1]);
        Check("解鎖四足型態", p.FormUnlocked("choupaca", "quadruped", "upright"));

        for (var r = 1; r <= 12; r++) p.OnVolley("sera", r);
        Check("排球第一次玩不算破紀錄", !events.Any(e => e.Reason == "record"));
        for (var r = 1; r <= 13; r++) p.OnVolley("sera", r);
        Check("排球破紀錄有獎勵（一局一次）", events.Count(e => e.Reason == "record") == 1);

        var xp0 = p.State.Xp.GetValueOrDefault("mira");
        p.Tick(59, "mira");
        Check("陪伴不滿一分鐘不加", p.State.Xp.GetValueOrDefault("mira") == xp0);
        p.Tick(2, "mira");
        Check("陪伴滿一分鐘加好感度", p.State.Xp["mira"] == xp0 + cfg.Affection.CompanionPerMinute);

        var d = new DateTime(2026, 1, 1);
        var q = new Progress(cfg, new ProgressState(), _ => { }, () => d);
        q.CheckDay();
        d = d.AddDays(1);
        q.CheckDay();
        Check("隔天連續登入 2 天", q.State.Login.Streak == 2);
        d = d.AddDays(3);
        q.CheckDay();
        Check("中斷後重新計算", q.State.Login.Streak == 1);

        var s = new Progress(cfg, new ProgressState(), _ => { }, () => day);
        var shop = new Shop(s);
        Check("預設款式不用買就擁有", shop.Owns("ball:classic") && shop.Equipped("ball") == "ball:classic");
        Check("點數不夠不能買", !shop.Buy("ball:beach") && shop.CanBuy("ball:beach").Reason == "points");
        s.AddPoints(1000, "test");
        Check("點數夠就能買，外觀直接換上", shop.Buy("ball:beach") && shop.Equipped("ball") == "ball:beach");
        Check("扣了點數", s.State.Points == 1000 - cfg.Shop.Items["ball:beach"].Price);
        Check("買過不能重買", shop.CanBuy("ball:beach").Reason == "owned");
        Check("可以換回預設款式", shop.Equip("ball:classic") && shop.Equipped("ball") == "ball:classic");
        Check("沒達成條件不能買", shop.CanBuy("ball:fire").Reason == "locked");
        Check("動作商品沒買不能用", !shop.ActionOwned("coffee"));
        Check("不是商品的動作可以用", shop.ActionOwned("wave"));
        shop.Buy("action:coffee");
        Check("動作買了就能用", shop.ActionOwned("coffee"));

        GD.Print(fails == 0 ? "全部通過" : $"✗ {fails} 項失敗");
        return fails;
    }
}
