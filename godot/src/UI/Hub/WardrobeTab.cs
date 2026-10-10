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

    static readonly (string Slot, string Label)[] Slots = { ("head", "頭"), ("face", "臉"), ("neck", "脖子"), ("back", "背") };

    public event Action<string, string?>? OutfitChosen;      // (角色, 衣服 id；null = 原本的)
    public event Action? ReloadRequested;
    public event Action<string>? ShopRequested;

    public WardrobeTab(Shop shop, Func<RigData> currentRig, Func<List<OutfitData>> outfits, Func<string, string?> wornOutfit)
    {
        this.shop = shop;
        this.currentRig = currentRig;
        this.outfits = outfits;
        this.wornOutfit = wornOutfit;
        Name = "👗 衣櫃";
        HorizontalScrollMode = ScrollMode.Disabled;
        AddChild(body);
    }

    public void Refresh()
    {
        var scroll = ScrollVertical;
        Callable.From(() => ScrollVertical = scroll).CallDeferred();
        Ui.Clear(body);
        var rig = currentRig();
        body.AddChild(Ui.Text($"正在幫 {rig.Name} 換裝", Ui.Accent, 17));

        body.AddChild(Ui.Heading("配件（在商店買，或自己做）"));
        var owned = shop.Items("acc").Where(x => shop.Owns(x.Id)).ToList();
        foreach (var (slot, label) in Slots)
        {
            var row = new HFlowContainer();
            var tag = Ui.Text($"{label}：");
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
            if (!any) row.AddChild(Ui.Text("（還沒有）", Ui.Muted));
            body.AddChild(row);
        }
        body.AddChild(Ui.Button("🛒 去商店買配件", () => ShopRequested?.Invoke("acc")));

        body.AddChild(Ui.Heading("衣服（自訂）"));
        var mine = outfits().Where(o => o.Character == rig.Id).ToList();
        var current = wornOutfit(rig.Id);
        var clothes = new HFlowContainer();
        clothes.AddChild(OutfitButton("原本的衣服", null, current == null, rig.Id));
        foreach (var o in mine) clothes.AddChild(OutfitButton(o.Name, o.Id, current == o.Id, rig.Id));
        body.AddChild(clothes);
        if (mine.Count == 0)
            body.AddChild(Ui.Text($"還沒有 {rig.Name} 的自訂衣服。用網頁預覽的編輯器（preview/editor.html）做好，放進下面的資料夾。", Ui.Muted, 13, wrap: true));

        body.AddChild(Ui.Heading("自訂內容"));
        body.AddChild(Ui.Row(
            Ui.Button("📂 打開自訂內容資料夾", () => { CustomContent.EnsureFolders(); OS.ShellOpen(CustomContent.Root); }),
            Ui.Button("🔄 重新讀取", () => ReloadRequested?.Invoke())));
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
