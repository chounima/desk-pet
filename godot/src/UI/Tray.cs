// 系統匣圖示與右鍵選單：切換角色、大小、自動行為、今日統計、結束
using System;
using System.Collections.Generic;
using DeskPet.Content;
using DeskPet.Game;
using DeskPet.Platform;
using DeskPet.Save;
using Godot;

namespace DeskPet.UI;

public partial class Tray : Node
{
    // 選單項目 id：角色用 0..n-1，大小用 IdSize + i
    const int IdSize = 100, IdForm = 200, IdAuto = 1000, IdAutostart = 1001, IdToday = 1002, IdQuit = 1003, IdVolley = 1004, IdSound = 1005,
        IdFullscreen = 1006, IdFit = 1007, IdHide = 1008,
        IdCollection = 1009, IdShop = 1010, IdSettings = 1011, IdGuitar = 1012, IdFocus = 1013, IdWardrobe = 1014;

    readonly List<string> ids = new();
    readonly List<string> formIds = new();
    PopupMenu menu = null!;
    PopupMenu formMenu = null!;
    StatusIndicator indicator = null!;
    int shownKeys = -1;

    public event Action<string>? CharacterChosen;
    public event Action<string>? FormChosen;
    public event Action<float>? SizeChosen;
    public event Action<bool>? AutoToggled;
    public event Action<bool>? AutostartToggled;
    public event Action<bool>? VolleyToggled;
    public event Action<bool>? SoundToggled;
    public event Action? QuitRequested;
    public event Action<bool>? FullscreenToggled;
    public event Action<bool>? FitScreenToggled;
    public event Action? HideToggled;
    /// <summary>打開 Desk Pet 視窗的某個分頁：collection | shop | settings</summary>
    public event Action<string>? HubRequested;
    public event Action? GuitarToggled;
    public event Action? FocusToggled;
    readonly List<string> charLabels = new();

