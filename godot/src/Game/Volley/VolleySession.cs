// 排球對打的場景：把 Volley（邏輯）接上球的畫面、計分顯示、對話泡泡、飄字與畫面震動
using System;
using DeskPet.Content;
using DeskPet.Platform;
using DeskPet.View;
using Godot;

namespace DeskPet.Game.Minigames;

public partial class VolleySession : Node2D
{
    Volley volley = null!;
    BallView ball = null!;
    Label hud = null!;
    Pet pet = null!;
    Behavior behavior = null!;
    Func<Rect2> area = null!;
    Node2D shakeTarget = null!;
    float shakeLeft;

    /// <summary>測試用：模擬完美玩家自動回球</summary>
    public bool Bot { get; set; }

    public bool Active => volley.Active;

    public void Init(Pet pet, Behavior behavior, Func<Rect2> area, PropData ballProp, Bubble bubble, Fx fx, Audio.Sfx sfx, Node2D shakeTarget)
    {
        this.pet = pet;
        this.behavior = behavior;
        this.area = area;
        this.shakeTarget = shakeTarget;

        volley = new Volley(pet, area);
        ball = new BallView();
        AddChild(ball);
        ball.Init(ballProp, VolleyConfig.BallRadius);

        var u = DesktopWindow.UiScale;
        hud = new Label
        {
            Visible = false,
            LabelSettings = new LabelSettings
            {
                Font = Fonts.Ui, FontSize = (int)(18 * u), FontColor = Colors.White,
                OutlineSize = (int)(6 * u), OutlineColor = new Color(0, 0, 0, 0.6f),
            },
        };
        AddChild(hud);

        volley.Said += text => bubble.Say(text, 2f);
        volley.Popped += (text, at) => fx.Burst(text, at, Colors.White);
        volley.Scored += ShowScore;
        volley.Scored += (rally, _) => Rallied?.Invoke(rally);
        volley.Shook += () => shakeLeft = 0.28f;
        volley.Sounded += (name, pitch) => sfx.Play(name, pitch);
    }

    /// <summary>來回次數變化（收集系統用來給點數）</summary>
    public event Action<int>? Rallied;

    public void SetBallSkin(PropData prop) => ball.SetSkin(prop);

    public void SetActive(bool on)
    {
        if (on == Active) return;
        if (on)
        {
            behavior.EnterMinigame("volley");
            volley.Start();
            hud.Visible = true;
        }
        else
        {
            volley.Stop();
            ball.HideBall();
            hud.Visible = false;
            shakeTarget.Position = Vector2.Zero;
            behavior.ExitMinigame("volley");
        }
    }

    /// <summary>每幀呼叫（在 Behavior.Update 之後、Pet.Update 之前）</summary>
    public void Tick(float dt, Vector2 cursor)
    {
        volley.OnCursor(cursor, dt);
        if (!Active) return;
        if (Bot) volley.BotStep();
        volley.Update(dt);
        ball.Show(volley.Ball, volley.OnFire);
        UpdateShake(dt);
        var u = DesktopWindow.UiScale;
        hud.Position = new Vector2(24 * u, area().End.Y - 56 * u);
    }

    /// <summary>畫面上的點是否點在球上（點擊穿透判斷、點球用）</summary>
    public bool HitBall(Vector2 point) => Active && ball.HitTest(point);

    public void ClickBall() => volley.OnBallClick();

    public string DebugInfo =>
        $"rally={volley.Rally} best={volley.Best} spikes={volley.Spikes} dives={volley.Dives} spikeWhy={volley.SpikeWhy}";

    void ShowScore(int rally, int best)
    {
        var icon = rally >= VolleyConfig.FireAt ? "🔥" : "🏐";
        hud.Text = I18n.T("volley.hud", icon, rally, best, pet.Rig.Name, VolleySkills.For(pet.Rig.Id).Title);
    }

    // 殺球時整個畫面抖一下
    void UpdateShake(float dt)
    {
        if (shakeLeft <= 0) return;
        shakeLeft -= dt;
        var u = DesktopWindow.UiScale;
        shakeTarget.Position = shakeLeft > 0
            ? new Vector2((GD.Randf() - 0.5f) * 10 * u, (GD.Randf() - 0.5f) * 6 * u)
            : Vector2.Zero;
    }
}
