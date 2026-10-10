// 主程式的收集系統部分：點數、好感度、解鎖、商店外觀、Desk Pet 視窗（收藏／商店／設定／開發者）
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeskPet.Content;
using DeskPet.Game.Progression;
using DeskPet.Platform;
using DeskPet.UI.Hub;
using Godot;

namespace DeskPet;

public partial class Main
{
    const float ProgressSaveEvery = 10;

    Progress progress = null!;
    ProgressState? progressState;
    Shop shop = null!;
    HubWindow hub = null!;
    Dictionary<string, PropData> allProps = null!;
    bool progressDirty;
    float progressSaveTimer = ProgressSaveEvery;
    int pendingPoints;
    float pointsTimer;

    void SetupProgress(List<string> ids, Dictionary<string, PropData> props)
    {
        allProps = props;
        var isNewSave = !File.Exists(ProjectSettings.GlobalizePath("user://progress.json"));
        progressState = ProgressState.Load();
        progress = new Progress(ProgressionConfig.Load(), progressState, _ => progressDirty = true);
        shop = new Shop(progress);
        progress.Event += OnProgressEvent;
        progress.CheckDay();
        if (isNewSave) KeepWhatWasInUse(ids);
        progress.CheckUnlocks();

        behavior.Allowed = shop.ActionOwned;
        volley.Rallied += rally => progress.OnVolley(pet.Rig.Id, rally);

        var characters = ids.Select(id => rigs[id]).ToList();
        hub = new HubWindow(progress, shop, settings, characters, props, Autostart.IsEnabled, () => pet.Rig.Id);
        AddChild(hub);
        hub.SetDevMode(settings.DevMode || args.ContainsKey("dev"));
        hub.Shop.Previewed += PreviewItem;
        hub.Dev.Changed += () => { RefreshLocks(); ApplyCosmetics(); };
        hub.Settings.Toggled += SetOption;
        hub.Settings.SizeChosen += SetSize;
        tray.HubRequested += tab => hub.Open(tab);

        // 起始角色還沒解鎖（例如開發者重置進度後）：換成第一個能用的
        if (!progress.CharacterUnlocked(pet.Rig.Id) && ids.FirstOrDefault(progress.CharacterUnlocked) is { } fallback)
            SwitchCharacter(fallback);
        RefreshLocks();
        ApplyCosmetics();
        if (args.TryGetValue("hub", out var tab)) hub.Open(tab);     // --hub=shop：一開始就打開視窗（截圖用）
    }

    // 收集系統上線前就在用的角色與型態直接算解鎖，不要因為更新就把使用者正在用的鎖起來
    void KeepWhatWasInUse(List<string> ids)
    {
        void Keep(string key) { if (!progress.IsUnlocked(key)) progressState!.Unlocked.Add(key); }
        if (ids.Contains(settings.Character)) Keep($"character:{settings.Character}");
        foreach (var (id, form) in settings.Forms)
            if (rigs.TryGetValue(id, out var rig) && form != ContentLoader.DefaultForm(rig)) Keep($"form:{id}/{form}");
        progress.Commit();
    }

    void UpdateProgress(float dt)
    {
        if (!presence.Hidden) progress.Tick(dt, pet.Rig.Id);
        if ((pointsTimer -= dt) <= 0 && pendingPoints > 0 && !presence.Hidden)
        {
            fx.Burst($"+{pendingPoints} 💎", pet.HeadTop(), new Color("#7fe0ff"));
            pendingPoints = 0;
            pointsTimer = 0.6f;
        }
        if ((progressSaveTimer -= dt) <= 0)
        {
            progressSaveTimer = ProgressSaveEvery;
            FlushProgress();
        }
    }

    void FlushProgress()
    {
        if (!progressDirty) return;
        progressDirty = false;
        progress.State.Save();
        progressState = progress.State;      // 開發者重置後 State 會換成新的物件
    }

    string NameOf(string id) => rigs.TryGetValue(id, out var r) ? r.Name : id;

