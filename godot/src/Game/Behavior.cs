// 行為邏輯：自動選動作、走路移動、打字反應、睡覺、被拖曳與掉落（移植自 preview/js/game/behavior.js）
using System;
using System.Collections.Generic;
using System.Linq;
using DeskPet.Platform;
using Godot;

namespace DeskPet.Game;

public sealed class Behavior
{
    public enum State { Ground, Held, Air }

    readonly Pet pet;
    readonly Func<Rect2> area;     // 活動範圍（視窗座標），底邊 = 地面
    readonly Interactions interactions;
    readonly Surfaces surfaces;    // 站在地面或哪個視窗上
    string? tempFace;              // 暫時的表情（例如連按 Backspace 時害羞）
    double tempFaceUntil;
    float vx;                      // 空中的水平速度（跳到視窗上用）
    float timer;
    float? target;                 // 走路的目標 x
    float vy;
    float grabOffset;
    double lastInput = Now();
    double typingUntil;
    readonly System.Collections.Generic.List<double> taps = new();

    public bool Auto { get; set; } = true;
    /// <summary>自動行為可以挑的動作（商店的動作買了才可以）</summary>
    public Func<string, bool> Allowed { get; set; } = _ => true;
    /// <summary>自動行為的權重倍率（休息時間、正在用的程式會讓某些動作更常出現）</summary>
    public Func<string, float> Weight { get; set; } = _ => 1;
    public State Current { get; private set; } = State.Ground;

    /// <summary>想說話時發出台詞的 key（見 data/lines/）</summary>
    public event Action<string>? Said;

    public void Say(string key) => Said?.Invoke(key);

    /// <summary>提醒（例如久坐）：說一句話，站在地上又沒在玩小遊戲時順便做一個動作</summary>
    public void Remind(string lineKey, string action)
    {
        Say(lineKey);
        if (Current == State.Ground && !Suspended) Start(action);
    }

    public Behavior(Pet pet, Func<Rect2> area, Interactions interactions, Surfaces surfaces)
    {
        this.pet = pet;
        this.area = area;
        this.interactions = interactions;
        this.surfaces = surfaces;
        Start("idle");
    }

    static double Now() => Time.GetTicksMsec() / 1000.0;
    static float Rand(float a, float b) => a + GD.Randf() * (b - a);
    static float U => DesktopWindow.UiScale;

    /// <summary>除錯：目前站在哪裡</summary>
    public string SurfaceInfo => surfaces.Current is { } p ? $"window(y={p.Y:0},{p.X0:0}~{p.X1:0})" : "ground";

    // ---------- 外部事件 ----------

    public void OnKey(GlobalInput.KeyKind kind)
    {
        var t = Now();
        lastInput = t;
        taps.Add(t);
        if (Current != State.Ground || Suspended) return;
        typingUntil = t + Tuning.TypingHold;
        if (pet.Action != "typing" && pet.Action != "salute")
        {
            if (pet.Action == "sleep") Say("wake");
            else if (GD.Randf() < 0.35f) Say("typingStart");
            Start("typing");
        }

        // 特殊按鍵：Enter 偶爾說話、連按 Backspace 害羞、Ctrl+S 敬禮
        if (interactions.OnKey(kind) is { } rule)
        {
            if (rule.Line != null) Say(rule.Line);
            if (rule.Face != null) TempFace(rule.Face, 1.5f);
            if (rule.Action != null) Start(rule.Action);
        }
    }

    /// <param name="zone">點到的部位：head、face、body、hand、feet、tail</param>
    public void OnPetClick(string zone)
    {
        lastInput = Now();
        if (Current != State.Ground || Suspended) return;
        if (pet.Action == "sleep") Apply(interactions.Busy("sleep"));
        else if (pet.Action == "typing") Apply(interactions.Busy("typing"));
        else Apply(interactions.OnClick(zone));
    }

    void Apply(Content.ReactionRule? rule)
    {
        if (rule == null) return;
        if (rule.Line != null) Say(rule.Line);
        if (rule.Face != null) TempFace(rule.Face, 1.5f);
        var name = rule.Random is { Count: > 0 } list ? list[(int)(GD.Randi() % (uint)list.Count)] : rule.Action;
        if (name == null) return;
        Start(name, rule.Time);
        if (rule.Flee) target = FarSide();
    }

    void TempFace(string face, float seconds)
    {
        tempFace = face;
        tempFaceUntil = Now() + seconds;
    }

    // 跑到離目前位置較遠的那一側
    float FarSide()
    {
        var rect = area();
        var margin = Tuning.Margin * DesktopWindow.UiScale;
        return pet.X < rect.GetCenter().X ? rect.End.X - margin : rect.Position.X + margin;
    }

