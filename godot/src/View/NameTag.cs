// 名牌：角色頭上的名字，講話時邊框變綠（像 Discord 的講話提示），靜音時名字前面加 🔇
using DeskPet.Platform;
using Godot;

namespace DeskPet.View;

public partial class NameTag : PanelContainer
{
    static readonly Color TextColor = new("#2b2d3a"), Speaking = new("#23a55a");

    readonly Label label;
    readonly StyleBoxFlat style;
    string name = "";
    bool muted, talking;

    public NameTag(string name)
    {
        var u = DesktopWindow.UiScale;
        MouseFilter = MouseFilterEnum.Ignore;
        style = new StyleBoxFlat
        {
            BgColor = new Color(1, 1, 1, 0.85f), BorderColor = Speaking,
            ContentMarginLeft = 6 * u, ContentMarginRight = 6 * u, ContentMarginTop = 1 * u, ContentMarginBottom = 1 * u,
        };
        style.SetCornerRadiusAll((int)(8 * u));
        AddThemeStyleboxOverride("panel", style);
        label = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            LabelSettings = new LabelSettings { Font = Fonts.Ui, FontSize = (int)(12 * u), FontColor = TextColor },
        };
        AddChild(label);
        this.name = name;
        Refresh();
    }

    public void SetName(string value)
    {
        name = value;
        Refresh();
    }

    public void SetMuted(bool on)
    {
        muted = on;
        Refresh();
    }

    /// <summary>正在講話：字變綠；動嘴的這一刻（talking）加綠框</summary>
    public void SetSpeaking(bool speaking, bool talkingNow)
    {
        label.LabelSettings.FontColor = speaking ? Speaking : TextColor;
        if (talkingNow == talking) return;
        talking = talkingNow;
        style.SetBorderWidthAll(talkingNow ? (int)Mathf.Max(1, 2 * DesktopWindow.UiScale) : 0);
    }

    void Refresh() => label.Text = (muted ? "🔇 " : "") + name;

    /// <summary>放在 anchor 正上方（底邊離 anchor 一點距離）</summary>
    public void Place(Vector2 anchor)
    {
        ResetSize();
        Position = anchor - new Vector2(Size.X / 2, Size.Y + 6 * DesktopWindow.UiScale);
    }
}
