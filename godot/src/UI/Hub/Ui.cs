// 收藏／商店／設定視窗共用的樣式與小元件（依螢幕縮放放大字型與間距）
using DeskPet.Platform;
using DeskPet.View;
using Godot;

namespace DeskPet.UI.Hub;

public static class Ui
{
    public static float U => DesktopWindow.UiScale;
    public static int Px(float v) => Mathf.RoundToInt(v * U);

    public static readonly Color Muted = new("#9aa0b4");
    public static readonly Color Accent = new("#ffd75e");
    public static readonly Color Line = new("#3a3f55");

    public static Theme MakeTheme()
    {
        var theme = new Theme { DefaultFont = Fonts.Ui, DefaultFontSize = Px(15) };
        var panel = new StyleBoxFlat { BgColor = new Color("#1e2130") };
        theme.SetStylebox("panel", "PanelContainer", panel);
        theme.SetConstant("separation", "VBoxContainer", Px(6));
        theme.SetConstant("separation", "HBoxContainer", Px(6));
        theme.SetConstant("h_separation", "GridContainer", Px(8));
        theme.SetConstant("v_separation", "GridContainer", Px(8));
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            theme.SetConstant(side, "MarginContainer", Px(12));

        // 內建的勾選框圖示不會跟著螢幕縮放，自己畫一組
        const string box = "<rect x='1.5' y='1.5' width='17' height='17' rx='4' fill='none' stroke='#9aa0b4' stroke-width='2'/>";
        const string on = "<rect x='1' y='1' width='18' height='18' rx='4' fill='#ffd75e'/>" +
                          "<path d='M5 10.5 L8.5 14 L15 6.5' fill='none' stroke='#1e2130' stroke-width='2.6' stroke-linecap='round' stroke-linejoin='round'/>";
        theme.SetIcon("unchecked", "CheckBox", Icon(box));
        theme.SetIcon("checked", "CheckBox", Icon(on));
        theme.SetIcon("unchecked_disabled", "CheckBox", Icon(box));
        theme.SetIcon("checked_disabled", "CheckBox", Icon(on));
        return theme;
    }

    static ImageTexture Icon(string body)
    {
        var image = new Image();
        image.LoadSvgFromString($"<svg xmlns='http://www.w3.org/2000/svg' width='20' height='20' viewBox='0 0 20 20'>{body}</svg>", U);
        return ImageTexture.CreateFromImage(image);
    }

    /// <param name="wrap">自動換行：只能用在直向排列（VBox）裡，放在橫向排列裡寬度會變成 0、一字一行</param>
    public static Label Text(string text, Color? color = null, float size = 15, bool wrap = false)
    {
        var l = new Label { Text = text, AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off };
        if (color is { } c) l.AddThemeColorOverride("font_color", c);
        if (!Mathf.IsEqualApprox(size, 15)) l.AddThemeFontSizeOverride("font_size", Px(size));
        return l;
    }

    public static Label Heading(string text) => Text(text, Muted, 13);

    public static Button Button(string text, System.Action onPressed)
    {
        var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        b.Pressed += onPressed;
        return b;
    }

    /// <summary>細長的進度條</summary>
    public static ProgressBar Bar(float ratio)
    {
        var bar = new ProgressBar
        {
            MinValue = 0, MaxValue = 1, Step = 0, Value = Mathf.Clamp(ratio, 0, 1), ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, Px(8)), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        var bg = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.1f) };
        bg.SetCornerRadiusAll(Px(4));
        var fill = new StyleBoxFlat { BgColor = new Color("#ff7d8a") };
        fill.SetCornerRadiusAll(Px(4));
        bar.AddThemeStyleboxOverride("background", bg);
        bar.AddThemeStyleboxOverride("fill", fill);
        return bar;
    }

    /// <summary>有框線的卡片</summary>
    public static (PanelContainer Card, VBoxContainer Body) Card(bool highlight = false, bool dim = false)
    {
        var style = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.03f), BorderColor = highlight ? Accent : Line };
        style.SetBorderWidthAll(Px(highlight ? 2 : 1));
        style.SetCornerRadiusAll(Px(10));
        style.SetContentMarginAll(Px(10));
        var card = new PanelContainer { Modulate = dim ? new Color(1, 1, 1, 0.65f) : Colors.White };
        card.AddThemeStyleboxOverride("panel", style);
        var body = new VBoxContainer();
        card.AddChild(body);
        return (card, body);
    }

    public static HBoxContainer Row(params Control[] children)
    {
        var row = new HBoxContainer();
        foreach (var c in children) row.AddChild(c);
        return row;
    }

    public static void Clear(Node node)
    {
        foreach (var child in node.GetChildren()) child.QueueFree();
    }
}
