// 排球對打小遊戲：寵物 vs 滑鼠游標（移植自 preview/js/game/volley.js）
//
// 玩家：游標快速揮過球 → 依揮動速度擊球；點球 → 自動吊回寵物那邊；
//       游標碰到高速球（防守殺球）→ 自動接起
// 寵物：每顆來球決定一次打法 —— 能殺就三步助跑殺球、來得及就低手接球、來不及就魚躍救球
// 連續來回 10 次以上，球會燒起來
using System;
using System.Collections.Generic;
using System.Linq;
using DeskPet.Platform;
using Godot;

namespace DeskPet.Game.Minigames;

public sealed class Volley
{
    const string Hand = "handNear";               // 擊球點：近側手掌中間
    const float HandY = 12;
    static readonly HashSet<string> Actions = new() { "vbBump", "vbApproach", "vbSpike", "vbDive" };

    sealed class Plan
    {
        public string Type = "";
        public int Aim, Dir;
        public float X0, X1, Xs, From, Slide, Jump;
        public bool Approach, Jumped, Dived, Hit;
        public double StartAt, ContactAt;
        public Vector2 Hand;                       // 擊球瞬間手相對腳底的位移
    }

    readonly Pet pet;
    readonly Func<Rect2> area;

    VolleyBall ball = new();
    string state = "over";
    string? lastHitter;
    int rally, best;
    double incomingSince, lastPlayerHit, restartAt;
    bool petWillMiss, decided, onFire;
    float missOffset;
    Plan? plan;
    (string anim, string line, double at)? pending;

    // 游標：每幀由 Main 更新（全域位置，視窗穿透時也收得到）
    Vector2 cursor, cursorV;
    double cursorMovedAt;
    bool cursorSeen;

    public bool Active { get; private set; }
    public VolleyBall Ball => ball;
    public bool OnFire => onFire;
    public int Rally => rally;
    public int Best => best;
    public string SpikeWhy { get; private set; } = "";
    public int Spikes { get; private set; }
    public int Dives { get; private set; }

    public event Action<string>? Said;                  // 台詞（顯示在對話泡泡）
    public event Action<string, Vector2>? Popped;       // 飄字（Nice!、連發…）
    public event Action<int, int>? Scored;              // 來回次數、最佳紀錄
    public event Action? Shook;                         // 殺球時畫面震動
    public event Action<string, float>? Sounded;        // 音效（名稱、音高）

    public Volley(Pet pet, Func<Rect2> area)
    {
        this.pet = pet;
        this.area = area;
    }

    static double Now() => Time.GetTicksMsec() / 1000.0;
    static float U => DesktopWindow.UiScale;
    float Width => area().Size.X;
    float Ground => area().End.Y;
    VolleySkill Skill => VolleySkills.For(pet.Rig.Id);

    // ---------- 開始／結束 ----------

    public void Start()
    {
        Active = true;
        Serve();
    }

    public void Stop()
    {
        Active = false;
        onFire = false;
        pet.FaceOverride = null;
    }

    // 寵物先面向游標，在擊球點上方把球往上拋，落下時自己接
    void Serve()
    {
        pet.Facing = AimDir();
        var hp = HitPoint();
        ball = new VolleyBall { X = hp.X, Y = hp.Y - 20 * U, Vy = -VolleyConfig.ServeToss };
        state = "play";
        rally = 0;
        lastHitter = null;
        incomingSince = Now();
        petWillMiss = decided = onFire = false;
        missOffset = 0;
        plan = null;
        pending = null;
        pet.Play("vbReady");
        Scored?.Invoke(rally, best);
    }

    // ---------- 玩家輸入 ----------

    /// <summary>每幀呼叫：更新游標位置與（平滑過的）速度</summary>
    public void OnCursor(Vector2 p, float dt)
    {
        if (cursorSeen && dt > 0 && p.DistanceSquaredTo(cursor) > 0.01f)
        {
            cursorV = cursorV * 0.5f + (p - cursor) / dt * 0.5f;
            cursorMovedAt = Now();
        }
        cursor = p;
        cursorSeen = true;
    }

