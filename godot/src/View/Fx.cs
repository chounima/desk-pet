// 頭上的特效：Zzz、音符、驚嘆號，以及一次性的飄字（移植自 preview/js/view/fx.js）
using System;
using System.Collections.Generic;
using Godot;

namespace DeskPet.View;

public partial class Fx : Node2D
{
    static readonly Dictionary<string, (float every, Func<string> text, Color color)> Emitters = new()
    {
        ["zzz"] = (0.9f, () => "Z", new Color("#c8d4ff")),
        ["notes"] = (0.45f, () => GD.Randf() < 0.5f ? "♪" : "♫", new Color("#ffd75e")),
    };

    Label? alert;
    float timer;

    /// <param name="kind">目前動作的特效名稱</param>
    /// <param name="anchor">頭頂位置</param>
    public void UpdateFx(float dt, string? kind, Vector2 anchor)
    {
        SetAlert(kind == "alert", anchor);
        if (kind == null || !Emitters.TryGetValue(kind, out var e)) return;
        timer -= dt;
        if (timer > 0) return;
        timer = e.every;
        Burst(e.text(), anchor, e.color);
    }

    void SetAlert(bool on, Vector2 anchor)
    {
        if (on && alert == null)
        {
            alert = MakeLabel("!", new Color("#ffd75e"), 30);
            AddChild(alert);
        }
        else if (!on && alert != null)
        {
            alert.QueueFree();
            alert = null;
        }
        if (alert != null)
        {
            alert.Position = anchor + new Vector2(-6, -44) * Platform.DesktopWindow.UiScale;
            alert.Rotation = Mathf.Sin(Time.GetTicksMsec() / 50f) * 0.2f;
        }
    }

    /// <summary>往上飄並淡出的文字</summary>
    public void Burst(string text, Vector2 at, Color color, int size = 22)
    {
        var u = Platform.DesktopWindow.UiScale;
        var label = MakeLabel(text, color, size);
        var start = at + new Vector2((GD.Randf() - 0.5f) * 40 - 10, -size * 1.2f) * u;
        label.Position = start;
        AddChild(label);
        var tween = CreateTween().SetParallel();
        tween.TweenProperty(label, "position", start + new Vector2(18, -90) * u, 1.6).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "modulate:a", 0f, 1.6);
        tween.Chain().TweenCallback(Callable.From(label.QueueFree));
    }

    // 文字大小依 Windows 顯示縮放放大
    static Label MakeLabel(string text, Color color, int size) => new()
    {
        Text = text,
        LabelSettings = new LabelSettings
        {
            FontSize = (int)(size * Platform.DesktopWindow.UiScale), FontColor = color,
            OutlineSize = (int)(6 * Platform.DesktopWindow.UiScale), OutlineColor = new Color(0, 0, 0, 0.55f),
        },
    };
}
