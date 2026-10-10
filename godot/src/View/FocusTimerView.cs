// 專注模式的倒數：顯示在角色腳邊（專注中番茄紅、休息中綠色），下面一條進度
using DeskPet.Platform;
using Godot;

namespace DeskPet.View;

public partial class FocusTimerView : Node2D
{
    readonly Label label;
    readonly ColorRect barBg, bar;
    const float BarWidth = 70;

    public FocusTimerView()
    {
        var u = DesktopWindow.UiScale;
        label = new Label
        {
            LabelSettings = new LabelSettings
            {
                Font = Fonts.Ui, FontSize = (int)(17 * u), FontColor = Colors.White,
                OutlineSize = (int)(6 * u), OutlineColor = new Color(0, 0, 0, 0.65f),
            },
        };
        barBg = new ColorRect { Color = new Color(0, 0, 0, 0.45f), Size = new Vector2(BarWidth, 5) * u };
        bar = new ColorRect { Size = new Vector2(BarWidth, 5) * u };
        AddChild(label);
        AddChild(barBg);
        AddChild(bar);
        Visible = false;
    }

    /// <param name="anchor">角色腳底位置</param>
    public void Show(string text, float progress, bool working, Vector2 anchor)
    {
        var u = DesktopWindow.UiScale;
        Visible = true;
        label.Text = text;
        var color = working ? new Color("#ff6b5e") : new Color("#6ee08a");
        label.LabelSettings.FontColor = color;
        bar.Color = color;
        var size = label.GetMinimumSize();
        // 腳邊往上一點，避免被工作列擋住
        Position = anchor + new Vector2(0, -6 * u);
        label.Position = new Vector2(-size.X / 2, -size.Y - 10 * u);
        barBg.Position = new Vector2(-BarWidth * u / 2, -8 * u);
        bar.Position = barBg.Position;
        bar.Size = new Vector2(BarWidth * u * Mathf.Clamp(1 - progress, 0, 1), 5 * u);
    }
}
