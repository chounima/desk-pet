// 客人角色：外部來源（直播聊天室、Discord 語音…）裡的一個人，或手動加進來的角色
//
// 只有動畫與反應：不影響收集進度、不參加排球、專注、彈吉他（那些都是主角色的事）。
// 頭上有名牌，講話時名牌變綠、嘴巴會動；靜音時名牌前面加 🔇。
using System;
using System.Collections.Generic;
using DeskPet.Content;
using DeskPet.Platform;
using DeskPet.View;
using Godot;

namespace DeskPet.Game.Guests;

public sealed class Guest
{
    const float LeaveTime = 1.8f;     // 揮手道別後淡出的時間
    static readonly Color TagColor = new("#2b2d3a"), TagSpeaking = new("#23a55a");

    public string Key { get; }
    public string Source { get; }
    public string Name { get; private set; }
    public Pet Pet { get; }
    public Behavior Behavior { get; }
    public bool Speaking { get; private set; }
    public bool Muted { get; private set; }
    /// <summary>最後一次有動靜（講話、聊天）的時間，聊天室客人太久沒動靜就離開</summary>
    public double LastActive { get; private set; } = Now();
    /// <summary>已經說再見、正在淡出</summary>
    public bool Leaving => leaveLeft >= 0;
    /// <summary>淡出完了，可以移除</summary>
    public bool Gone => leaveLeft is >= 0 and <= 0.001f;

    readonly Bubble bubble;
    readonly Fx fx;
    readonly Label tag;
    readonly PanelContainer tagPanel;
    readonly StyleBoxFlat tagStyle;
    readonly Lines lines;
    readonly Mouth mouth = new();
    readonly SpeechWaves waves = new();
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
        fx = new Fx();
        parent.AddChild(fx);
        parent.AddChild(waves);
        bubble = new Bubble();
        parent.AddChild(bubble);
        Behavior.Said += key2 => { if (!bubble.Visible) bubble.Say(lines.Pick(Pet.Rig.Id, key2)); };

        var u = DesktopWindow.UiScale;
        tagStyle = new StyleBoxFlat
        {
            BgColor = new Color(1, 1, 1, 0.85f),
            CornerRadiusTopLeft = (int)(8 * u), CornerRadiusTopRight = (int)(8 * u),
            CornerRadiusBottomLeft = (int)(8 * u), CornerRadiusBottomRight = (int)(8 * u),
            ContentMarginLeft = 6 * u, ContentMarginRight = 6 * u, ContentMarginTop = 1 * u, ContentMarginBottom = 1 * u,
            BorderColor = TagSpeaking,
        };
        tagPanel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        tagPanel.AddThemeStyleboxOverride("panel", tagStyle);
        tag = new Label
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            LabelSettings = new LabelSettings { Font = Fonts.Ui, FontSize = (int)(12 * u), FontColor = TagColor },
        };
        tagPanel.AddChild(tag);
        parent.AddChild(tagPanel);
        RefreshTag();

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
        RefreshTag();
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
        RefreshTag();
    }

    public void SetMuted(bool on)
    {
        Muted = on;
        if (on) SetSpeaking(false);
        RefreshTag();
    }

    /// <summary>聊天訊息：顯示在泡泡裡，關鍵字對應的動作會做出來</summary>
    public void Chat(string text, int maxChars, string? action)
    {
        Touch();
        var shown = text.Length > maxChars ? text[..maxChars] + "…" : text;
        bubble.Say(shown, Math.Clamp(2.5f + shown.Length * 0.08f, 3, 7));
        if (action != null) Do(action);
    }

    public void React(string emoji, string? action)
    {
        Touch();
        fx.Burst(emoji, Pet.HeadTop(), Colors.White, 30);
        if (action != null) Do(action);
    }

    /// <summary>做某個動作；這個體型沒有就改成跳一下</summary>
    public void Do(string action)
    {
        if (Leaving) return;
        Behavior.Force(Pet.Clips.ContainsKey(action) || action is "climb" or "hopDown" ? action : "jump");
    }

    public void Say(string text, float seconds = 2.8f) => bubble.Say(text, seconds);

    public void SayLine(string key) => bubble.Say(lines.Pick(Pet.Rig.Id, key));

    /// <summary>揮手說再見，淡出後 Gone = true</summary>
    public void Leave()
    {
        if (Leaving) return;
        SetSpeaking(false);
        leaveLeft = LeaveTime;
        if (Behavior.Current == Behavior.State.Ground) Behavior.Force(Pet.Clips.ContainsKey("wave") ? "wave" : "jump");
        SayLine("guestBye");
    }

    public void SetHidden(bool on)
    {
        hidden = on;
        Pet.View.Visible = fx.Visible = tagPanel.Visible = !on;
        bubble.Muted = on;
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
        UpdateMouth(dt);
        Pet.Update(dt);
        var head = Pet.HeadTop();
        fx.UpdateFx(dt, Pet.Animator.Fx, head);

        // 名牌在頭上，泡泡再往上
        var u = DesktopWindow.UiScale;
        tagPanel.ResetSize();
        tagPanel.Position = head - new Vector2(tagPanel.Size.X / 2, tagPanel.Size.Y + 6 * u);
        bubble.UpdateBubble(dt, head - new Vector2(0, tagPanel.Size.Y + 4 * u));

        if (Leaving)
        {
            leaveLeft = Mathf.Max(0.001f, leaveLeft - dt);
            var a = Mathf.Clamp(leaveLeft / (LeaveTime * 0.5f), 0, 1);
            Pet.View.Modulate = tagPanel.Modulate = new Color(1, 1, 1, a);
        }
    }

    // 講話中：動嘴，名牌加綠框（像 Discord 的講話提示）
    void UpdateMouth(float dt)
    {
        var talking = mouth.Update(dt, Pet, Behavior, Speaking && !Muted && !hidden);
        waves.UpdateWaves(dt, talking, Pet, mouth.Open);
        var w = talking ? (int)Mathf.Max(1, 2 * DesktopWindow.UiScale) : 0;
        if (tagStyle.BorderWidthBottom != w) SetBorder(w);
    }

    void SetBorder(int w)
    {
        tagStyle.BorderWidthLeft = tagStyle.BorderWidthRight = tagStyle.BorderWidthTop = tagStyle.BorderWidthBottom = w;
    }

    void RefreshTag()
    {
        tag.Text = (Muted ? "🔇 " : "") + Name;
        tag.LabelSettings.FontColor = Speaking ? TagSpeaking : TagColor;
    }

    public void Free()
    {
        Pet.View.QueueFree();
        fx.QueueFree();
        waves.QueueFree();
        bubble.QueueFree();
        tagPanel.QueueFree();
    }

    public bool HitTest(Vector2 p) => !hidden && !Leaving && Pet.View.HitTest(p);
}