    // 點球：自動把球吊向寵物
    public void OnBallClick()
    {
        if (state != "play") return;
        LobToPet();
        PlayerHit("Nice!");
    }

    void CheckCursorHit()
    {
        if (!cursorSeen || Now() - lastPlayerHit < 0.25) return;
        if (ball.Position.DistanceTo(cursor) > ball.R + VolleyConfig.CursorRadius) return;

        // 游標停下來時速度視為 0，否則靜止的游標會一直「揮擊」
        var moving = Now() - cursorMovedAt < 0.05;
        if (moving && cursorV.Length() >= VolleyConfig.MinSwipe)
        {
            ball.Vx = Math.Clamp(cursorV.X * 0.8f, -VolleyConfig.MaxVx, VolleyConfig.MaxVx);
            ball.Vy = Math.Clamp(cursorV.Y * 0.8f - 300 * U, -1400 * U, 1000 * U);
            PlayerHit(rally + 1 >= 5 ? $"{rally + 1}!" : "Nice!");
        }
        else if (lastHitter == "pet" && ball.Speed > VolleyConfig.DigSpeed)
        {
            LobToPet();
            PlayerHit("接到了！");
        }
    }

    void LobToPet()
    {
        var hp = HitPoint();
        ball.Launch(hp.X + (GD.Randf() - 0.5f) * 300 * U, hp.Y, VolleyConfig.LobArc);
    }

    /// <summary>測試用：模擬完美玩家，球從寵物那邊落到畫面中段時點球回擊</summary>
    public void BotStep()
    {
        if (state == "play" && lastHitter == "pet" && ball.Vy > 0 && ball.Y > Ground * 0.45f) OnBallClick();
    }

    // ---------- 每幀 ----------

    public void Update(float dt)
    {
        if (!Active) return;
        if (ball.Step(dt, Width, Ground) && state == "play")
        {
            Sounded?.Invoke("thud", 1.2f);
            EndRally();
        }
        if (state == "play")
        {
            CheckCursorHit();
            UpdatePet(dt);
            return;
        }
        if (pending is { } p && Now() >= p.at)
        {
            pet.Play(p.anim, true);
            SayKey(p.line);
            pending = null;
        }
        if (Now() > restartAt) Serve();
    }

    // ---------- 寵物：決定打法 ----------

    void UpdatePet(float dt)
    {
        if (plan != null) { RunPlan(dt); return; }

        var incoming = lastHitter != "pet";
        var busy = IsBusy();
        if (!incoming || Now() - incomingSince < VolleyConfig.ReactDelay)
        {
            if (!busy) pet.Play("vbReady");
            return;
        }
        if (!busy && !decided)
        {
            decided = true;
            plan = MakePlan();
            if (plan != null) { RunPlan(dt); return; }
        }
        ChaseBump(dt, busy);
    }

    bool IsBusy() => Actions.Contains(pet.Action) && !pet.Animator.Finished;

    bool Inside(float x) => x > 40 * U && x < Width - 40 * U;