    void OnProgressEvent(ProgressEvent e)
    {
        switch (e.Type)
        {
            case "points":
                pendingPoints += e.Amount;
                return;
            case "levelUp":
                if (e.Char == pet.Rig.Id) fx.Burst($"❤ Lv{e.Level}!", pet.HeadTop(), new Color("#ff6b8a"), 28);
                bubble.Say($"❤ {NameOf(e.Char!)} 好感度 Lv{e.Level}！", 3.5f);
                sfx.Play("chime");
                break;
            case "unlock":
                var parts = e.Key!.Split(':', 2);
                var target = parts[1].Split('/');
                var text = parts[0] == "character"
                    ? $"🔓 解鎖新角色：{NameOf(target[0])}！"
                    : $"🔓 解鎖 {NameOf(target[0])} 的{rigs[target[0]].Forms?.GetValueOrDefault(target[1])?.Label ?? target[1]}型態！";
                bubble.Say(text, 4.5f);
                sfx.Play("chime");
                RefreshLocks();
                break;
            case "buy":
                bubble.Say($"🛒 買到「{shop.Item(e.ItemId!)?.Label}」！", 3f);
                ApplyCosmetics();
                RefreshLocks();
                break;
            case "equip":
                ApplyCosmetics();
                break;
        }
        progressDirty = true;
        FlushProgress();         // 重要事件立刻存檔
        if (hub.Visible) hub.RefreshAll();
    }

    // ---------- 鎖定 ----------

    string? ActionLockHint(string action) =>
        shop.ActionItem(action) is { } id && !shop.Owns(id) ? $"商店商品：💎 {shop.Item(id)!.Price}（選了打開商店）" : null;

    string? FormLockHint(string id, string form) =>
        progress.FormUnlocked(id, form, ContentLoader.DefaultForm(rigs[id])) ? null : progress.LockHint($"form:{id}/{form}", NameOf);

    void RefreshLocks()
    {
        tray.SetCharacterLocks(id => progress.CharacterUnlocked(id) ? null : progress.LockHint($"character:{id}", NameOf));
        var cur = pet.Rig.Id;
        tray.SetForms(rigs[cur].Forms, FormOf(cur), form => FormLockHint(cur, form));
        // 正在用的型態被鎖住（開發者重置進度）：換回預設型態
        if (FormOf(cur) is { } f && FormLockHint(cur, f) != null && ContentLoader.DefaultForm(rigs[cur]) is { } def) ChooseForm(def);
    }

    void SwitchCharacter(string id)
    {
        pet.SetCharacter(RigOf(id));
        settings.Character = id;
        tray.SetCharacter(id);
        settings.Save();
    }

    // ---------- 商店外觀 ----------

    void ApplyCosmetics()
    {
        if (shop.EquippedItem("fx") is { Symbol: { } symbol } fxItem)
            fx.SetHeartStyle(symbol, new Color(fxItem.Color ?? "#ff6b8a"));
        bubble.SetStyle(shop.EquippedItem("bubble")?.Style ?? "classic");
        var ball = shop.EquippedItem("ball")?.Prop ?? "volleyball";
        volley.SetBallSkin(allProps.GetValueOrDefault(ball) ?? allProps["volleyball"]);
    }

    // 預覽：動作直接播一次、特效冒出來、泡泡講一句（3 秒後換回原本的款式）
    void PreviewItem(string id, ProgressionConfig.ShopItem it)
    {
        if (it.Action != null) behavior.Force(it.Action);
        else if (it.Symbol != null)
            for (var i = 0; i < 4; i++)
                GetTree().CreateTimer(i * 0.15).Timeout += () => fx.Burst(it.Symbol, pet.HeadTop(), new Color(it.Color ?? "#ffffff"));
        else if (it.Style != null)
        {
            bubble.SetStyle(it.Style);
            behavior.Say("greet");
            GetTree().CreateTimer(3).Timeout += ApplyCosmetics;
        }
        else if (it.Prop != null) fx.Burst(it.Label, pet.HeadTop(), Colors.White);
    }

    // ---------- 設定（系統匣與設定視窗共用）----------

    void SetOption(string key, bool on)
    {
        switch (key)
        {
            case "auto": behavior.Auto = settings.Auto = on; break;
            case "sound": sfx.Enabled = settings.Sound = on; break;
            case "autostart": Autostart.Set(on); break;
            case "fullscreenHide": presence.AutoHideFullscreen = settings.AutoHideFullscreen = on; break;
            case "fitScreen": settings.FitScreen = on; pet.SetScale(PetScale(settings.Size)); break;
            case "devMode": settings.DevMode = on; hub.SetDevMode(on); break;
        }
        settings.Save();
        tray.SetOption(key, on);
    }

    void SetSize(float size)
    {
        pet.SetScale(PetScale(size));
        settings.Size = size;
        settings.Save();
        tray.SetSize(size);
    }
}
