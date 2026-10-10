// 開發者分頁：即時修改 data/progression.json 的參數（存成 user://progression.override.json）、作弊按鈕
using System;
using System.Text.Json;
using DeskPet.Content;
using DeskPet.Game.Progression;
using Godot;

namespace DeskPet.UI.Hub;

public partial class DevTab : VBoxContainer
{
    readonly Progress progress;
    readonly Func<string> currentChar;
    readonly TextEdit editor = new()
    {
        SizeFlagsVertical = SizeFlags.ExpandFill, WrapMode = TextEdit.LineWrappingMode.None,
        CustomMinimumSize = new Vector2(0, 200),
    };
    readonly Label status;

    /// <summary>進度或參數改了，其他畫面要重畫</summary>
    public event Action? Changed;

    public DevTab(Progress progress, Func<string> currentChar)
    {
        this.progress = progress;
        this.currentChar = currentChar;
        Name = I18n.T("tab.dev");
        status = Ui.Text("", Ui.Muted, 13);
        status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        status.ClipText = true;

        var cheats = new HFlowContainer();
        cheats.AddChild(Ui.Button("💎 +100", () => { progress.AddPoints(100, "dev"); Done(I18n.T("dev.addedPoints")); }));
        cheats.AddChild(Ui.Button(I18n.T("dev.addXp"), () => { progress.AddXp(currentChar(), 50); Done(I18n.T("dev.addedXp", currentChar())); }));
        cheats.AddChild(Ui.Button(I18n.T("dev.nextDay"), () => { progress.SimulateNextDay(); Done(I18n.T("dev.nextDayDone")); }));
        cheats.AddChild(Ui.Button(I18n.T("dev.reset"), () => { progress.Reset(); Done(I18n.T("dev.resetDone")); }));
        AddChild(cheats);

        AddChild(Ui.Heading(I18n.T("dev.params")));
        editor.AddThemeFontSizeOverride("font_size", Ui.Px(13));
        AddChild(editor);
        AddChild(Ui.Row(
            Ui.Button(I18n.T("dev.apply"), Apply),
            Ui.Button(I18n.T("dev.restore"), () =>
            {
                ProgressionConfig.ClearOverride();
                progress.Cfg = ProgressionConfig.Load();
                editor.Text = ProgressionConfig.DefaultText();
                Done(I18n.T("dev.restored"));
            }),
            status));
    }

    public void Refresh()
    {
        if (string.IsNullOrEmpty(editor.Text)) editor.Text = ProgressionConfig.CurrentText();
    }

    void Apply()
    {
        try
        {
            progress.Cfg = ProgressionConfig.Parse(editor.Text);
            ProgressionConfig.SaveOverride(editor.Text);
            progress.CheckUnlocks();
            Done(I18n.T("dev.applied"));
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            status.Text = I18n.T("dev.jsonError", e.Message);
        }
    }

    void Done(string message)
    {
        progress.Commit();
        status.Text = message;
        Changed?.Invoke();
    }
}
