// 專注分頁：開始／結束／跳過、目前狀態、專注與休息的分鐘數
using DeskPet.Content;
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
        Name = I18n.T("tab.focus");
    }

    public void Refresh()
    {
        Ui.Clear(this);
        var status = focus.Current switch
        {
            Focus.Phase.Work => I18n.T("focus.working", focus.Label()),
            Focus.Phase.Break => I18n.T("focus.resting", focus.Label()),
            _ => I18n.T("focus.off"),
        };
        AddChild(Ui.Text(status, Ui.Accent, 18));
        if (focus.Current != Focus.Phase.Off) AddChild(Ui.Text(I18n.T("focus.rounds", focus.Rounds), Ui.Muted));
        AddChild(Ui.Row(
            Ui.Button(focus.Current == Focus.Phase.Off ? I18n.T("focus.start") : I18n.T("focus.restart"), () => StartRequested?.Invoke()),
            Ui.Button(I18n.T("focus.skip"), () => SkipRequested?.Invoke()),
            Ui.Button(I18n.T("focus.stop"), () => StopRequested?.Invoke())));

        AddChild(Ui.Heading(I18n.T("focus.minutes")));
        AddChild(Ui.Row(Ui.Text(I18n.T("focus.work")), Spin(settings.FocusWork ?? cfg.WorkMinutes, 5, 120, v => { settings.FocusWork = v; focus.WorkMinutes = v; }),
                        Ui.Text(I18n.T("focus.break")), Spin(settings.FocusBreak ?? cfg.ShortBreakMinutes, 1, 60, v => { settings.FocusBreak = v; focus.BreakMinutes = v; })));
        AddChild(Ui.Text(I18n.T("focus.note", cfg.LongBreakEvery, cfg.LongBreakMinutes, cfg.Reward.Points, cfg.Reward.Affection), Ui.Muted, 13, wrap: true));
    }

    SpinBox Spin(float value, float min, float max, Action<float> changed)
    {
        var s = new SpinBox { MinValue = min, MaxValue = max, Step = 1, Value = value, CustomMinimumSize = new Vector2(Ui.Px(110), 0) };
        s.GetLineEdit().AddThemeFontSizeOverride("font_size", Ui.Px(15));
        s.ValueChanged += v => { changed((float)v); settings.Save(); };
        return s;
    }
}
