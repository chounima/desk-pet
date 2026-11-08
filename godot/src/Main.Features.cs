// 功能模組：建立 FeatureHost、在主迴圈的固定位置呼叫每個 IFeature（見 Features/IFeature.cs、docs/ARCHITECTURE.md）
using System.Collections.Generic;
using System.Linq;
using DeskPet.Content;
using DeskPet.Features;
using Godot;

namespace DeskPet;

public partial class Main
{
    readonly List<IFeature> features = new();
    FeatureHost host = null!;
    Features.Guests.GuestsFeature guestsFeature = null!;

    void SetupFeatures(List<string> ids, Dictionary<string, Dictionary<string, ClipData>> clips, Dictionary<string, PropData> props)
    {
        host = new FeatureHost
        {
            Root = this, Pet = pet, Behavior = behavior, Control = control, Settings = settings, Tray = tray, Hub = hub, Presence = presence,
            Progress = progress, Fx = fx, Lines = lines, Rigs = rigs, CharacterIds = ids, Clips = clips, Props = props, Args = args,
            Area = Area, RigOf = RigOf, SetDriver = SetDriver, HidePet = SetPetHiddenBy, Voice = voice, Sandbox = sandbox, PetScale = () => PetScale(settings.Size), Dump = dumpState,
        };
        // 新功能在這裡加：AddFeature(new Features.Xxx.XxxFeature());
        AddFeature(guestsFeature = new Features.Guests.GuestsFeature());
        AddFeature(new Features.Vtuber.VtuberFeature());
    }

    // 功能模組要求先把桌面上的主角色藏起來（例如半身模式）：{ 來源 }
    readonly HashSet<string> petHiders = new();

    void SetPetHiddenBy(string source, bool hide)
    {
        if (hide ? petHiders.Add(source) : petHiders.Remove(source)) ApplyPetVisibility();
    }

    /// <summary>
    /// 桌面上的主角色藏起來了嗎：全螢幕自動隱藏、使用者暫時隱藏、功能模組要求（例如半身模式）。
    /// 藏起來時，主角色相關的東西全部停：畫面、特效、泡泡、聲波、台詞音效、番茄鐘倒數、彈吉他、排球、日常反應、收集計時、動嘴、點擊。
    /// 新增主角色相關的東西時，都要看這個（不要只看 presence.Hidden）
    /// </summary>
    bool PetHidden => presence.Hidden || petHiders.Count > 0;

    void ApplyPetVisibility()
    {
        var hide = PetHidden;
        pet.View.Visible = !hide;
        attachments.SetHidden(hide);
        if (!hide) return;
        drag = null;
        if (volley.Active) { volley.SetActive(false); tray.SetVolley(false); }
        focusView.Visible = false;
    }

    /// <summary>換主角色的驅動者；null = 換回桌面漫遊（Behavior）</summary>
    void SetDriver(Game.Drivers.IPetDriver? next)
    {
        driver = next ?? behavior;
        drag = null;
    }

    void AddFeature(IFeature feature)
    {
        features.Add(feature);
        feature.Init(host);
    }

    void FeaturesBeforeBehavior(float dt) { foreach (var f in features) f.BeforeBehavior(dt); }
    void FeaturesAfterBehavior(float dt) { foreach (var f in features) f.AfterBehavior(dt); }
    void FeaturesAfterPet(float dt) { foreach (var f in features) f.AfterPet(dt); }
    bool FeaturesWantMouse(Vector2 cursor) => features.Any(f => f.WantsMouse(cursor));
    bool FeaturesHandleInput(InputEvent e) => features.Any(f => f.HandleInput(e));
    void FeaturesHiddenChanged(bool hidden) { foreach (var f in features) f.OnHiddenChanged(hidden); }
    void FeaturesDisplayChanged() { foreach (var f in features) f.OnDisplayChanged(); }
    void FeaturesShutdown() { foreach (var f in features) f.Shutdown(); }

    void DumpFeatures()
    {
        foreach (var f in features)
            if (f.DumpInfo() is { } info) GD.Print($"[{f.Id}] {info}");
    }
}
