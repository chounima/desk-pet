// 主程式的日常體驗部分：右鍵選單、全螢幕自動隱藏、暫時隱藏、螢幕變化自適應、依時間反應
using DeskPet.Content;
using DeskPet.Game;
using DeskPet.Platform;
using DeskPet.UI;
using Godot;

namespace DeskPet;

public partial class Main
{
    DailyConfig daily = null!;
    DailyLife dailyLife = null!;
    Presence presence = null!;
    PetMenu petMenu = null!;
    float fullscreenTimer, displayTimer;

    void SetupDaily()
    {
        dailyLife = new DailyLife(daily, behavior);
        presence = new Presence { AutoHideFullscreen = settings.AutoHideFullscreen };
        presence.Changed += OnPresenceChanged;

        petMenu = new PetMenu(MenuConfig.Load());
        AddChild(petMenu);
        petMenu.ActionChosen += name => behavior.Force(name);
        petMenu.FormChosen += ChooseForm;
        petMenu.VolleyToggled += () =>
        {
            volley.SetActive(!volley.Active);
            tray.SetVolley(volley.Active);
        };
        petMenu.HideRequested += () =>
        {
            behavior.Say("hide");
            // 讓她說完再躲
            GetTree().CreateTimer(1.5).Timeout += () => presence.HideFor(daily.HideMinutes);
        };

        tray.SetDaily(settings.AutoHideFullscreen, settings.FitScreen);
        tray.FullscreenToggled += on => { presence.AutoHideFullscreen = on; settings.AutoHideFullscreen = on; settings.Save(); };
        tray.FitScreenToggled += on => { settings.FitScreen = on; settings.Save(); pet.SetScale(PetScale(settings.Size)); };
        tray.HideToggled += () =>
        {
            if (presence.ManualHidden) presence.ShowNow();
            else presence.HideFor(daily.HideMinutes);
        };
    }

    void UpdateDaily(float dt)
    {
        presence.Update();
        if ((fullscreenTimer -= dt) <= 0)
        {
            fullscreenTimer = daily.FullscreenCheckSeconds;
            presence.SetFullscreen(FullscreenWatcher.IsFullscreenActive(DesktopWindow.Hwnd));
        }
        if ((displayTimer -= dt) <= 0)
        {
            displayTimer = daily.DisplayCheckSeconds;
            if (DesktopWindow.RefreshDisplay()) OnDisplayChanged();
        }
        if (!presence.Hidden) dailyLife.Update(dt);
    }

    void OnPresenceChanged(bool hidden)
    {
        pet.View.Visible = !hidden;
        fx.Visible = !hidden;
        bubble.Visible = !hidden;
        tray.SetHidden(presence.ManualHidden);
        if (hidden)
        {
            drag = null;
            if (volley.Active) { volley.SetActive(false); tray.SetVolley(false); }
            DesktopWindow.SetClickThrough(true);
        }
        else behavior.Say("back");
    }

    // 解析度、縮放比例、工作列位置改變：重新計算大小，角色留在畫面內並回到地面
    void OnDisplayChanged()
    {
        pet.SetScale(PetScale(settings.Size));
        var area = Area();
        pet.X = Mathf.Clamp(pet.X, area.Position.X + 40, area.End.X - 40);
        if (pet.Y > area.End.Y || behavior.Current == Behavior.State.Ground) pet.Y = Mathf.Min(pet.Y, area.End.Y);
        RefreshWindows();
        GD.Print($"[DeskPet] 螢幕變化：area={DesktopWindow.Area} uiScale={DesktopWindow.UiScale} scale={pet.Scale:0.00}");
    }

    /// <summary>角色大小：依螢幕縮放比例（預設），或依螢幕高度（系統匣「大小跟著螢幕高度」）</summary>
    float PetScale(float size)
    {
        if (settings.FitScreen && daily != null)
            return DesktopWindow.Area.Size.Y * daily.FitScreen.ScreenRatio / daily.FitScreen.BaseHeight * size;
        return Tuning.Scale * DesktopWindow.UiScale * size;
    }

    void OpenPetMenu()
    {
        var id = pet.Rig.Id;
        petMenu.Open(DisplayServer.MouseGetPosition(), pet.Clips, _ => null,
                     rigs[id].Forms, FormOf(id), _ => null, volley.Active, daily.HideMinutes, DesktopWindow.UiScale);
    }
}
