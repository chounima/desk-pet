// 角色右鍵選單：在游標旁邊跳出，可以叫角色做動作、切換型態、排球、暫時隱藏
// 動作清單在 data/menu.json；鎖住的（商店還沒買）顯示 🔒 不能選
using System;
using System.Collections.Generic;
using DeskPet.Content;
using Godot;

namespace DeskPet.UI;

public partial class PetMenu : Node
{
    const int IdVolley = 1, IdHide = 2, IdCollection = 3, IdShop = 4, IdAction = 100, IdForm = 200;

    readonly MenuConfig config;
    PopupMenu menu = null!, actions = null!, forms = null!;
    readonly List<string> formIds = new();

    public event Action<string>? ActionChosen;
    public event Action<string>? FormChosen;
    public event Action? VolleyToggled;
    public event Action? HideRequested;
    public event Action<string>? HubRequested;

    public PetMenu(MenuConfig config) => this.config = config;

    public override void _Ready()
    {
        menu = new PopupMenu();
        actions = new PopupMenu();
        forms = new PopupMenu();
        AddChild(menu);
        actions.IdPressed += id => ActionChosen?.Invoke(config.Actions[(int)(id - IdAction)]);
        forms.IdPressed += id => FormChosen?.Invoke(formIds[(int)(id - IdForm)]);
        menu.IdPressed += id =>
        {
            if (id == IdVolley) VolleyToggled?.Invoke();
            else if (id == IdHide) HideRequested?.Invoke();
            else if (id == IdCollection) HubRequested?.Invoke("collection");
            else if (id == IdShop) HubRequested?.Invoke("shop");
        };
    }

    /// <param name="clips">目前角色的動作組（取標籤用）</param>
    /// <param name="lockedHint">動作鎖住時回傳提示文字</param>
    /// <param name="hideMinutes">「躲起來」的分鐘數</param>
    public void Open(Vector2I screenPos, Dictionary<string, ClipData> clips, Func<string, string?> lockedHint,
                     Dictionary<string, FormData>? formList, string? currentForm, Func<string, string?> formLocked,
                     bool volleyOn, float hideMinutes, float uiScale)
    {
        menu.Clear();
        actions.Clear();
        forms.Clear();
        formIds.Clear();

        for (var i = 0; i < config.Actions.Count; i++)
        {
            var name = config.Actions[i];
            if (!clips.TryGetValue(name, out var clip)) continue;
            var hint = lockedHint(name);
            actions.AddItem(hint == null ? clip.Label : $"🔒 {clip.Label}", IdAction + i);
            var index = actions.GetItemIndex(IdAction + i);
            if (hint != null) actions.SetItemTooltip(index, hint);     // 鎖住的仍可選：選了會打開商店
        }
        menu.AddSubmenuNodeItem("🎭 動作", actions);

        if (formList is { Count: > 1 })
        {
            foreach (var (name, f) in formList)
            {
                var locked = formLocked(name);
                forms.AddRadioCheckItem(locked == null ? f.Label : $"🔒 {f.Label}", IdForm + formIds.Count);
                forms.SetItemChecked(formIds.Count, name == currentForm);
                if (locked != null) forms.SetItemDisabled(formIds.Count, true);
                formIds.Add(name);
            }
            menu.AddSubmenuNodeItem("🔁 型態", forms);
        }

        menu.AddSeparator();
        menu.AddItem(volleyOn ? "🏐 結束對打" : "🏐 排球對打", IdVolley);
        menu.AddItem($"😴 躲起來 {hideMinutes:0} 分鐘", IdHide);
        menu.AddSeparator();
        menu.AddItem("📖 收藏", IdCollection);
        menu.AddItem("🛒 商店", IdShop);

        foreach (var m in new[] { menu, actions, forms }) MenuStyle.Apply(m, uiScale);
        menu.Popup(new Rect2I(screenPos, Vector2I.Zero));
    }
}
