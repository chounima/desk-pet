// 誰在控制角色：動作與表情的請求都經過這裡，依優先順序決定誰生效（每個角色一個）
//
// 管道：動作（Action）、整張臉（Face）、眼睛（Eyes）、眉毛（Brows）、嘴巴（Mouth）。
// 眼、眉、嘴可以同時生效（之後的分層表情）；只有整張臉的角色，Face 和 Mouth 取優先順序高的那個。
// 請求可以有時間（秒），或只在這一幀有效（seconds <= 0，例如講話時每幀請求一次嘴型）。
// 規則見 docs/ARCHITECTURE.md；之後的聲音規則、快捷鍵表情都透過這裡，不直接改 Pet.FaceOverride。
using System;
using System.Collections.Generic;
using System.Linq;

namespace DeskPet.Game.Control;

public enum Channel { Action, Face, Eyes, Brows, Mouth }

/// <summary>優先順序：數字大的蓋過小的；一樣大時後來的蓋過先來的</summary>
public static class Priority
{
    public const int Behavior = 0;      // 自動行為、點擊反應（Behavior 自己的表情）
    public const int Reaction = 10;     // 聊天關鍵字、表情反應
    public const int Talking = 20;      // 講話動嘴
    public const int Hotkey = 40;       // 快捷鍵表情（之後的半身模式）
    public const int Minigame = 50;     // 小遊戲（排球、彈吉他）
}

public sealed class ControlArbiter
{
    sealed record Entry(string Source, string Value, int Priority, double Until, long Order);

    readonly Func<string, bool> startAction;
    readonly Func<double> clock;
    readonly Dictionary<Channel, Dictionary<string, Entry>> requests = new();
    long order;
    Entry? actionOwner;

    /// <param name="startAction">開始做某個動作；做不到（例如在空中、小遊戲中）回傳 false</param>
    /// <param name="clock">目前時間（秒）；測試時可以換成假的時鐘</param>
    public ControlArbiter(Func<string, bool> startAction, Func<double> clock)
    {
        this.startAction = startAction;
        this.clock = clock;
        foreach (var ch in Enum.GetValues<Channel>()) requests[ch] = new();
    }

    /// <summary>請求表情類管道（Face、Eyes、Brows、Mouth）；同一個來源再請求會蓋掉自己前一次的</summary>
    public void Request(string source, Channel channel, string value, int priority, float seconds = 0)
    {
        if (channel == Channel.Action) throw new ArgumentException("動作用 TryAct");
        requests[channel][source] = new Entry(source, value, priority, seconds > 0 ? clock() + seconds : -1, ++order);
    }

    public void Release(string source, Channel channel) => requests[channel].Remove(source);

    /// <summary>
    /// 請求做某個動作：目前沒有更高優先的動作在進行，才會真的開始。回傳有沒有開始。
    /// seconds = 這個動作佔用多久（期間優先順序比它低的請求做不了）
    /// </summary>
    public bool TryAct(string source, string action, int priority, float seconds)
    {
        var now = clock();
        if (actionOwner != null && now < actionOwner.Until && priority < actionOwner.Priority) return false;
        if (!startAction(action)) return false;
        actionOwner = new Entry(source, action, priority, now + seconds, ++order);
        return true;
    }

    /// <summary>目前佔用動作的來源（沒有就 null）</summary>
    public string? ActionOwner => actionOwner != null && clock() < actionOwner.Until ? actionOwner.Source : null;

    /// <summary>某個管道目前生效的值（優先順序最高、一樣高時最新的）</summary>
    public string? Winner(Channel channel) => Top(channel)?.Value;

    Entry? Top(Channel channel)
    {
        var now = clock();
        var live = requests[channel];
        foreach (var expired in live.Values.Where(r => r.Until >= 0 && now >= r.Until).Select(r => r.Source).ToList())
            live.Remove(expired);
        return live.Values.OrderByDescending(r => r.Priority).ThenByDescending(r => r.Order).FirstOrDefault();
    }

    /// <summary>
    /// 只有整張臉的角色要顯示哪張臉：Face 與 Mouth 取優先順序高的（一樣高時取 Mouth，講話比較明顯）；
    /// 都沒有請求就回傳 null（用 Behavior 自己的表情）
    /// </summary>
    public string? WholeFace()
    {
        var face = Top(Channel.Face);
        var mouth = Top(Channel.Mouth);
        if (face == null) return mouth?.Value;
        if (mouth == null) return face.Value;
        return mouth.Priority >= face.Priority ? mouth.Value : face.Value;
    }

    /// <summary>每幀最後呼叫：清掉「只在這一幀有效」的請求</summary>
    public void EndFrame()
    {
        foreach (var live in requests.Values)
            foreach (var source in live.Values.Where(r => r.Until < 0).Select(r => r.Source).ToList())
                live.Remove(source);
    }
}