    public void Grab(Vector2 at)
    {
        Current = State.Held;
        surfaces.Stand(null);
        grabOffset = pet.Y - at.Y;
        pet.FaceOverride = null;
        pet.Play("dragged");
        Say("dragged");
        MoveHeld(at);
    }

    public void MoveHeld(Vector2 at)
    {
        pet.X = at.X;
        pet.Y = at.Y + grabOffset;
    }

    public void Release()
    {
        Current = State.Air;
        vx = vy = 0;
        pet.Play("fall");
    }

    public void Force(string name)
    {
        lastInput = Now();
        if (Current != State.Ground || Suspended) return;     // 小遊戲中不接受指定動作（右鍵選單、商店預覽）
        if (name == "typing") typingUntil = Auto ? Now() + 5 : double.MaxValue;
        Start(name, Auto ? null : float.PositiveInfinity);
    }

    // ---------- 每幀 ----------

    // ---------- 小遊戲 ----------

    // 正在控制角色的小遊戲（排球、彈吉他…）。用名字記，一個結束時另一個還在就不會把控制權交回來
    readonly HashSet<string> minigames = new();

    /// <summary>小遊戲進行中：寵物交給小遊戲控制，這裡只負責站在地面</summary>
    public bool Suspended => minigames.Count > 0;

    public void EnterMinigame(string name)
    {
        minigames.Add(name);
        surfaces.Stand(null);
        Current = State.Ground;
        vx = vy = 0;
        target = null;
        pet.Y = surfaces.GroundY;
        pet.FaceOverride = null;
    }

    public void ExitMinigame(string name)
    {
        if (!minigames.Remove(name) || Suspended) return;      // 還有別的小遊戲在進行
        lastInput = Now();
        Start("idle");
    }

    public void Update(float dt)
    {
        if (Suspended)
        {
            pet.Y = surfaces.GroundY;
            return;
        }
        UpdateState(dt);
        if (Now() < tempFaceUntil) pet.FaceOverride = tempFace;
    }

    void UpdateState(float dt)
    {
        var rect = area();
        if (Current == State.Held) return;
        if (Current == State.Air)
        {
            UpdateAir(dt, rect);
            return;
        }

        // 腳下的視窗移動了就跟著移動；視窗不見了就掉下去
        if (!surfaces.Follow(pet.X, out var carried))
        {
            Current = State.Air;
            vx = vy = 0;
            pet.Play("fall");
            Say("windowGone");
            return;
        }
        pet.X += carried;
        pet.Y = surfaces.Y;
        var (lo, hi) = surfaces.Span;
        var edge = (surfaces.Current == null ? 40 : 20) * U;
        pet.X = Math.Clamp(pet.X, lo + edge, hi - edge);

        if (pet.Action == "typing") { UpdateTyping(); return; }
        pet.FaceOverride = null;

        if (pet.Action == "sleep") return;
        if (Auto && Now() - lastInput > Tuning.SleepAfter)
        {
            Start("sleep", float.PositiveInfinity);
            Say("sleep");
            return;
        }

        if (target != null) { UpdateWalk(dt); return; }
        TurnToCursor(dt);

        timer -= dt;
        if (!pet.Animator.Finished && timer > 0) return;
        if (Auto) Start(WeightedPick());
        else if (pet.Animator.Finished) Start(pet.Animator.Clip.Next ?? "idle", float.PositiveInfinity);
    }

    // 空中：重力、水平移動、落在途中碰到的視窗或地面
    void UpdateAir(float dt, Rect2 rect)
    {
        var prevY = pet.Y;
        vy += Tuning.Gravity * U * dt;
        pet.Y += vy * dt;
        pet.X = Math.Clamp(pet.X + vx * dt, rect.Position.X + 20 * U, rect.End.X - 20 * U);

        WindowPlatform? landing = vy > 0 ? surfaces.LandingBetween(pet.X, prevY, pet.Y) : null;
        if (landing == null && pet.Y < surfaces.GroundY) return;

        surfaces.Stand(landing);
        pet.Y = surfaces.Y;
        var wasLeap = pet.Action == "leap";
        Current = State.Ground;
        vx = vy = 0;
        Start("idle");
        if (!wasLeap) Say("land");
    }

    // ---------- 跳上、跳下視窗 ----------

    // 拋物線跳到 (tx, ty)：頂點比起點和終點都高一點
    void Leap(float tx, float ty)
    {
        var g = Tuning.Gravity * U;
        var apex = Math.Min(pet.Y, ty) - 120 * U;
        vy = -Mathf.Sqrt(2 * g * (pet.Y - apex));
        var t = -vy / g + Mathf.Sqrt(2 * Math.Max(1, ty - apex) / g);
        vx = (tx - pet.X) / t;
        pet.Facing = Math.Sign(tx - pet.X) is 0 ? pet.Facing : Math.Sign(tx - pet.X);
        surfaces.Stand(null);
        Current = State.Air;
        target = null;
        pet.Play("leap", true);
    }

