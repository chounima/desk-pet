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
        ["hearts"] = (0.5f, () => "♥", new Color("#ff6b8a")),
    };

    /// <summary>停在頭上、會抖動的符號（驚嘆號、生氣）</summary>
    static readonly Dictionary<string, (string text, Color color, Vector2 offset)> Marks = new()
    {
        ["alert"] = ("!", new Color("#ffd75e"), new Vector2(-6, -44)),
        ["anger"] = ("💢", new Color("#ff4d5e"), new Vector2(24, -36)),
    };

    Label? mark;
    string? markKind;
    float timer;
    string heartSymbol = "♥";
    Color heartColor = new("#ff6b8a");

    /// <summary>愛心特效的款式（商店的特效樣式）</summary>
    public void SetHeartStyle(string symbol, Color color)
    {
        heartSymbol = symbol;
        heartColor = color;
    }

    /// <summary>點擊、被摸頭時冒出的愛心（依款式）</summary>
    public void Heart(Vector2 at) => Burst(heartSymbol, at, heartColor);

    /// <param name="kind">目前動作的特效名稱</param>
    /// <param name="anchor">頭頂位置</param>
    public void UpdateFx(float dt, string? kind, Vector2 anchor)
    {
        SetMark(kind != null && Marks.ContainsKey(kind) ? kind : null, anchor);
        if (kind == null || !Emitters.TryGetValue(kind, out var e)) return;
        timer -= dt;
        if (timer > 0) return;
        timer = e.every;
        if (kind == "hearts") Heart(anchor);
        else Burst(e.text(), anchor, e.color);
    }

    void SetMark(string? kind, Vector2 anchor)
    {
        if (kind != markKind)
        {
            mark?.QueueFree();
            mark = null;
            markKind = kind;
            if (kind != null)
            {
                mark = MakeLabel(Marks[kind].text, Marks[kind].color, 30);
                AddChild(mark);
            }
        }
        if (mark != null && kind != null)
        {
            mark.Position = anchor + Marks[kind].offset * Platform.DesktopWindow.UiScale;
            mark.Rotation = Mathf.Sin(Time.GetTicksMsec() / 50f) * 0.2f;
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
            Font = Fonts.Ui,
            FontSize = (int)(size * Platform.DesktopWindow.UiScale), FontColor = color,
            OutlineSize = (int)(6 * Platform.DesktopWindow.UiScale), OutlineColor = new Color(0, 0, 0, 0.55f),
        },
    };
}
