// 系統匣圖示與右鍵選單：切換角色、大小、自動行為、今日統計、結束
using System;
using System.Collections.Generic;
using DeskPet.Content;
using DeskPet.Game;
using DeskPet.Save;
using Godot;

namespace DeskPet.UI;

public partial class Tray : Node
{
    // 選單項目 id：角色用 0..n-1，大小用 IdSize + i
    const int IdSize = 100, IdAuto = 1000, IdAutostart = 1001, IdToday = 1002, IdQuit = 1003, IdVolley = 1004, IdSound = 1005;

    readonly List<string> ids = new();
    PopupMenu menu = null!;
    StatusIndicator indicator = null!;
    int shownKeys = -1;

    public event Action<string>? CharacterChosen;
    public event Action<float>? SizeChosen;
    public event Action<bool>? AutoToggled;
    public event Action<bool>? AutostartToggled;
    public event Action<bool>? VolleyToggled;
    public event Action<bool>? SoundToggled;
    public event Action? QuitRequested;

    public void Build(IList<RigData> characters, Settings settings, bool autostart, Texture2D icon)
    {
        menu = new PopupMenu();
        AddChild(menu);

        for (var i = 0; i < characters.Count; i++)
        {
            ids.Add(characters[i].Id);
            menu.AddRadioCheckItem($"{characters[i].Name}  {characters[i].Description}", i);
        }
        menu.AddSeparator("大小");
        for (var i = 0; i < Tuning.Sizes.Length; i++)
            menu.AddRadioCheckItem(Tuning.Sizes[i].Label, IdSize + i);
        menu.AddSeparator();
        menu.AddCheckItem("🏐 排球對打", IdVolley);
        menu.AddCheckItem("自動行為", IdAuto);
        menu.AddCheckItem("🔊 音效", IdSound);
        menu.AddCheckItem("開機時自動啟動", IdAutostart);
        menu.AddSeparator();
        menu.AddItem("", IdToday);
        menu.SetItemDisabled(menu.GetItemIndex(IdToday), true);
        menu.AddItem("結束", IdQuit);
        menu.IdPressed += OnId;

        SetCharacter(settings.Character);
        SetSize(settings.Size);
        SetChecked(IdAuto, settings.Auto);
        SetChecked(IdSound, settings.Sound);
        SetChecked(IdAutostart, autostart);

        indicator = new StatusIndicator { Icon = icon, Tooltip = "Desk Pet" };
        AddChild(indicator);
        indicator.Menu = indicator.GetPathTo(menu);
        SetTodayKeys(settings.TodayKeys);
    }

    public void SetCharacter(string id)
    {
        for (var i = 0; i < ids.Count; i++) SetChecked(i, ids[i] == id);
    }

    public void SetSize(float value)
    {
        for (var i = 0; i < Tuning.Sizes.Length; i++)
            SetChecked(IdSize + i, Mathf.IsEqualApprox(Tuning.Sizes[i].Value, value));
    }

    public void SetTodayKeys(int count)
    {
        if (count == shownKeys) return;
        shownKeys = count;
        menu.SetItemText(menu.GetItemIndex(IdToday), $"今天打了 {count:N0} 下");
        indicator.Tooltip = $"Desk Pet — 今天 {count:N0} 下";
    }

    void SetChecked(int id, bool on) => menu.SetItemChecked(menu.GetItemIndex(id), on);

    public void SetVolley(bool on) => SetChecked(IdVolley, on);

    void OnId(long id)
    {
        switch (id)
        {
            case IdQuit:
                QuitRequested?.Invoke();
                break;
            case IdAuto:
                var on = !menu.IsItemChecked(menu.GetItemIndex(IdAuto));
                SetChecked(IdAuto, on);
                AutoToggled?.Invoke(on);
                break;
            case IdSound:
                var sound = !menu.IsItemChecked(menu.GetItemIndex(IdSound));
                SetChecked(IdSound, sound);
                SoundToggled?.Invoke(sound);
                break;
            case IdVolley:
                var volley = !menu.IsItemChecked(menu.GetItemIndex(IdVolley));
                SetChecked(IdVolley, volley);
                VolleyToggled?.Invoke(volley);
                break;
            case IdAutostart:
                var start = !menu.IsItemChecked(menu.GetItemIndex(IdAutostart));
                SetChecked(IdAutostart, start);
                AutostartToggled?.Invoke(start);
                break;
            case >= IdSize and < IdSize + 100 when id - IdSize < Tuning.Sizes.Length:
                var size = Tuning.Sizes[id - IdSize].Value;
                SetSize(size);
                SizeChosen?.Invoke(size);
                break;
            case >= 0 when id < ids.Count:
                SetCharacter(ids[(int)id]);
                CharacterChosen?.Invoke(ids[(int)id]);
                break;
        }
    }
}