    // 跳到附近比較高的視窗上
    bool TryClimb()
    {
        var candidates = surfaces.All.Where(p =>
            p.Y < pet.Y - 80 * U && pet.Y - p.Y < 1000 * U && p.X1 - p.X0 >= 160 * U &&
            Math.Max(0, Math.Max(p.X0 - pet.X, pet.X - p.X1)) < 900 * U).ToList();
        if (candidates.Count == 0) return false;
        var p = candidates[(int)(GD.Randi() % (uint)candidates.Count)];
        var inset = 60 * U;
        var tx = Math.Clamp(pet.X + Rand(-150, 150) * U, p.X0 + inset, p.X1 - inset);
        Say("climb");
        Leap(tx, p.Y);
        return true;
    }

    // 從視窗上跳下去（往比較近的那一邊）
    bool TryHopDown()
    {
        if (surfaces.Current is not { } p) return false;
        var rect = area();
        var left = pet.X - p.X0 < p.X1 - pet.X;
        var tx = left ? p.X0 - 90 * U : p.X1 + 90 * U;
        tx = Math.Clamp(tx, rect.Position.X + 60 * U, rect.End.X - 60 * U);
        Say("hopDown");
        Leap(tx, surfaces.GroundY);
        return true;
    }

    // 游標在背後一段時間就轉過身來
    float behindTime;

    void TurnToCursor(float dt)
    {
        var u = DesktopWindow.UiScale;
        if (pet.Gaze.Target is not { } t || !Gaze.Lookable.Contains(pet.Action))
        {
            behindTime = 0;
            return;
        }
        var dx = t.X - pet.X;
        var behind = Math.Sign(dx) == -pet.Facing && Math.Abs(dx) > 120 * u && t.DistanceTo(pet.FaceCenter()) < 900 * u;
        behindTime = behind ? behindTime + dt : 0;
        if (behindTime > 0.8f)
        {
            pet.Facing = -pet.Facing;
            behindTime = 0;
        }
    }

    void UpdateTyping()
    {
        var t = Now();
        taps.RemoveAll(k => t - k >= 1);
        pet.FaceOverride = taps.Count >= Tuning.FocusRate ? "focus" : null;
        if (t > typingUntil)
        {
            pet.FaceOverride = null;
            Start("idle");
        }
    }

    void UpdateWalk(float dt)
    {
        var step = pet.Speed * dt;
        var dist = target!.Value - pet.X;
        if (Math.Abs(dist) <= step)
        {
            pet.X = target.Value;
            target = null;
            if (Auto) Start("idle");
            else Start(pet.Action, float.PositiveInfinity);
            return;
        }
        pet.Facing = Math.Sign(dist);
        pet.X += Math.Sign(dist) * step;
    }

    // ---------- 切換動作 ----------

    public void Start(string name, float? duration = null)
    {
        // 跳上／跳下視窗是行為，不是單一動作；做不到（附近沒有視窗）就改成待機
        if (name is "climb" or "hopDown")
        {
            if (!(name == "climb" ? TryClimb() : TryHopDown())) Start("idle", duration);
            return;
        }
        pet.Play(name, true);
        var clip = pet.Animator.Clip;
        var range = Tuning.Auto.TryGetValue(name, out var a) && a.Time != null ? a.Time : new[] { 3f, 5f };
        timer = duration ?? (clip.Loop ? Rand(range[0], range[1]) : float.PositiveInfinity);
        target = clip.Move > 0 ? PickTarget() : null;
    }

    float PickTarget()
    {
        // 在目前站立面的範圍內走（站在視窗上就只在視窗頂邊走）
        var (spanLo, spanHi) = surfaces.Span;
        var margin = (surfaces.Current == null ? Tuning.Margin : 30) * U;
        var lo = spanLo + margin;
        var hi = Math.Max(lo, spanHi - margin);
        float x;
        var tries = 0;
        do x = Rand(lo, hi);
        while (Math.Abs(x - pet.X) < 150 * U && hi - lo > 400 * U && ++tries < 20);
        return x;
    }

    string WeightedPick()
    {
        var table = Tuning.Auto.Where(kv => Allowed(kv.Key)).ToList();
        float W(string name, float w) => w * Weight(name);
        var total = table.Sum(kv => W(kv.Key, kv.Value.Weight));
        var r = GD.Randf() * total;
        foreach (var (name, v) in table)
            if ((r -= W(name, v.Weight)) <= 0) return name;
        return "idle";
    }
}
