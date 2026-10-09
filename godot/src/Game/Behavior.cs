// 行為邏輯：自動選動作、走路移動、打字反應、睡覺、被拖曳與掉落（移植自 preview/js/game/behavior.js）
using System;
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
    string? tempFace;              // 暫時的表情（例如連按 Backspace 時害羞）
    double tempFaceUntil;
    float timer;
    float? target;                 // 走路的目標 x
    float vy;
    float grabOffset;
    double lastInput = Now();
    double typingUntil;
    readonly System.Collections.Generic.List<double> taps = new();

    public bool Auto { get; set; } = true;
    public State Current { get; private set; } = State.Ground;

    /// <summary>想說話時發出台詞的 key（見 data/lines/）</summary>
    public event Action<string>? Said;

    void Say(string key) => Said?.Invoke(key);

    public Behavior(Pet pet, Func<Rect2> area, Interactions interactions)
    {
        this.pet = pet;
        this.area = area;
        this.interactions = interactions;
        Start("idle");
    }

    static double Now() => Time.GetTicksMsec() / 1000.0;
    static float Rand(float a, float b) => a + GD.Randf() * (b - a);
    float Ground => area().End.Y;

    // ---------- 外部事件 ----------

    public void OnKey(GlobalInput.KeyKind kind)
    {
        var t = Now();
        lastInput = t;
        taps.Add(t);
        if (Current != State.Ground) return;
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
        if (Current != State.Ground) return;
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
        vy = 0;
        pet.Play("fall");
    }

    public void Force(string name)
    {
        lastInput = Now();
        if (Current != State.Ground) return;
        if (name == "typing") typingUntil = Auto ? Now() + 5 : double.MaxValue;
        Start(name, Auto ? null : float.PositiveInfinity);
    }

    // ---------- 每幀 ----------

    public void Update(float dt)
    {
        UpdateState(dt);
        if (Now() < tempFaceUntil) pet.FaceOverride = tempFace;
    }

    void UpdateState(float dt)
    {
        var rect = area();
        if (Current == State.Held) return;
        if (Current == State.Air)
        {
            vy += Tuning.Gravity * DesktopWindow.UiScale * dt;
            pet.Y += vy * dt;
            if (pet.Y >= Ground)
            {
                pet.Y = Ground;
                Current = State.Ground;
                Start("idle");
                Say("land");
            }
            return;
        }
        pet.Y = Ground;
        var edge = 40 * DesktopWindow.UiScale;
        pet.X = Math.Clamp(pet.X, rect.Position.X + edge, rect.End.X - edge);

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
        pet.Play(name, true);
        var clip = pet.Animator.Clip;
        var range = Tuning.Auto.TryGetValue(name, out var a) && a.Time != null ? a.Time : new[] { 3f, 5f };
        timer = duration ?? (clip.Loop ? Rand(range[0], range[1]) : float.PositiveInfinity);
        target = clip.Move > 0 ? PickTarget() : null;
    }

    float PickTarget()
    {
        var rect = area();
        var margin = Tuning.Margin * DesktopWindow.UiScale;
        var lo = rect.Position.X + margin;
        var hi = rect.End.X - margin;
        float x;
        var tries = 0;
        do x = Rand(lo, hi);
        while (Math.Abs(x - pet.X) < 150 * DesktopWindow.UiScale && rect.Size.X > 400 * DesktopWindow.UiScale && ++tries < 20);
        return x;
    }

    static string WeightedPick()
    {
        var total = Tuning.Auto.Values.Sum(v => v.Weight);
        var r = GD.Randf() * total;
        foreach (var (name, v) in Tuning.Auto)
            if ((r -= v.Weight) <= 0) return name;
        return "idle";
    }
}
