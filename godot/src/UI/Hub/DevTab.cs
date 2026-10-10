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
        Name = "🛠 開發者";
        status = Ui.Text("", Ui.Muted, 13);
        status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        status.ClipText = true;

        var cheats = new HFlowContainer();
        cheats.AddChild(Ui.Button("💎 +100", () => { progress.AddPoints(100, "dev"); Done("已加 100 點"); }));
        cheats.AddChild(Ui.Button("❤ +50（目前角色）", () => { progress.AddXp(currentChar(), 50); Done($"{currentChar()} 好感度 +50"); }));
        cheats.AddChild(Ui.Button("⏭ 隔天", () => { progress.SimulateNextDay(); Done("已模擬換日"); }));
        cheats.AddChild(Ui.Button("🗑 重置進度", () => { progress.Reset(); Done("已清除所有進度"); }));
        AddChild(cheats);

        AddChild(Ui.Heading("參數（data/progression.json）"));
        editor.AddThemeFontSizeOverride("font_size", Ui.Px(13));
        AddChild(editor);
        AddChild(Ui.Row(
            Ui.Button("套用", Apply),
            Ui.Button("還原預設", () =>
            {
                ProgressionConfig.ClearOverride();
                progress.Cfg = ProgressionConfig.Load();
                editor.Text = ProgressionConfig.DefaultText();
                Done("已還原成 data/progression.json");
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
            Done("已套用（存在 user://progression.override.json）");
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            status.Text = $"JSON 格式錯誤：{e.Message}";
        }
    }

    void Done(string message)
    {
        progress.Commit();
        status.Text = message;
        Changed?.Invoke();
    }
}
