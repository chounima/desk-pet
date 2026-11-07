// 功能模組拿到的共用服務：由 Main 建立後交給每個 IFeature.Init
// 功能需要新的共用服務時加在這裡，不要讓功能直接讀 Main 的欄位
using System;
using System.Collections.Generic;
using DeskPet.Content;
using DeskPet.Game;
using DeskPet.Game.Progression;
using DeskPet.Save;
using DeskPet.UI;
using DeskPet.UI.Hub;
using DeskPet.View;
using Godot;

namespace DeskPet.Features;

public sealed class FeatureHost
{
    /// <summary>桌面視窗的根節點：功能的畫面節點加在這下面</summary>
    public required Node Root { get; init; }
    public required Pet Pet { get; init; }
    public required Behavior Behavior { get; init; }
    /// <summary>主角色的動作與表情請求（依優先順序，見 Game/Control/ControlArbiter.cs）</summary>
    public required Game.Control.ControlArbiter Control { get; init; }
    public required Settings Settings { get; init; }
    public required Tray Tray { get; init; }
    public required HubWindow Hub { get; init; }
    public required Presence Presence { get; init; }
    public required Progress Progress { get; init; }
    public required Fx Fx { get; init; }
    public required Lines Lines { get; init; }

    /// <summary>所有角色（含自訂角色）與清單順序</summary>
    public required IReadOnlyDictionary<string, RigData> Rigs { get; init; }
    public required IReadOnlyList<string> CharacterIds { get; init; }
    /// <summary>動作組 { 體型: { 動作名: clip } } 與道具</summary>
    public required Dictionary<string, Dictionary<string, ClipData>> Clips { get; init; }
    public required Dictionary<string, PropData> Props { get; init; }

    /// <summary>命令列參數（--key=value），測試用</summary>
    public required IReadOnlyDictionary<string, string> Args { get; init; }

    /// <summary>活動範圍（視窗座標），底邊 = 地面</summary>
    public required Func<Rect2> Area { get; init; }
    /// <summary>角色某個型態的骨架（依使用者選的型態）</summary>
    public required Func<string, RigData> RigOf { get; init; }
    /// <summary>換主角色的驅動者（例如半身模式原地不動）；null = 換回桌面漫遊</summary>
    public required Action<Game.Drivers.IPetDriver?> SetDriver { get; init; }
    /// <summary>主角色目前的顯示大小</summary>
    public required Func<float> PetScale { get; init; }
    /// <summary>--dump：除錯輸出開著</summary>
    public bool Dump { get; init; }
}
