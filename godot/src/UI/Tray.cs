// 系統匣圖示與右鍵選單：切換角色、自動行為開關、結束
using System;
using System.Collections.Generic;
using DeskPet.Content;
using Godot;

namespace DeskPet.UI;

public partial class Tray : Node
{
    const int IdAuto = 1000, IdQuit = 1001;

    readonly List<string> ids = new();
    PopupMenu menu = null!;

    public event Action<string>? CharacterChosen;
    public event Action<bool>? AutoToggled;
    public event Action? QuitRequested;

    public void Build(IList<RigData> characters, string current, Texture2D icon)
    {
        menu = new PopupMenu();
        AddChild(menu);
        for (var i = 0; i < characters.Count; i++)
        {
            ids.Add(characters[i].Id);
            menu.AddRadioCheckItem($"{characters[i].Name}  {characters[i].Description}", i);
        }
        menu.AddSeparator();
        menu.AddCheckItem("自動行為", IdAuto);
        menu.SetItemChecked(menu.GetItemIndex(IdAuto), true);
        menu.AddItem("結束", IdQuit);
        menu.IdPressed += OnId;
        SetCurrent(current);

        var indicator = new StatusIndicator { Icon = icon, Tooltip = "Desk Pet" };
        AddChild(indicator);
        indicator.Menu = indicator.GetPathTo(menu);
    }

    public void SetCurrent(string id)
    {
        for (var i = 0; i < ids.Count; i++) menu.SetItemChecked(menu.GetItemIndex(i), ids[i] == id);
    }

    void OnId(long id)
    {
        if (id == IdQuit) { QuitRequested?.Invoke(); return; }
        if (id == IdAuto)
        {
            var index = menu.GetItemIndex(IdAuto);
            var on = !menu.IsItemChecked(index);
            menu.SetItemChecked(index, on);
            AutoToggled?.Invoke(on);
            return;
        }
        if (id >= 0 && id < ids.Count)
        {
            SetCurrent(ids[(int)id]);
            CharacterChosen?.Invoke(ids[(int)id]);
        }
    }
}
