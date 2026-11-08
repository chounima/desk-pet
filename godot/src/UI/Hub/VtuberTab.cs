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
    HSlider? threshold;
    Label? thresholdText;

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

        // 角度（面向）：0 正面、90 側面、180 背面、270 左側；角色有那個視角的圖才換得過去
        body.AddChild(Ui.Heading(I18n.T("vtuber.view")));
        var angle = new HSlider { MinValue = 0, MaxValue = 359, Step = 1, Value = v.ViewYaw, SizeFlagsHorizontal = SizeFlags.ExpandFill,
                                  FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0, Ui.Px(24)) };
        var angleText = Ui.Text($"{v.ViewYaw:0}°", Ui.Accent, 13);
        angle.ValueChanged += a => { feature.SetViewYaw((float)a); angleText.Text = $"{a:0}°"; };
        var presets = new HFlowContainer();
        foreach (var (key, yaw) in new[] { ("vtuber.front", 0f), ("vtuber.side", 90f), ("vtuber.back", 180f), ("vtuber.left", 270f) })
            presets.AddChild(Ui.Button(I18n.T(key), () => angle.Value = yaw));
        body.AddChild(presets);
        body.AddChild(Ui.Row(angle, angleText));
        body.AddChild(Check("vtuber.autoTurn", v.AutoTurn, feature.SetAutoTurn));
        body.AddChild(Ui.Text(I18n.T("vtuber.viewHint"), Ui.Muted, 13, wrap: true));

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
        // 講話門檻：音量超過才算在講話（預設自動學；拉了就固定，按重新校正回到自動）
        body.AddChild(Ui.Text(I18n.T("vtuber.threshold"), null, 13, wrap: true));
        threshold = new HSlider { MinValue = -70, MaxValue = -10, Step = 1, Value = Math.Round(feature.Threshold), SizeFlagsHorizontal = SizeFlags.ExpandFill,
                                  FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0, Ui.Px(24)) };
        thresholdText = Ui.Text("", Ui.Accent, 13);
        threshold.ValueChanged += db => feature.SetThreshold(db);
        body.AddChild(Ui.Row(threshold, thresholdText));
        body.AddChild(Ui.Row(Ui.Button(I18n.T("vtuber.recalibrate"), feature.Recalibrate)));
        body.AddChild(Ui.Text(I18n.T("vtuber.micHint"), Ui.Muted, 13, wrap: true));
        body.AddChild(Check("vtuber.micWhenOff", v.MicWhenOff, feature.SetMicWhenOff));

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
        if (!feature.Active && !feature.MicRunning)
        {
            meter.Text = I18n.T("vtuber.off");
            level.Value = 0;
            return;
        }
        var f = feature.Frame;
        var cal = feature.Calibration;
        if (threshold != null && IsInstanceValid(threshold) && thresholdText != null)
        {
            // 自動時拉桿跟著學到的值走（不觸發 ValueChanged，免得變成手動）
            if (!cal.Manual) threshold.SetValueNoSignal(Math.Round(feature.Threshold));
            thresholdText.Text = I18n.T(cal.Manual ? "vtuber.thresholdManual" : "vtuber.thresholdAuto", $"{feature.Threshold:0}");
        }
        level.Value = Mathf.Clamp((float)(f.Db - cal.Floor) / 40, 0, 1);
        var st = Core.Voice.VoiceAnalyzer.Semitones(f.Pitch, cal.BasePitch);
        meter.Text = I18n.T("vtuber.meter", $"{f.Db:0}", $"{cal.Floor:0}", f.Speaking ? "🎤" : "·",
                            f.Pitch > 0 ? $"{f.Pitch:0} Hz ({st:+0.0;-0.0})" : "-", f.Vowel ?? "-", feature.LastRule ?? "-");
    }
}
