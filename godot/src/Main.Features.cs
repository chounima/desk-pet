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

    void SetupFeatures(List<string> ids, Dictionary<string, Dictionary<string, ClipData>> clips, Dictionary<string, PropData> props)
    {
        host = new FeatureHost
        {
            Root = this, Pet = pet, Behavior = behavior, Control = control, Settings = settings, Tray = tray, Hub = hub, Presence = presence,
            Progress = progress, Fx = fx, Lines = lines, Rigs = rigs, CharacterIds = ids, Clips = clips, Props = props, Args = args,
            Area = Area, RigOf = RigOf, SetDriver = SetDriver, PetScale = () => PetScale(settings.Size), Dump = dumpState,
        };
        // 新功能在這裡加：AddFeature(new Features.Xxx.XxxFeature());
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
