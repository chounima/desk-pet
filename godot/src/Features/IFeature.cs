// 功能模組介面：每個功能（多人、之後的半身模式…）實作這個介面，由 Main 在主迴圈的固定位置呼叫，不直接改 Main
//
// 主迴圈的順序（Main._Process）：
//   BeforeBehavior → behavior.Update → AfterBehavior → pet.Update → AfterPet → 點擊穿透
// 例如「動嘴」要改表情，必須在 behavior.Update（會重設表情）之後、pet.Update（套用表情）之前，所以放 AfterBehavior。
// 規則見 docs/ARCHITECTURE.md「功能模組」。
using Godot;

namespace DeskPet.Features;

public interface IFeature
{
    /// <summary>名稱（除錯、--dump 用）</summary>
    string Id { get; }

    /// <summary>建立：拿到共用服務，建立自己的節點、讀自己的設定、加系統匣項目或 Hub 分頁</summary>
    void Init(FeatureHost host);

    /// <summary>每幀：主角色的行為更新之前</summary>
    void BeforeBehavior(float dt) { }

    /// <summary>每幀：行為更新之後、角色姿勢與表情套用之前（要改表情、改動作的放這裡）</summary>
    void AfterBehavior(float dt) { }

    /// <summary>每幀：主角色畫完之後（自己的角色、掛件、特效放這裡）</summary>
    void AfterPet(float dt) { }

    /// <summary>游標在這個功能的東西上（要接收滑鼠，不要點擊穿透）</summary>
    bool WantsMouse(Vector2 cursor) => false;

    /// <summary>滑鼠事件；處理了就回傳 true（主角色不再處理）。主角色的點擊判定優先</summary>
    bool HandleInput(InputEvent e) => false;

    /// <summary>全螢幕自動隱藏、使用者暫時隱藏</summary>
    void OnHiddenChanged(bool hidden) { }

    /// <summary>螢幕解析度、縮放比例、角色大小改變</summary>
    void OnDisplayChanged() { }

    /// <summary>--dump 時印出的狀態（沒有就回傳 null）</summary>
    string? DumpInfo() => null;

    /// <summary>程式結束：斷線、存檔</summary>
    void Shutdown() { }
}
