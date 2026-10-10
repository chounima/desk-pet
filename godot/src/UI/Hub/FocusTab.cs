// 專注分頁：開始／結束／跳過、目前狀態、專注與休息的分鐘數
using System;
using DeskPet.Game;
using DeskPet.Save;
using Godot;

namespace DeskPet.UI.Hub;

public partial class FocusTab : VBoxContainer
{
    readonly Focus focus;
    readonly Settings settings;
    readonly FocusConfig cfg;

    public event Action? StartRequested;
    public event Action? StopRequested;
    public event Action? SkipRequested;

    public FocusTab(Focus focus, FocusConfig cfg, Settings settings)
    {
        this.focus = focus;
        this.cfg = cfg;
        this.settings = settings;
        Name = "🍅 專注";
    }

    public void Refresh()
    {
        Ui.Clear(this);
        var status = focus.Current switch
        {
            Focus.Phase.Work => $"專注中　{focus.Label()}",
            Focus.Phase.Break => $"休息中　{focus.Label()}",
            _ => "沒有在專注",
        };
        AddChild(Ui.Text(status, Ui.Accent, 18));
        if (focus.Current != Focus.Phase.Off) AddChild(Ui.Text($"這次已完成 {focus.Rounds} 輪", Ui.Muted));
        AddChild(Ui.Row(
            Ui.Button(focus.Current == Focus.Phase.Off ? "▶ 開始專注" : "↺ 重新開始", () => StartRequested?.Invoke()),
            Ui.Button("⏭ 跳到下一段", () => SkipRequested?.Invoke()),
            Ui.Button("⏹ 結束", () => StopRequested?.Invoke())));

        AddChild(Ui.Heading("時間（分鐘）"));
        AddChild(Ui.Row(Ui.Text("專注"), Spin(settings.FocusWork ?? cfg.WorkMinutes, 5, 120, v => { settings.FocusWork = v; focus.WorkMinutes = v; }),
                        Ui.Text("休息"), Spin(settings.FocusBreak ?? cfg.ShortBreakMinutes, 1, 60, v => { settings.FocusBreak = v; focus.BreakMinutes = v; })));
        AddChild(Ui.Text($"每 {cfg.LongBreakEvery} 輪有一次 {cfg.LongBreakMinutes:0} 分鐘的長休息。專注時角色只做安靜的動作、不講話；" +
                         $"完成一輪送 💎 {cfg.Reward.Points}、❤ {cfg.Reward.Affection}。", Ui.Muted, 13, wrap: true));
    }

    SpinBox Spin(float value, float min, float max, Action<float> changed)
    {
        var s = new SpinBox { MinValue = min, MaxValue = max, Step = 1, Value = value, CustomMinimumSize = new Vector2(Ui.Px(110), 0) };
        s.GetLineEdit().AddThemeFontSizeOverride("font_size", Ui.Px(15));
        s.ValueChanged += v => { changed((float)v); settings.Save(); };
        return s;
    }
}
