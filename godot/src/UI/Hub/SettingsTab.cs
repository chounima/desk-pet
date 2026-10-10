// 設定分頁：把系統匣的選項集中在一個地方（兩邊改的是同一份設定，由 Main 同步）
using System;
using DeskPet.Game;
using DeskPet.Save;
using Godot;

namespace DeskPet.UI.Hub;

public partial class SettingsTab : VBoxContainer
{
    /// <summary>(設定名稱, 新的值)：auto、sound、autostart、fullscreenHide、fitScreen、devMode</summary>
    public event Action<string, bool>? Toggled;
    public event Action<float>? SizeChosen;
    /// <summary>選了要顯示的螢幕（-1 = 主螢幕）</summary>
    public event Action<int>? ScreenChosen;

    readonly Settings settings;
    readonly Func<bool> autostart;
    bool refreshing;

    public SettingsTab(Settings settings, Func<bool> autostart)
    {
        this.settings = settings;
        this.autostart = autostart;
        Name = "⚙ 設定";
    }

    public void Refresh()
    {
        refreshing = true;      // 重建時設定勾選狀態不要觸發事件
        Ui.Clear(this);
        AddChild(Ui.Heading("行為"));
        AddChild(Check("自動行為（自己走動、做各種動作）", settings.Auto, "auto"));
        AddChild(Check("🔊 音效", settings.Sound, "sound"));
        AddChild(Check("全螢幕時自動隱藏（玩遊戲、看影片、簡報）", settings.AutoHideFullscreen, "fullscreenHide"));
        AddChild(Check("依正在用的程式反應（只看程式名稱，不讀內容、不存紀錄）", settings.AppAwareness, "appAware"));

        AddChild(Ui.Heading("大小"));
        var size = new OptionButton { FocusMode = FocusModeEnum.None };
        for (var i = 0; i < Tuning.Sizes.Length; i++)
        {
            size.AddItem(Tuning.Sizes[i].Label, i);
            if (Mathf.IsEqualApprox(Tuning.Sizes[i].Value, settings.Size)) size.Select(i);
        }
        size.ItemSelected += i => SizeChosen?.Invoke(Tuning.Sizes[i].Value);
        AddChild(size);
        AddChild(Check("大小跟著螢幕高度（否則跟著 Windows 縮放比例）", settings.FitScreen, "fitScreen"));

        AddChild(Ui.Heading("螢幕"));
        var screens = new OptionButton { FocusMode = FocusModeEnum.None };
        var primary = DisplayServer.GetPrimaryScreen();
        screens.AddItem("主螢幕", 0);
        for (var i = 0; i < DisplayServer.GetScreenCount(); i++)
        {
            var res = DisplayServer.ScreenGetSize(i);
            screens.AddItem($"螢幕 {i + 1}（{res.X}×{res.Y}{(i == primary ? "，主螢幕" : "")}）", i + 1);
        }
        screens.Select(settings.Screen < 0 ? 0 : screens.GetItemIndex(settings.Screen + 1));
        screens.ItemSelected += idx => ScreenChosen?.Invoke(screens.GetItemId((int)idx) - 1);
        AddChild(screens);

        AddChild(Ui.Heading("系統"));
        AddChild(Check("開機時自動啟動", autostart(), "autostart"));
        AddChild(Check("🛠 開發者模式（調整收集系統參數、測試用作弊）", settings.DevMode, "devMode"));
        refreshing = false;
    }

    CheckBox Check(string text, bool on, string key)
    {
        var c = new CheckBox { Text = text, ButtonPressed = on, FocusMode = FocusModeEnum.None };
        c.Toggled += v => { if (!refreshing) Toggled?.Invoke(key, v); };
        return c;
    }
}
