// 設定分頁：把系統匣的選項集中在一個地方（兩邊改的是同一份設定，由 Main 同步）
using DeskPet.Content;
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
    /// <summary>選了介面語言（auto、zh-TW、en、ja），要重新啟動才會生效</summary>
    public event Action<string>? LanguageChosen;

    readonly Settings settings;
    readonly Func<bool> autostart;
    bool refreshing;

    public SettingsTab(Settings settings, Func<bool> autostart)
    {
        this.settings = settings;
        this.autostart = autostart;
        Name = I18n.T("tab.settings");
    }

    public void Refresh()
    {
        refreshing = true;      // 重建時設定勾選狀態不要觸發事件
        Ui.Clear(this);
        AddChild(Ui.Heading(I18n.T("settings.behavior")));
        AddChild(Check(I18n.T("settings.auto"), settings.Auto, "auto"));
        AddChild(Check(I18n.T("settings.sound"), settings.Sound, "sound"));
        AddChild(Check(I18n.T("settings.fullscreenHide"), settings.AutoHideFullscreen, "fullscreenHide"));
        AddChild(Check(I18n.T("settings.appAware"), settings.AppAwareness, "appAware"));

        AddChild(Ui.Heading(I18n.T("settings.size")));
        var size = new OptionButton { FocusMode = FocusModeEnum.None };
        for (var i = 0; i < Tuning.Sizes.Length; i++)
        {
            size.AddItem(I18n.T(Tuning.Sizes[i].Label), i);
            if (Mathf.IsEqualApprox(Tuning.Sizes[i].Value, settings.Size)) size.Select(i);
        }
        size.ItemSelected += i => SizeChosen?.Invoke(Tuning.Sizes[i].Value);
        AddChild(size);
        AddChild(Check(I18n.T("settings.fitScreen"), settings.FitScreen, "fitScreen"));

        AddChild(Ui.Heading(I18n.T("settings.screen")));
        var screens = new OptionButton { FocusMode = FocusModeEnum.None };
        var primary = DisplayServer.GetPrimaryScreen();
        screens.AddItem(I18n.T("settings.primary"), 0);
        for (var i = 0; i < DisplayServer.GetScreenCount(); i++)
        {
            var res = DisplayServer.ScreenGetSize(i);
            screens.AddItem(I18n.T(i == primary ? "settings.screenPrimary" : "settings.screenN", i + 1, res.X, res.Y), i + 1);
        }
        screens.Select(settings.Screen < 0 ? 0 : screens.GetItemIndex(settings.Screen + 1));
        screens.ItemSelected += idx => ScreenChosen?.Invoke(screens.GetItemId((int)idx) - 1);
        AddChild(screens);

        AddChild(Ui.Heading(I18n.T("settings.language")));
        var langs = new OptionButton { FocusMode = FocusModeEnum.None };
        var codes = new System.Collections.Generic.List<string> { "auto" };
        langs.AddItem(I18n.T("settings.languageAuto"), 0);
        foreach (var (code, label) in I18n.Languages)
        {
            langs.AddItem(label, codes.Count);
            codes.Add(code);
        }
        langs.Select(Math.Max(0, codes.IndexOf(settings.Language)));
        langs.ItemSelected += i => LanguageChosen?.Invoke(codes[(int)i]);
        AddChild(langs);
        AddChild(Ui.Text(I18n.T("settings.languageNote"), Ui.Muted, 13, wrap: true));

        AddChild(Ui.Heading(I18n.T("settings.system")));
        AddChild(Check(I18n.T("settings.autostart"), autostart(), "autostart"));
        AddChild(Check(I18n.T("settings.devMode"), settings.DevMode, "devMode"));
        refreshing = false;
    }

    CheckBox Check(string text, bool on, string key)
    {
        // 長文字（例如英文）要換行，不然會超出視窗
        var c = new CheckBox { Text = text, ButtonPressed = on, FocusMode = FocusModeEnum.None,
                               AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        c.Toggled += v => { if (!refreshing) Toggled?.Invoke(key, v); };
        return c;
    }
}