    // 殺球 > 一般低手接球 > 魚躍；回傳 null 表示用一般接球
    Plan? MakePlan()
    {
        if (lastHitter == null) return null;            // 自己拋的發球只用低手
        var sk = Skill;
        var s = BodyScale();
        var aim = AimDir();
        var speed = VolleyConfig.RunSpeed * sk.Speed;

        // 1. 殺球
        var spike = pet.Rig.Volley?.Spike == false ? null : PlanSpike(aim, speed, s);
        if (spike != null) return spike;

        // 2. 跑得到一般接球的位置 → 不需要特別打法
        if (ball.Predict(pet.Y - HitHeight * s, Width, Ground) is { } pb)
        {
            var standX = Math.Clamp(pb.x - aim * Reach * s, 40 * U, Width - 40 * U);
            if (Math.Abs(standX - pet.X) <= speed * pb.t) return null;
        }

        // 3. 來不及 → 魚躍；就算撲不到也撲（比站著看球落地有戲）
        if (pet.Rig.Volley?.Dive == false) return null;
        var d = pet.Probe("vbDive", VolleyConfig.DiveContact, Hand, 0, HandY);
        if (ball.Predict(pet.Y + d.Y, Width, Ground) is not { } pd || GD.Randf() >= sk.Dive) return null;
        var runT = pd.t - VolleyConfig.DiveContact;
        if (runT <= 0) return null;
        var dir = Math.Sign(pd.x - pet.X) is var sg && sg != 0 ? sg : pet.Facing;
        var slide = VolleyConfig.DiveSlide * s;
        var reach = speed * runT;
        var xs = pet.X + Math.Clamp(pd.x - dir * (d.X + slide) - pet.X, -reach, reach);
        return new Plan
        {
            Type = "dive", Dir = dir, Xs = xs, Hand = d, Slide = slide,
            StartAt = Now() + runT, ContactAt = Now() + pd.t,
        };
    }

    // 沿著預測軌跡找第一個「可以殺」的點。擊球可以發生在揮臂過程中的任一時刻，
    // 起跳高度也可以在 0～100% 之間調整，所以每個時刻都有一段可擊球的高度範圍。
    // 還要：時間夠三步助跑（不夠就原地起跳）、而且跑得到。
    Plan? PlanSpike(int aim, float speed, float s)
    {
        var options = VolleyConfig.SpikeTimes.Select(tc =>
        {
            var full = pet.Probe("vbSpike", tc, Hand, 0, HandY);
            var flat = pet.Probe("vbSpike", tc, Hand, 0, HandY, new() { ["root.y"] = 0 });
            return (tc, dx: full.X, hi: -full.Y, lo: -flat.Y);
        }).ToList();
        var hMax = options.Max(o => o.hi);
        var hMin = options.Min(o => o.lo);

        SpikeWhy = "球不夠高";
        foreach (var p in ball.Trajectory(Width, Ground))
        {
            var h = pet.Y - p.Y;                         // 球離地高度
            if (p.Vy <= 0 || h > hMax) continue;         // 還在上升，或比最高擊球點還高
            if (h < hMin) break;                         // 已經低到殺不到
            foreach (var o in options)                   // 依偏好順序
            {
                if (h < o.lo || h > o.hi) continue;
                var approachLead = VolleyConfig.ApproachTime + o.tc;
                var approach = p.T >= approachLead + 0.05f;
                if (!approach && p.T < o.tc + 0.05f) { SpikeWhy = "時間不夠"; continue; }
                var x1 = p.X - aim * o.dx;                                           // 起跳點
                var x0 = approach ? x1 - aim * VolleyConfig.ApproachDist * s : x1;   // 助跑起點
                if (!Inside(x0) || !Inside(x1)) { SpikeWhy = "太靠邊"; continue; }
                if (Math.Abs(x0 - pet.X) > speed * (p.T - (approach ? approachLead : o.tc))) { SpikeWhy = "跑不到"; continue; }
                if (GD.Randf() >= Skill.Spike) { SpikeWhy = "沒選殺球"; return null; }
                var contactAt = Now() + p.T;
                SpikeWhy = approach ? "殺球（助跑）" : "殺球（原地）";
                return new Plan
                {
                    Type = "spike", Aim = aim, X0 = x0, X1 = x1, Approach = approach, ContactAt = contactAt,
                    Jump = (h - o.lo) / Math.Max(1, o.hi - o.lo),
                    StartAt = contactAt - (approach ? approachLead : o.tc),
                    Hand = new Vector2(o.dx, -h),
                };
            }
        }
        return null;
    }

