// 主程式的專注與生活部分：番茄鐘（安靜陪伴、休息提醒、完成獎勵）、依正在用的程式反應
using System.Collections.Generic;
using DeskPet.Game;
using DeskPet.Platform;
using DeskPet.View;
using Godot;

namespace DeskPet;

public partial class Main
{
    FocusConfig focusCfg = null!;
    Focus focus = null!;
    FocusTimerView focusView = null!;
    AppAwareness apps = null!;
    float appTimer;
    bool focusWasBreak;

    // 專注中也可以說的台詞（被抓起來、掉落這種當下的反應還是要有）
    static readonly HashSet<string> FocusAllowedLines = new() { "dragged", "land", "windowGone", "back", "hide", "noSongs" };

    void SetupFocus()
    {
        focusCfg = FocusConfig.Load();
        focus = new Focus(focusCfg) { WorkMinutes = settings.FocusWork, BreakMinutes = settings.FocusBreak };
        // --focus-minutes=0.1：測試用，專注與休息都縮短
        if (args.TryGetValue("focus-minutes", out var fm) && float.TryParse(fm, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var m))
            focus.WorkMinutes = focus.BreakMinutes = m;
        focusView = new FocusTimerView();
        AddChild(focusView);

        focus.WorkStarted += () =>
        {
            behavior.Say(focusWasBreak ? "focusBack" : "focusStart");
            focusWasBreak = false;
            SyncFocusMenus();
        };
        focus.WorkCompleted += _ =>
        {
            progress.AddPoints(focusCfg.Reward.Points, "focus");
            progress.AddStat("focusRounds");
            progress.AddXp(pet.Rig.Id, focusCfg.Reward.Affection);
            progress.Commit();
            behavior.Say("focusDone");
            GetTree().CreateTimer(3).Timeout += () => behavior.Remind("focusBreak", "stretch");
            focusWasBreak = true;
            sfx.Play("chime");
        };
        focus.Stopped += SyncFocusMenus;

        // 專注中只做安靜的動作；休息中活潑的動作更常出現；有開程式偵測時依類別加權
        behavior.Allowed = name => shop.ActionOwned(name) && (!focus.Working || focusCfg.CalmActions.Contains(name));
        behavior.Weight = name => apps.Weight(name) * (focus.Current == Focus.Phase.Break && focusCfg.BreakActions.Contains(name) ? 3 : 1);

        apps = new AppAwareness(AppConfig.Load()) { Enabled = settings.AppAwareness };
        apps.Said += key => behavior.Say(key);

        hub.AddFocusTab(focus, focusCfg, settings);
        hub.Focus.StartRequested += StartFocus;
        hub.Focus.StopRequested += StopFocus;
        hub.Focus.SkipRequested += () => { focus.Skip(); hub.RefreshAll(); };
        petMenu.FocusToggled += () => { if (focus.Current == Focus.Phase.Off) StartFocus(); else StopFocus(); };
        tray.FocusToggled += () => { if (focus.Current == Focus.Phase.Off) StartFocus(); else StopFocus(); };
    }

    void StartFocus()
    {
        focusWasBreak = false;
        focus.Start();
        if (hub.Visible) hub.RefreshAll();
    }

    void StopFocus()
    {
        focus.Stop();
        if (hub.Visible) hub.RefreshAll();
    }

    void SyncFocusMenus()
    {
        var on = focus.Current != Focus.Phase.Off;
        petMenu.Focusing = on;
        tray.SetFocusing(on);
    }

    /// <summary>專注中不講一般台詞（Main 的 Said 處理會先問這裡）</summary>
    bool ShouldSpeak(string key) => !focus.Working || key.StartsWith("focus") || FocusAllowedLines.Contains(key);

    void UpdateFocus(float dt)
    {
        focus.Update(dt);
        if (focus.Current != Focus.Phase.Off && !presence.Hidden)
        {
            // 倒數顯示在頭的旁邊（頭上是對話泡泡的位置）
            var u = DesktopWindow.UiScale;
            focusView.Show(focus.Label(), 1 - focus.Remaining / focus.Total, focus.Working,
                           pet.HeadTop() + new Vector2(-pet.Facing * 90 * u, 40 * u));
        }
        else focusView.Visible = false;

        if (apps.Enabled && (appTimer -= dt) <= 0)
        {
            appTimer = 1;
            apps.Update(1, ActiveApp.ProcessName(DesktopWindow.Hwnd));
        }
    }
}
