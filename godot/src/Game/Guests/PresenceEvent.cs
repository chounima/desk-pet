// 外部來源（直播聊天室、Discord 語音…）傳來的「有人怎麼了」事件，統一格式後交給 Main 產生／控制客人角色
using System.Collections.Concurrent;

namespace DeskPet.Game.Guests;

public enum PresenceKind
{
    Join,       // 出現（進語音頻道）
    Leave,      // 離開
    Speaking,   // 開始講話
    Silent,     // 講完了
    Mute,       // 靜音
    Unmute,
    Chat,       // 傳了文字訊息（Text = 內容）
    Reaction,   // 表情反應、訂閱之類（Text = emoji）
}

/// <param name="Source">來源名稱（twitch、discord、script…），和 UserId 組成客人的唯一 key</param>
/// <param name="Name">顯示名稱；Leave 之類可以是空的</param>
public sealed record PresenceEvent(string Source, PresenceKind Kind, string UserId, string Name = "", string Text = "")
{
    /// <summary>使用者自己（例如 Discord 裡的自己、麥克風）：不產生客人，改成主角色講話</summary>
    public const string SelfId = "@me";

    public string Key => $"{Source}:{UserId}";
}

/// <summary>
/// 一個外部來源。連線在背景執行緒跑，事件放進佇列，主執行緒每幀用 Drain 取出。
/// </summary>
public abstract class PresenceSource
{
    readonly ConcurrentQueue<PresenceEvent> queue = new();

    /// <summary>來源名稱（同 PresenceEvent.Source）</summary>
    public abstract string Id { get; }

    /// <summary>
    /// 只會在聊天時出現的來源（直播聊天室）：沒有 Join／Leave，第一次講話就出現，太久沒講話就離開
    /// </summary>
    public virtual bool ChatOnly => false;

    /// <summary>目前狀態（顯示在「多人」分頁）</summary>
    public volatile string Status = "";

    public abstract void Start();
    public abstract void Stop();

    protected void Emit(PresenceEvent e) => queue.Enqueue(e);

    public bool TryDequeue(out PresenceEvent e) => queue.TryDequeue(out e!);
}
