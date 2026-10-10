// 商店：用點數買商品、買了永久擁有，外觀類可以切換使用中的款式（規則與 preview/js/game/shop.js 相同）
using System.Collections.Generic;
using System.Linq;
using DeskPet.Content;

namespace DeskPet.Game.Progression;

public sealed class Shop
{
    readonly Progress progress;

    public Shop(Progress progress) => this.progress = progress;

    ProgressionConfig.ShopConfig Cfg => progress.Cfg.Shop;
    ProgressState State => progress.State;

    public IReadOnlyDictionary<string, string> Categories => Cfg.Categories;

    public IEnumerable<(string Id, ProgressionConfig.ShopItem Item)> Items(string? category = null) =>
        Cfg.Items.Where(kv => category == null || kv.Value.Category == category).Select(kv => (kv.Key, kv.Value));

    public ProgressionConfig.ShopItem? Item(string id) => Cfg.Items.GetValueOrDefault(id);

    public bool Owns(string? id) => id != null && Item(id) is { } it && (it.Price == 0 || State.Owned.Contains(id));

    /// <summary>還沒達成購買條件時回傳 (角色, 目前等級, 需要等級)</summary>
    public (string Char, int Have, int Need)? Requirement(string id)
    {
        var a = Item(id)?.Requires?.Affection;
        if (a == null) return null;
        var have = progress.Level(a.Char).Level;
        return have >= a.Level ? null : (a.Char, have, a.Level);
    }

    /// <summary>reason：owned | locked | points | missing</summary>
    public (bool Ok, string? Reason) CanBuy(string id)
    {
        var it = Item(id);
        if (it == null) return (false, "missing");
        if (Owns(id)) return (false, "owned");
        if (Requirement(id) != null) return (false, "locked");
        if (State.Points < it.Price) return (false, "points");
        return (true, null);
    }

    public bool Buy(string id)
    {
        if (!CanBuy(id).Ok) return false;
        var it = Item(id)!;
        State.Points -= it.Price;
        State.Owned.Add(id);
        if (Cfg.Defaults.ContainsKey(it.Category)) State.Equipped[it.Category] = id;     // 外觀類買了直接換上
        progress.Emit(new ProgressEvent("buy", ItemId: id));
        progress.Commit();
        return true;
    }

    public bool IsCosmetic(string category) => Cfg.Defaults.ContainsKey(category);

    public string? Equipped(string category)
    {
        var id = State.Equipped.GetValueOrDefault(category);
        return Owns(id) ? id : Cfg.Defaults.GetValueOrDefault(category);
    }

    public ProgressionConfig.ShopItem? EquippedItem(string category) => Equipped(category) is { } id ? Item(id) : null;

    public bool Equip(string id)
    {
        var it = Item(id);
        if (it == null || !Owns(id) || !IsCosmetic(it.Category)) return false;
        State.Equipped[it.Category] = id;
        progress.Emit(new ProgressEvent("equip", ItemId: id));
        progress.Commit();
        return true;
    }

    /// <summary>動作商品 id（不是商品時為 null）</summary>
    public string? ActionItem(string action) => Items("action").FirstOrDefault(x => x.Item.Action == action).Id;

    /// <summary>動作是否可以使用：不是商品的動作一律可以</summary>
    public bool ActionOwned(string action) => ActionItem(action) is not { } id || Owns(id);
}
