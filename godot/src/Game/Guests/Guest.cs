// 客人角色：外部來源（直播聊天室、Discord 語音…）裡的一個人，或手動加進來的角色
//
// 只有動畫與反應：不影響收集進度、不參加排球、專注、彈吉他（那些都是主角色的事）。
// 頭上有名牌，講話時名牌變綠、嘴巴會動；靜音時名牌前面加 🔇。
using System;
using System.Collections.Generic;
using DeskPet.Content;
using DeskPet.Game.Control;
using DeskPet.Platform;
using DeskPet.View;
using Godot;

namespace DeskPet.Game.Guests;

public sealed class Guest
{
    const float LeaveTime = 1.8f;     // 揮手道別後淡出的時間

    public string Key { get; }
    public string Source { get; }
    public string Name { get; private set; }
    public Pet Pet { get; }
    public Behavior Behavior { get; }
    /// <summary>這個客人的動作與表情由誰控制（聊天觸發、講話、離開…）</summary>
    public ControlArbiter Control { get; }
    public bool Speaking { get; private set; }
    public bool Muted { get; private set; }
    /// <summary>最後一次有動靜（講話、聊天）的時間，聊天室客人太久沒動靜就離開</summary>
    public double LastActive { get; private set; } = Now();
    /// <summary>已經說再見、正在淡出</summary>
    public bool Leaving => leaveLeft >= 0;
    /// <summary>淡出完了，可以移除</summary>
    public bool Gone => leaveLeft is >= 0 and <= 0.001f;

    readonly PetAttachments attach;     // 特效、聲波、泡泡、名牌，跟著角色的錨點
    readonly Lines lines;
    readonly Mouth mouth = new();
    float leaveLeft = -1;
    bool hidden;

    public Guest(Node parent, string key, string source, string name, RigData rig,
                 Dictionary<string, Dictionary<string, ClipData>> clips, Dictionary<string, PropData> props,
                 float scale, Func<Rect2> area, Interactions interactions, Lines lines, float x)
    {
        Key = key;
        Source = source;
        Name = name;
        this.lines = lines;
        Pet = new Pet(parent, rig, clips, props, scale);
        Behavior = new Behavior(Pet, area, interactions, new Surfaces(area, () => WindowTracker.Platforms));
        Control = new ControlArbiter(Behavior.TryStart, Now);
        attach = new PetAttachments(parent, Pet, name);
        Behavior.Said += key2 => { if (!attach.Bubble.Visible) attach.Bubble.Say(lines.Pick(Pet.Rig.Id, key2)); };
        var u = DesktopWindow.UiScale;

        // 從畫面上方掉下來登場
        Pet.X = x;
        Pet.Y = area().Position.Y + 40 * u;
        Pet.Facing = GD.Randf() < 0.5f ? 1 : -1;
        Behavior.Release();
    }

    static double Now() => Time.GetTicksMsec() / 1000.0;

    public void Rename(string name)
    {
        if (string.IsNullOrEmpty(name) || name == Name) return;
        Name = name;
        attach.Tag!.SetName(name);
    }

    public void Touch()
    {
        LastActive = Now();
        Behavior.Touch();
    }

    public void SetSpeaking(bool on)
    {
        if (on == Speaking) return;
        Speaking = on;
        if (on)
        {
            Touch();
            if (Pet.Action == "sleep") Behavior.Force("stretch");     // 睡著的人一講話就醒來
        }
        attach.Tag!.SetMuted(Muted);
    }

    public void SetMuted(bool on)
    {
        Muted = on;
        if (on) SetSpeaking(false);
        attach.Tag!.SetMuted(Muted);
    }

    /// <summary>聊天訊息：顯示在泡泡裡，關鍵字對應的動作會做出來</summary>
    /// <summary>聊天訊息：顯示在泡泡裡，符合的規則（關鍵字）照做</summary>
    public void Chat(string text, int maxChars, Rules.Rule? rule)
    {
        Touch();
        var shown = text.Length > maxChars ? text[..maxChars] + "…" : text;
        attach.Bubble.Say(shown, Math.Clamp(2.5f + shown.Length * 0.08f, 3, 7));
        if (rule != null) Apply(rule, sayLine: false);       // 泡泡已經在顯示聊天內容，不再說台詞
    }

    public void React(string emoji, Rules.Rule? rule)
    {
        Touch();
        attach.Fx.Burst(emoji, Pet.HeadTop(), Colors.White, 30);
        if (rule != null) Apply(rule);
    }

    /// <summary>照規則的 do 做：動作、表情（維持 Seconds 秒）、台詞、特效</summary>
    public void Apply(Rules.Rule rule, bool sayLine = true)
    {
        if (Leaving) return;
        var d = rule.Do;
        if (d.Action != null) Do(d.Action, rule.Priority);
        if (d.Face != null) Control.Request("rule", Channel.Face, d.Face, rule.Priority, d.Seconds);
        if (d.Line != null && sayLine) SayLine(d.Line);
        if (d.Fx != null) attach.Fx.Burst(d.Fx, Pet.HeadTop(), Colors.White, 30);
    }

    /// <summary>做某個動作；這個體型沒有就改成跳一下</summary>
    public void Do(string action, int priority = Priority.Reaction)
    {
        if (Leaving) return;
        Control.TryAct("chat", Pet.Clips.ContainsKey(action) || action is "climb" or "hopDown" ? action : "jump", priority, 2.5f);
    }

    public void Say(string text, float seconds = 2.8f) => attach.Bubble.Say(text, seconds);

    public void SayLine(string key) => attach.Bubble.Say(lines.Pick(Pet.Rig.Id, key));

    /// <summary>揮手說再見，淡出後 Gone = true</summary>
    public void Leave()
    {
        if (Leaving) return;
        SetSpeaking(false);
        leaveLeft = LeaveTime;
        if (Behavior.Current == Behavior.State.Ground) Control.TryAct("leave", Pet.Clips.ContainsKey("wave") ? "wave" : "jump", Priority.Minigame, LeaveTime);
        SayLine("guestBye");
    }

    public void SetHidden(bool on)
    {
        hidden = on;
        Pet.View.Visible = !on;
        attach.SetHidden(on);
    }

    // 換大小會重建畫面，要重新套用隱藏狀態
    public void SetScale(float scale)
    {
        Pet.SetScale(scale);
        SetHidden(hidden);
    }

    public void Update(float dt, Vector2 cursor)
    {
        Pet.Gaze.Target = cursor;
        Behavior.Update(dt);
        var talking = UpdateMouth(dt);
        Pet.Update(dt);
        attach.Tag!.SetSpeaking(Speaking, talking);
        attach.Update(dt, talking, mouth.Open);

        if (Leaving)
        {
            leaveLeft = Mathf.Max(0.001f, leaveLeft - dt);
            var a = Mathf.Clamp(leaveLeft / (LeaveTime * 0.5f), 0, 1);
            Pet.View.Modulate = new Color(1, 1, 1, a);
            attach.SetAlpha(a);
        }
    }

    // 講話中：動嘴（嘴型經過仲裁），回傳這一幀有沒有在動嘴
    bool UpdateMouth(float dt)
    {
        var talking = mouth.Update(dt, Behavior, Control, Speaking && !Muted && !hidden);
        Pet.ApplyControl(Control);
        Control.EndFrame();
        return talking;
    }

    public void Free()
    {
        Pet.View.QueueFree();
        attach.Free();
    }

    public bool HitTest(Vector2 p) => !hidden && !Leaving && Pet.View.HitTest(p);
}