    void RunPlan(float dt)
    {
        var p = plan!;
        var t = Now();
        var speed = VolleyConfig.RunSpeed * Skill.Speed;

        if (p.Type == "spike")
        {
            if (t < p.StartAt) { MoveTo(p.X0, speed, dt, p.Aim); return; }
            var k = p.Approach ? (float)((t - p.StartAt) / VolleyConfig.ApproachTime) : 1;
            if (k < 1)
            {
                // 三步助跑：從目前位置往起跳點前進
                if (pet.Action != "vbApproach")
                {
                    p.From = pet.X;
                    pet.Facing = p.Aim;
                    pet.Play("vbApproach", true);
                }
                pet.X = p.From + (p.X1 - p.From) * k;
                return;
            }
            if (!p.Jumped)
            {
                p.Jumped = true;
                pet.X = p.X1;
                pet.Facing = p.Aim;
                pet.Play("vbSpike", true, new() { ["root.y"] = p.Jump });   // 依球的高度決定跳多高
            }
            if (!p.Hit && Touches(p.Hand, t - p.ContactAt, 1.4f))
            {
                p.Hit = true;
                SpikeHit();
            }
            if (pet.Action == "vbSpike" && pet.Animator.Finished) plan = null;
            return;
        }

        // 魚躍
        if (t < p.StartAt) { MoveTo(p.Xs, speed, dt, p.Dir); return; }
        if (!p.Dived)
        {
            p.Dived = true;
            p.From = pet.X;
            pet.Facing = p.Dir;
            pet.Play("vbDive", true);
            Sounded?.Invoke("whoosh", 1);
        }
        // 撲出的前衝：在碰到球之前滑完
        var kd = Math.Min(1, (float)((t - p.StartAt) / VolleyConfig.DiveContact));
        pet.X = Math.Clamp(p.From + p.Dir * p.Slide * kd, 20 * U, Width - 20 * U);
        if (!p.Hit && Touches(p.Hand, t - p.ContactAt, 1.7f, 1.5f))
        {
            p.Hit = true;
            DiveHit();
        }
        if (pet.Action == "vbDive" && pet.Animator.Finished) plan = null;
    }

    // 擊球時間點附近，球是否碰到手（hand：擊球瞬間手相對腳底的位移）
    bool Touches(Vector2 hand, double dtFromContact, float reachMul, float windowMul = 1)
    {
        if (Math.Abs(dtFromContact) > VolleyConfig.ContactWindow * windowMul) return false;
        var hx = pet.X + pet.Facing * hand.X;
        var hy = pet.Y + hand.Y;
        return ball.Position.DistanceTo(new Vector2(hx, hy)) < ball.R + VolleyConfig.HitTolerance * reachMul;
    }

    void MoveTo(float x, float speed, float dt, int faceAfter)
    {
        var d = x - pet.X;
        if (Math.Abs(d) > 6 * U)
        {
            pet.Facing = Math.Sign(d);
            pet.X += Math.Sign(d) * Math.Min(Math.Abs(d), speed * dt);
            pet.Play("run");
        }
        else
        {
            pet.Facing = faceAfter;
            pet.Play("vbReady");
        }
    }

    // 一般低手接球：跑到落點，等球掉到手臂高度
    void ChaseBump(float dt, bool busy)
    {
        var s = BodyScale();
        var aim = AimDir();
        var landX = ball.Predict(pet.Y - HitHeight * s, Width, Ground) is { } pb ? pb.x : ball.X;
        var standX = Math.Clamp(landX - aim * Reach * s + missOffset, 40 * U, Width - 40 * U);
        if (!busy) MoveTo(standX, VolleyConfig.RunSpeed * Skill.Speed, dt, aim);

        var hp = HitPoint();
        if (petWillMiss || ball.Vy <= 0 || ball.Position.DistanceTo(hp) >= ball.R + VolleyConfig.HitTolerance) return;
        pet.Facing = AimDir();
        pet.Play("vbBump", true);
        Sounded?.Invoke("bump", 1);
        var c = AimTarget();
        ball.Launch(c.X, Math.Min(c.Y, Ground - 120 * U), VolleyConfig.Arc);
        AfterPetHit();
    }

    // ---------- 擊球 ----------

