// 半身分頁：開關半身 VTuber 模式、選角色、麥克風與校正、即時音量／音調（方便調門檻）、規則與快捷鍵開關
using System;
using System.Collections.Generic;
using System.Linq;
using DeskPet.Content;
using DeskPet.Features.Vtuber;
using DeskPet.Platform.Voice;
using DeskPet.Save;
using Godot;

namespace DeskPet.UI.Hub;

public partial class VtuberTab : ScrollContainer
{
    readonly Settings settings;
    readonly List<RigData> characters;
    readonly Func<string, bool> unlocked;
    readonly VtuberFeature feature;
    readonly VBoxContainer body = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
    Label? meter;
    ProgressBar? level;

    public VtuberTab(Settings settings, List<RigData> characters, Func<string, bool> unlocked, VtuberFeature feature)
    {
        this.settings = settings;
        this.characters = characters;
        this.unlocked = unlocked;
        this.feature = feature;
        Name = I18n.T("tab.vtuber");
        HorizontalScrollMode = ScrollMode.Disabled;
        AddChild(body);
    }

    CheckBox Check(string key, bool on, Action<bool> toggled)
    {
        var c = new CheckBox { Text = I18n.T(key), ButtonPressed = on, FocusMode = FocusModeEnum.None,
                               AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        c.Toggled += v => toggled(v);
        return c;
    }

    public void Refresh()
    {
        Ui.Clear(body);
        var v = settings.Vtuber;

        body.AddChild(Check("vtuber.enable", feature.Active, on => { feature.SetEnabled(on); Refresh(); }));
        body.AddChild(Ui.Text(I18n.T("vtuber.hint"), Ui.Muted, 13, wrap: true));

        // 角色
        body.AddChild(Ui.Heading(I18n.T("vtuber.character")));
        var pick = new OptionButton { FocusMode = FocusModeEnum.None };
        pick.AddItem(I18n.T("vtuber.sameAsPet"));
        var ids = new List<string> { "" };
        foreach (var c in characters.Where(c => unlocked(c.Id)))
        {
            pick.AddItem(c.Name);
            ids.Add(c.Id);
        }
        pick.Selected = Math.Max(0, ids.IndexOf(v.Character));
        pick.ItemSelected += i => feature.SetCharacter(ids[(int)i]);
        body.AddChild(pick);
        body.AddChild(Ui.Text(I18n.T("vtuber.moveHint"), Ui.Muted, 13, wrap: true));

        // 麥克風
        body.AddChild(Ui.Heading(I18n.T("vtuber.mic")));
        var mics = new OptionButton { FocusMode = FocusModeEnum.None };
        var devices = MicCapture.Devices();
        foreach (var d in devices) mics.AddItem(d == "Default" ? I18n.T("vtuber.micDefault") : d);
        mics.Selected = Math.Max(0, Array.IndexOf(devices, string.IsNullOrEmpty(v.MicDevice) ? "Default" : v.MicDevice));
        mics.ItemSelected += i =>
        {
            v.MicDevice = devices[i] == "Default" ? "" : devices[i];
            settings.Save();
            feature.RestartMic();
        };
        body.AddChild(mics);
        level = Ui.Bar(0);
        meter = Ui.Text("", Ui.Accent, 13, wrap: true);
        body.AddChild(level);
        body.AddChild(meter);
        body.AddChild(Ui.Row(Ui.Button(I18n.T("vtuber.recalibrate"), feature.Recalibrate)));
        body.AddChild(Ui.Text(I18n.T("vtuber.micHint"), Ui.Muted, 13, wrap: true));

        // 規則
        body.AddChild(Ui.Heading(I18n.T("vtuber.reactions")));
        body.AddChild(Check("vtuber.voiceRules", v.VoiceRules, on => { v.VoiceRules = on; settings.Save(); }));
        body.AddChild(Check("vtuber.hotkeys", v.Hotkeys, on => { v.Hotkeys = on; settings.Save(); }));
        body.AddChild(Ui.Text(I18n.T("vtuber.hotkeysHint"), Ui.Muted, 13, wrap: true));

        // 顯示
        body.AddChild(Ui.Heading(I18n.T("vtuber.display")));
        body.AddChild(Check("vtuber.hidePet", v.HideDesktopPet, feature.SetHideDesktopPet));
        body.AddChild(Check("vtuber.fullscreen", v.ShowOnFullscreen, feature.SetShowOnFullscreen));
        Tick();
    }

    /// <summary>每幀更新音量條與數值</summary>
    public void Tick()
    {
        if (meter == null || !IsInstanceValid(meter) || level == null) return;
        if (!feature.Active)
        {
            meter.Text = I18n.T("vtuber.off");
            level.Value = 0;
            return;
        }
        var f = feature.Frame;
        var cal = feature.Calibration;
        level.Value = Mathf.Clamp((float)(f.Db - cal.Floor) / 40, 0, 1);
        var st = Core.Voice.VoiceAnalyzer.Semitones(f.Pitch, cal.BasePitch);
        meter.Text = I18n.T("vtuber.meter", $"{f.Db:0}", $"{cal.Floor:0}", f.Speaking ? "🎤" : "·",
                            f.Pitch > 0 ? $"{f.Pitch:0} Hz ({st:+0.0;-0.0})" : "-", f.Vowel ?? "-", feature.LastRule ?? "-");
    }
}
