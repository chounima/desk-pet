// 商店分頁：分類按鈕、商品卡片（預覽、價格、鎖定條件）、購買／使用／預覽
using System;
using System.Collections.Generic;
using System.Linq;
using DeskPet.Content;
using DeskPet.Game.Progression;
using DeskPet.View;
using Godot;

namespace DeskPet.UI.Hub;

public partial class ShopTab : VBoxContainer
{
    readonly Shop shop;
    readonly Progress progress;
    readonly Dictionary<string, PropData> props;
    readonly Dictionary<string, AccessoryData> accessories;
    readonly Func<string> currentChar;
    readonly Func<string, string> names;
    readonly HBoxContainer tabs = new();
    readonly GridContainer grid = new() { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
    readonly Dictionary<string, Texture2D> previews = new();
    string category;

    /// <summary>在角色身上預覽商品（Main 處理：播動作、冒特效、講一句話）</summary>
    public event Action<string, ProgressionConfig.ShopItem>? Previewed;

    public ShopTab(Shop shop, Progress progress, Dictionary<string, PropData> props, Dictionary<string, AccessoryData> accessories,
                   Func<string> currentChar, Func<string, string> names)
    {
        this.shop = shop;
        this.progress = progress;
        this.props = props;
        this.accessories = accessories;
        this.currentChar = currentChar;
        this.names = names;
        Name = "🛒 商店";
        category = shop.Categories.Keys.FirstOrDefault() ?? "";
        AddChild(tabs);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.AddChild(grid);
        AddChild(scroll);
    }

    public void ShowCategory(string cat)
    {
        if (shop.Categories.ContainsKey(cat)) category = cat;
        Refresh();
    }

    public void Refresh()
    {
        Ui.Clear(tabs);
        foreach (var (cat, label) in shop.Categories)
        {
            var b = Ui.Button(label, () => ShowCategory(cat));
            b.ToggleMode = true;
            b.ButtonPressed = cat == category;
            tabs.AddChild(b);
        }
        Ui.Clear(grid);
        foreach (var (id, item) in shop.Items(category)) grid.AddChild(ItemCard(id, item));
    }

    Control Preview(ProgressionConfig.ShopItem it)
    {
        var box = new CenterContainer { CustomMinimumSize = new Vector2(0, Ui.Px(60)) };
        if (it.Accessory != null && accessories.TryGetValue(it.Accessory, out var acc))
        {
            var key = $"acc:{it.Accessory}";
            if (!previews.TryGetValue(key, out var tex))
            {
                var w = acc.Box[2] - acc.Box[0];
                previews[key] = tex = SvgTextures.Load(acc.Path, Ui.Px(52) / Math.Max(1, w) * 1.4f).Texture;
            }
            box.AddChild(new TextureRect { Texture = tex, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                                           CustomMinimumSize = new Vector2(Ui.Px(52), Ui.Px(52)), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize });
        }
        else if (it.Prop != null && props.TryGetValue(it.Prop, out var prop))
        {
            if (!previews.TryGetValue(it.Prop, out var tex))
                previews[it.Prop] = tex = SvgTextures.Load(prop.Path, Ui.Px(48) / 34f).Texture;
            box.AddChild(new TextureRect { Texture = tex, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                                           CustomMinimumSize = new Vector2(Ui.Px(48), Ui.Px(48)), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize });
        }
        else if (it.Symbol != null) box.AddChild(Ui.Text(it.Symbol, it.Color != null ? new Color(it.Color) : null, 34));
        else if (it.Style != null)
        {
            var (style, text, _, rot) = BubbleStyles.Make(it.Style, Ui.U * 0.8f);
            var panel = new PanelContainer { Rotation = rot };
            panel.AddThemeStyleboxOverride("panel", style);
            panel.AddChild(Ui.Text("嗨！", text, 13));
            box.AddChild(panel);
        }
        else box.AddChild(Ui.Text(it.Icon ?? "🎁", null, 34));
        return box;
    }

    Control ItemCard(string id, ProgressionConfig.ShopItem it)
    {
        var owned = shop.Owns(id);
        var equipped = shop.IsCosmetic(it.Category) && shop.Equipped(it.Category) == id;
        var (card, body) = Ui.Card(highlight: equipped);
        card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        body.Alignment = AlignmentMode.Center;
        body.AddChild(Preview(it));
        var title = Ui.Text(it.Label);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        body.AddChild(title);

        var actions = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center };
        if (owned && shop.IsWearable(id))
        {
            var ch = currentChar();
            actions.AddChild(shop.IsWorn(id, ch)
                ? Ui.Button("脫下", () => { shop.TakeOff(id, ch); Refresh(); })
                : Ui.Button("穿上", () => { shop.Wear(id, ch); Refresh(); }));
        }
        else if (owned)
        {
            if (equipped) actions.AddChild(Ui.Text("使用中", Ui.Accent, 13));
            else if (shop.IsCosmetic(it.Category)) actions.AddChild(Ui.Button("使用", () => { shop.Equip(id); Refresh(); }));
            else actions.AddChild(Ui.Text("✓ 已擁有", Ui.Muted, 13));
        }
        else if (shop.Requirement(id) is { } req)
            actions.AddChild(Ui.Text($"🔒 {names(req.Char)} ❤ Lv{req.Have} / {req.Need}", Ui.Muted, 13));
        else
        {
            var (ok, reason) = shop.CanBuy(id);
            var buy = Ui.Button($"購買 💎 {it.Price}", () => { shop.Buy(id); Refresh(); });
            buy.Disabled = !ok;
            if (reason == "points") buy.TooltipText = $"還差 {it.Price - progress.State.Points} 點";
            actions.AddChild(buy);
        }
        actions.AddChild(Ui.Button("預覽", () => Previewed?.Invoke(id, it)));
        body.AddChild(actions);
        return card;
    }
}