    // 往下扣向游標的位置
    void SpikeHit()
    {
        var c = AimTarget();
        var g = VolleyConfig.Gravity;
        var tx = Math.Clamp(c.X, ball.R, Width - ball.R);
        var ty = Ground - ball.R;
        var dist = new Vector2(tx - ball.X, ty - ball.Y).Length();
        var T = Math.Clamp(dist / (VolleyConfig.SpikeSpeed * Skill.Power), 0.45f, 1f);
        ball.Vx = (tx - ball.X) / T;
        ball.Vy = (ty - ball.Y - 0.5f * g * T * T) / T;
        Spikes++;
        Sounded?.Invoke("spike", 1);
        SayKey("spike");
        Shook?.Invoke();
        AfterPetHit();
    }

    void DiveHit()
    {
        var c = AimTarget();
        ball.Launch(c.X, Math.Min(c.Y, Ground - 120 * U), VolleyConfig.LobArc);
        Dives++;
        Sounded?.Invoke("bump", 0.9f);
        SayKey("dive");
        AfterPetHit();
    }

    void AfterPetHit()
    {
        lastHitter = "pet";
        rally++;
        Scored?.Invoke(rally, best);
        CheckFire();
    }

    void PlayerHit(string text)
    {
        lastHitter = "player";
        lastPlayerHit = incomingSince = Now();
        rally++;
        plan = null;
        decided = false;
        var chance = Math.Min(VolleyConfig.MissMax, (VolleyConfig.MissBase + VolleyConfig.MissPerRally * rally) * Skill.Miss);
        petWillMiss = GD.Randf() < chance;
        // 漏接時故意站偏一點，讓球從手邊擦過，而不是穿過身體
        missOffset = petWillMiss ? (ball.R + VolleyConfig.HitTolerance + 20 * U) * (GD.Randf() < 0.5f ? 1 : -1) : 0;
        Popped?.Invoke(text, ball.Position - new Vector2(0, ball.R));
        Sounded?.Invoke("bump", 1.2f);
        Scored?.Invoke(rally, best);
        CheckFire();
    }

    // ---------- 火焰 ----------

    void CheckFire()
    {
        var on = rally >= VolleyConfig.FireAt;
        if (on && !onFire)
        {
            Popped?.Invoke($"🔥 {VolleyConfig.FireAt} 連發！", ball.Position - new Vector2(0, 40 * U));
            SayKey("fire");
            Sounded?.Invoke("chime", 1.2f);
        }
        onFire = on;
    }

    // ---------- 回合結束 ----------

    void EndRally()
    {
        var petMissed = lastHitter != "pet";
        var diving = plan?.Type == "dive";
        state = "over";
        restartAt = Now() + VolleyConfig.RestartAfter;
        best = Math.Max(best, rally);
        Scored?.Invoke(rally, best);
        // 魚躍或殺球中就等動作做完再反應
        pending = (petMissed ? "shy" : "cheer",
                   petMissed ? (diving ? "diveFail" : "petMiss") : "playerMiss",
                   Now() + (IsBusy() ? 0.8 : 0));
        plan = null;
        onFire = false;
    }

    void SayKey(string key)
    {
        if (Skill.Pick(key) is { } text) Said?.Invoke(text);
    }

    // ---------- 幾何 ----------

    // 寵物要把球打向哪裡：游標；還沒偵測到游標時打向畫面中央
    Vector2 AimTarget() => cursorSeen ? cursor : new Vector2(Width / 2, Ground / 2);

    int AimDir() => Math.Sign(AimTarget().X - pet.X) is var s && s != 0 ? s : pet.Facing;

    float BodyScale() => pet.Scale * pet.Rig.RootScale;

    // 依型態調整的擊球點（例如四足用頭頂球）；沒設定就用 VolleyConfig
    float Reach => pet.Rig.Volley?.Reach ?? VolleyConfig.Reach;
    float HitHeight => pet.Rig.Volley?.HitHeight ?? VolleyConfig.HitHeight;

    // 一般低手接球的擊球點
    Vector2 HitPoint()
    {
        var s = BodyScale();
        return new Vector2(pet.X + pet.Facing * Reach * s, pet.Y - HitHeight * s);
    }
}