    public void Build(IList<RigData> characters, Settings settings, bool autostart, Texture2D icon)
    {
        menu = new PopupMenu();
        AddChild(menu);

        for (var i = 0; i < characters.Count; i++)
        {
            ids.Add(characters[i].Id);
            charLabels.Add($"{characters[i].Name}  {characters[i].Description}");
            menu.AddRadioCheckItem(charLabels[i], i);
        }
        // 型態子選單：只有多種型態的角色可以用（見 SetForms）
        formMenu = new PopupMenu();
        formMenu.IdPressed += id => { if (id - IdForm < formIds.Count) FormChosen?.Invoke(formIds[(int)(id - IdForm)]); };
        menu.AddSubmenuNodeItem("🔁 型態", formMenu, IdForm - 1);
        menu.AddSeparator();
        menu.AddItem("👗 衣櫃", IdWardrobe);
        menu.AddItem("📖 收藏", IdCollection);
        menu.AddItem("🛒 商店", IdShop);
        menu.AddItem("⚙ 設定…", IdSettings);
        menu.AddSeparator("大小");
        for (var i = 0; i < Tuning.Sizes.Length; i++)
            menu.AddRadioCheckItem(Tuning.Sizes[i].Label, IdSize + i);
        menu.AddSeparator();
        menu.AddItem("🍅 開始專注", IdFocus);
        menu.AddItem("🎸 彈吉他", IdGuitar);
        menu.AddCheckItem("🏐 排球對打", IdVolley);
        menu.AddCheckItem("自動行為", IdAuto);
        menu.AddCheckItem("🔊 音效", IdSound);
        menu.AddCheckItem("全螢幕時自動隱藏", IdFullscreen);
        menu.AddCheckItem("大小跟著螢幕高度", IdFit);
        menu.AddItem("😴 暫時隱藏", IdHide);
        menu.AddCheckItem("開機時自動啟動", IdAutostart);
        menu.AddSeparator();
        menu.AddItem("", IdToday);
        menu.SetItemDisabled(menu.GetItemIndex(IdToday), true);
        menu.AddItem("結束", IdQuit);
        menu.IdPressed += OnId;
        MenuStyle.Apply(menu, DesktopWindow.UiScale);
        MenuStyle.Apply(formMenu, DesktopWindow.UiScale);

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

    /// <summary>鎖住的角色：顯示 🔒、不能選；lockedHint 回傳提示文字表示鎖住</summary>
    public void SetCharacterLocks(Func<string, string?> lockedHint)
    {
        for (var i = 0; i < ids.Count; i++)
        {
            var hint = lockedHint(ids[i]);
            var index = menu.GetItemIndex(i);
            menu.SetItemText(index, hint == null ? charLabels[i] : $"🔒 {charLabels[i]}");
            menu.SetItemDisabled(index, hint != null);
            menu.SetItemTooltip(index, hint ?? "");
        }
    }

    public void SetCharacter(string id)
    {
        for (var i = 0; i < ids.Count; i++) SetChecked(i, ids[i] == id);
    }

    /// <summary>更新型態子選單；forms 為 null 或只有一種時停用</summary>
    public void SetForms(Dictionary<string, FormData>? forms, string? current, Func<string, string?>? lockedHint = null)
    {
        formMenu.Clear();
        formIds.Clear();
        foreach (var (name, f) in forms ?? new())
        {
            var hint = lockedHint?.Invoke(name);
            formMenu.AddRadioCheckItem(hint == null ? f.Label : $"🔒 {f.Label}", IdForm + formIds.Count);
            formMenu.SetItemChecked(formIds.Count, name == current);
            formMenu.SetItemDisabled(formIds.Count, hint != null);
            formMenu.SetItemTooltip(formIds.Count, hint ?? "");
            formIds.Add(name);
        }
        var label = forms is { Count: > 1 } && current != null && forms.TryGetValue(current, out var cur)
            ? $"🔁 型態：{cur.Label}" : "🔁 型態";
        var index = menu.GetItemIndex(IdForm - 1);
        menu.SetItemText(index, label);
        menu.SetItemDisabled(index, formIds.Count < 2);
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

    public void SetFocusing(bool on) => menu.SetItemText(menu.GetItemIndex(IdFocus), on ? "⏹ 結束專注" : "🍅 開始專注");

    public void SetPerforming(bool on) => menu.SetItemText(menu.GetItemIndex(IdGuitar), on ? "⏹ 停止演奏" : "🎸 彈吉他");

    /// <summary>設定視窗改了選項時同步勾選狀態（key 同 SettingsTab）</summary>
    public void SetOption(string key, bool on)
    {
        int? id = key switch
        {
            "auto" => IdAuto, "sound" => IdSound, "autostart" => IdAutostart,
            "fullscreenHide" => IdFullscreen, "fitScreen" => IdFit, _ => null,
        };
        if (id is { } i) SetChecked(i, on);
    }

    public void SetDaily(bool autoHideFullscreen, bool fitScreen)
    {
        SetChecked(IdFullscreen, autoHideFullscreen);
        SetChecked(IdFit, fitScreen);
    }

    /// <summary>暫時隱藏中，選單改成「叫她出來」</summary>
    public void SetHidden(bool hidden) =>
        menu.SetItemText(menu.GetItemIndex(IdHide), hidden ? "🙋 叫她出來" : "😴 暫時隱藏");

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
            case IdFullscreen:
                var fs = !menu.IsItemChecked(menu.GetItemIndex(IdFullscreen));
                SetChecked(IdFullscreen, fs);
                FullscreenToggled?.Invoke(fs);
                break;
            case IdFit:
                var fit = !menu.IsItemChecked(menu.GetItemIndex(IdFit));
                SetChecked(IdFit, fit);
                FitScreenToggled?.Invoke(fit);
                break;
            case IdHide:
                HideToggled?.Invoke();
                break;
            case IdFocus:
                FocusToggled?.Invoke();
                break;
            case IdGuitar:
                GuitarToggled?.Invoke();
                break;
            case IdWardrobe:
                HubRequested?.Invoke("wardrobe");
                break;
            case IdCollection:
                HubRequested?.Invoke("collection");
                break;
            case IdShop:
                HubRequested?.Invoke("shop");
                break;
            case IdSettings:
                HubRequested?.Invoke("settings");
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
