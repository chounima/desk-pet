// 衣櫃分頁：目前角色身上的配件（已擁有的可以穿上／脫下）、自訂衣服、打開自訂內容資料夾
using System;
using System.Collections.Generic;
using System.Linq;
using DeskPet.Content;
using DeskPet.Game.Progression;
using Godot;

namespace DeskPet.UI.Hub;

public partial class WardrobeTab : ScrollContainer
{
    readonly Shop shop;
    readonly Func<RigData> currentRig;
    readonly Func<List<OutfitData>> outfits;
    readonly Func<string, string?> wornOutfit;
    readonly VBoxContainer body = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };

    static readonly (string Slot, string Label)[] Slots = { ("head", "slot.head"), ("face", "slot.face"), ("neck", "slot.neck"), ("back", "slot.back") };

    public event Action<string, string?>? OutfitChosen;      // (角色, 衣服 id；null = 原本的)
    public event Action? ReloadRequested;
    public event Action<string>? ShopRequested;

    public WardrobeTab(Shop shop, Func<RigData> currentRig, Func<List<OutfitData>> outfits, Func<string, string?> wornOutfit)
    {
        this.shop = shop;
        this.currentRig = currentRig;
        this.outfits = outfits;
        this.wornOutfit = wornOutfit;
        Name = I18n.T("tab.wardrobe");
        HorizontalScrollMode = ScrollMode.Disabled;
        AddChild(body);
    }

    public void Refresh()
    {
        var scroll = ScrollVertical;
        Callable.From(() => ScrollVertical = scroll).CallDeferred();
        Ui.Clear(body);
        var rig = currentRig();
        body.AddChild(Ui.Text(I18n.T("wardrobe.title", rig.Name), Ui.Accent, 17));

        body.AddChild(Ui.Heading(I18n.T("wardrobe.accessories")));
        var owned = shop.Items("acc").Where(x => shop.Owns(x.Id)).ToList();
        foreach (var (slot, label) in Slots)
        {
            var row = new HFlowContainer();
            var tag = Ui.Text(I18n.T("wardrobe.slot", I18n.T(label)));
            tag.CustomMinimumSize = new Vector2(Ui.Px(60), 0);
            row.AddChild(tag);
            var any = false;
            foreach (var (id, item) in owned.Where(x => x.Item.Slot == slot))
            {
                any = true;
                var worn = shop.IsWorn(id, rig.Id);
                var b = Ui.Button(worn ? $"✓ {item.Label}" : item.Label, () =>
                {
                    if (worn) shop.TakeOff(id, rig.Id); else shop.Wear(id, rig.Id);
                    Refresh();
                });
                b.ToggleMode = true;
                b.ButtonPressed = worn;
                row.AddChild(b);
            }
            if (!any) row.AddChild(Ui.Text(I18n.T("wardrobe.none"), Ui.Muted));
            body.AddChild(row);
        }
        body.AddChild(Ui.Button(I18n.T("wardrobe.toShop"), () => ShopRequested?.Invoke("acc")));

        body.AddChild(Ui.Heading(I18n.T("wardrobe.outfits")));
        var mine = outfits().Where(o => o.Character == rig.Id).ToList();
        var current = wornOutfit(rig.Id);
        var clothes = new HFlowContainer();
        clothes.AddChild(OutfitButton(I18n.T("wardrobe.original"), null, current == null, rig.Id));
        foreach (var o in mine) clothes.AddChild(OutfitButton(o.Name, o.Id, current == o.Id, rig.Id));
        body.AddChild(clothes);
        if (mine.Count == 0)
            body.AddChild(Ui.Text(I18n.T("wardrobe.noOutfits", rig.Name), Ui.Muted, 13, wrap: true));

        body.AddChild(Ui.Heading(I18n.T("wardrobe.custom")));
        body.AddChild(Ui.Row(
            Ui.Button(I18n.T("wardrobe.folder"), () => { CustomContent.EnsureFolders(); OS.ShellOpen(CustomContent.Root); }),
            Ui.Button(I18n.T("wardrobe.reload"), () => ReloadRequested?.Invoke())));
        body.AddChild(Ui.Text(CustomContent.Root, Ui.Muted, 12, wrap: true));
    }

    Button OutfitButton(string label, string? id, bool on, string ch)
    {
        var b = Ui.Button(on ? $"✓ {label}" : label, () => { OutfitChosen?.Invoke(ch, id); Refresh(); });
        b.ToggleMode = true;
        b.ButtonPressed = on;
        return b;
    }
}
