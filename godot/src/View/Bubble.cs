// 對話泡泡：顯示在頭上，跟著角色移動，幾秒後淡出
using DeskPet.Platform;
using Godot;

namespace DeskPet.View;

public partial class Bubble : Node2D
{
    const float FadeTime = 0.3f;

    readonly PanelContainer panel;
    readonly Label label;
    readonly Polygon2D tail;
    float timeLeft;
    bool muted;

    /// <summary>角色隱藏中：不顯示任何台詞</summary>
    public bool Muted
    {
        get => muted;
        set { muted = value; if (value) Visible = false; }
    }

    public Bubble()
    {
        var u = DesktopWindow.UiScale;
        panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        label = new Label
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            LabelSettings = new LabelSettings { Font = Fonts.Ui, FontSize = (int)(15 * u), FontColor = new Color("#2b2d3a") },
        };
        panel.AddChild(label);

        // 泡泡下方的小尖角
        tail = new Polygon2D
        {
            Color = new Color(1, 1, 1, 0.96f),
            Polygon = new[] { new Vector2(-7, 0) * u, new Vector2(7, 0) * u, new Vector2(0, 10) * u },
        };
        AddChild(tail);
        AddChild(panel);
        Visible = false;
        SetStyle("classic");
    }

    /// <summary>泡泡款式（商店商品），見 BubbleStyles</summary>
    public void SetStyle(string style)
    {
        var (box, text, tailColor, rotation) = BubbleStyles.Make(style, DesktopWindow.UiScale);
        panel.AddThemeStyleboxOverride("panel", box);
        label.LabelSettings.FontColor = text;
        tail.Color = tailColor;
        panel.Rotation = rotation;
    }

    public void Say(string? text, float seconds = 2.8f)
    {
        if (string.IsNullOrEmpty(text) || muted) return;
        label.Text = text;
        panel.ResetSize();
        timeLeft = seconds;
        Modulate = Colors.White;
        Visible = true;
    }

    /// <param name="anchor">頭頂位置，泡泡畫在它上方</param>
    public void UpdateBubble(float dt, Vector2 anchor)
    {
        if (!Visible) return;
        timeLeft -= dt;
        if (timeLeft <= 0)
        {
            Visible = false;
            return;
        }
        if (timeLeft < FadeTime) Modulate = new Color(1, 1, 1, timeLeft / FadeTime);

        var u = DesktopWindow.UiScale;
        var size = panel.Size;
        Position = anchor - new Vector2(0, 14 * u);
        panel.Position = new Vector2(-size.X / 2, -size.Y - 9 * u);
        tail.Position = new Vector2(0, -10 * u);
    }
}
