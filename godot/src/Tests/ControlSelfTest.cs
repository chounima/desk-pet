// --selftest=control：「誰在控制角色」的優先順序規則（假時鐘，不用開畫面）
using System.Collections.Generic;
using DeskPet.Game.Control;
using Godot;

namespace DeskPet.Tests;

public static class ControlSelfTest
{
    public static int Run()
    {
        var failures = new List<string>();
        void Check(bool ok, string what) { if (!ok) failures.Add(what); }

        double now = 0;
        var started = new List<string>();
        var canAct = true;
        var c = new ControlArbiter(a => { if (canAct) started.Add(a); return canAct; }, () => now);

        // 表情：優先順序高的贏，一樣高時後來的贏
        c.Request("rule", Channel.Face, "happy", Priority.Reaction, 2);
        c.Request("hotkey", Channel.Face, "angry", Priority.Hotkey, 1);
        Check(c.Winner(Channel.Face) == "angry", "快捷鍵（40）要蓋過反應（10）");
        c.Request("rule2", Channel.Face, "shy", Priority.Hotkey, 1);
        Check(c.Winner(Channel.Face) == "shy", "一樣優先時後來的贏");
        now = 1.5;
        Check(c.Winner(Channel.Face) == "happy", "時間到的請求要消失，換回下一個");
        now = 2.5;
        Check(c.Winner(Channel.Face) == null, "全部過期後沒有表情請求");

        // 只在這一幀有效的請求：EndFrame 後清掉；有時間的保留
        c.Request("talk", Channel.Mouth, "surprised", Priority.Talking);
        c.Request("rule", Channel.Face, "happy", Priority.Reaction, 5);
        Check(c.WholeFace() == "surprised", "講話（20）蓋過反應（10）的整張臉");
        c.EndFrame();
        Check(c.Winner(Channel.Mouth) == null, "這一幀的嘴型要在 EndFrame 清掉");
        Check(c.WholeFace() == "happy", "嘴型清掉後回到反應的臉");
        c.Request("hotkey", Channel.Face, "angry", Priority.Hotkey, 1);
        c.Request("talk", Channel.Mouth, "surprised", Priority.Talking);
        Check(c.WholeFace() == "angry", "快捷鍵表情（40）蓋過講話（20）");
        c.Release("hotkey", Channel.Face);
        Check(c.WholeFace() == "surprised", "放開快捷鍵後換回講話");
        c.EndFrame();

        // 眼、嘴是不同管道，可以同時生效
        c.Request("gaze", Channel.Eyes, "look", Priority.Behavior);
        c.Request("talk", Channel.Mouth, "a", Priority.Talking);
        Check(c.Winner(Channel.Eyes) == "look" && c.Winner(Channel.Mouth) == "a", "眼和嘴同時生效");
        c.EndFrame();

        // 動作：比較高優先的動作進行中，低的做不了；時間到就可以
        Check(c.TryAct("leave", "wave", Priority.Minigame, 2), "離開的揮手要能開始");
        Check(!c.TryAct("chat", "jump", Priority.Reaction, 1), "揮手中聊天觸發不能打斷");
        Check(c.ActionOwner == "leave", "目前動作屬於 leave");
        now += 2.1;
        Check(c.TryAct("chat", "jump", Priority.Reaction, 1), "揮手結束後聊天觸發可以做");
        Check(c.TryAct("hotkey", "cheer", Priority.Hotkey, 1), "高優先可以打斷低優先");
        canAct = false;
        Check(!c.TryAct("hotkey2", "dance", Priority.Minigame, 1), "角色做不了（在空中）時回傳 false");
        Check(c.ActionOwner == "hotkey", "做不了的請求不會搶走動作");
        Check(string.Join(",", started) == "wave,jump,cheer", $"開始過的動作：{string.Join(",", started)}");

        foreach (var f in failures) GD.Print($"[control] ✗ {f}");
        GD.Print(failures.Count == 0 ? "[control] 全部通過" : $"[control] ✗ {failures.Count} 個失敗");
        return failures.Count == 0 ? 0 : 1;
    }
}
