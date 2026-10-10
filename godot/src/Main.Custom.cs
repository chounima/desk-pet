// 主程式的自訂內容部分：讀取文件\DeskPet 的自訂配件與衣服、衣櫃分頁
using System.Collections.Generic;
using System.Linq;
using DeskPet.Content;
using DeskPet.UI.Hub;

namespace DeskPet;

public partial class Main
{
    List<OutfitData> outfits = null!;
    readonly List<string> customItemIds = new();

    void SetupCustom()
    {
        LoadCustom();
        var wardrobe = new WardrobeTab(shop, () => pet.Rig, () => outfits, id => settings.Outfits.GetValueOrDefault(id));
        hub.AddWardrobeTab(wardrobe);
        wardrobe.OutfitChosen += (ch, id) =>
        {
            if (id == null) settings.Outfits.Remove(ch); else settings.Outfits[ch] = id;
            settings.Save();
            ApplyOutfit();
        };
        wardrobe.ReloadRequested += () => { LoadCustom(); ApplyAccessories(); ApplyOutfit(); hub.RefreshAll(); };
        wardrobe.ShopRequested += cat => hub.Open("shop", cat);
        ApplyAccessories();
        ApplyOutfit();
    }

    // 重新掃描自訂資料夾：配件加進配件表與商店（免費），衣服另外列
    void LoadCustom()
    {
        foreach (var id in customItemIds) accessories.Remove(id);
        var loaded = CustomContent.LoadAccessories();
        foreach (var (id, (data, _)) in loaded) accessories[id] = data;
        customAccessoryNames = loaded.ToDictionary(kv => kv.Key, kv => kv.Value.Name);
        customItemIds.Clear();
        customItemIds.AddRange(customAccessoryNames.Keys);
        InjectCustomItems();
        outfits = CustomContent.LoadOutfits();
    }

    Dictionary<string, string> customAccessoryNames = new();

    /// <summary>自訂配件當作免費商品放進商店（開發者改參數後要重新放）</summary>
    void InjectCustomItems()
    {
        var items = progress.Cfg.Shop.Items;
        foreach (var key in items.Keys.Where(k => k.StartsWith("custom:")).ToList()) items.Remove(key);
        foreach (var (id, name) in customAccessoryNames)
            items[id] = new ProgressionConfig.ShopItem
            {
                Category = "acc", Label = I18n.T("custom.suffix", name), Price = 0, Accessory = id, Slot = accessories[id].Slot,
            };
    }

    /// <summary>目前角色的自訂衣服</summary>
    void ApplyOutfit()
    {
        var id = settings.Outfits.GetValueOrDefault(pet.Rig.Id);
        var outfit = outfits.FirstOrDefault(o => o.Id == id && o.Character == pet.Rig.Id);
        pet.SetOutfit(outfit?.Parts);
    }
}
