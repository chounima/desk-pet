// --selftest=live：多人功能的規則測試（Twitch 訊息解析、頻道名稱、聊天關鍵字），不用連網路
// Discord 的協定用假伺服器測：tools/test_discord.ps1
using System;
using System.Collections.Generic;
using DeskPet.Game.Guests;
using DeskPet.Platform.Live;
using Godot;

namespace DeskPet.Tests;

public static class LiveSelfTest
{
    public static int Run()
    {
        var failures = new List<string>();
        void Check(bool ok, string what) { if (!ok) failures.Add(what); }

        // 頻道名稱：網址、#、大寫都要變成小寫名稱
        Check(TwitchChat.Normalize("https://www.twitch.tv/Shroud/") == "shroud", "Normalize 網址");
        Check(TwitchChat.Normalize("#XQC") == "xqc", "Normalize #大寫");

        // IRC 解析：標籤、顯示名稱、跳脫字元
        var m = TwitchChat.Parse("@badge-info=;display-name=小明\\sTW;user-id=123 :xiaoming!xiaoming@xiaoming.tmi.twitch.tv PRIVMSG #chan :哈囉 大家");
        Check(m is { Command: "PRIVMSG", UserId: "123", Name: "小明 TW", Text: "哈囉 大家" }, $"Parse PRIVMSG：{m}");
        var noTags = TwitchChat.Parse(":bob!bob@bob.tmi.twitch.tv PRIVMSG #chan :hi");
        Check(noTags is { Command: "PRIVMSG", UserId: "bob", Name: "bob", Text: "hi" }, $"Parse 沒有標籤：{noTags}");
        Check(TwitchChat.Parse("PING :tmi.twitch.tv")?.Command == "PING", "Parse PING");
        Check(TwitchChat.Parse("") == null, "Parse 空行");

        // 訊息 → 事件：/me 動作、訂閱 → 🎉＋訊息
        var chat = new TwitchChat("chan");
        chat.Handle("@display-name=Amy;user-id=7 :amy!amy@amy.tmi.twitch.tv PRIVMSG #chan :\u0001ACTION waves\u0001");
        Check(chat.TryDequeue(out var e1) && e1 is { Kind: PresenceKind.Chat, UserId: "7", Text: "waves" }, $"/me 動作：{e1}");
        chat.Handle("@display-name=Amy;user-id=7;msg-id=sub :tmi.twitch.tv USERNOTICE #chan :好耶");
        Check(chat.TryDequeue(out var e2) && e2 is { Kind: PresenceKind.Reaction, Text: "🎉" }, $"訂閱 → 🎉：{e2}");
        Check(chat.TryDequeue(out var e3) && e3 is { Kind: PresenceKind.Chat, Text: "好耶" }, $"訂閱附帶的訊息：{e3}");
        Check(!chat.Handle(":tmi.twitch.tv RECONNECT"), "RECONNECT 要回傳 false");

        // 聊天關鍵字（data/guests.json）：英數字要整個字相符，中日文出現就算
        var cfg = GuestConfig.Load();
        foreach (var (text, want) in new (string, string?)[]
                 {
                     ("hi大家", "wave"), ("HELLO there", "wave"), ("this is fine", null), ("哈哈哈", "cheer"),
                     ("GG", "peace"), ("!jump", "jump"), ("nicely done", null), ("要喝咖啡嗎", "coffee"), ("晚安～", "sleep"),
                 })
        {
            var got = cfg.ActionForChat(text);
            Check(got == want, $"關鍵字 「{text}」→ {got ?? "(無)"}，應該是 {want ?? "(無)"}");
        }
        Check(cfg.ActionForReaction("👍") == "peace", "表情 👍");
        Check(cfg.ActionForReaction("❤️") == "cheer", "表情 ❤️（含 FE0F）");
        Check(cfg.ActionForReaction("🦄") == cfg.Reactions["*"], "其他表情用 *");

        foreach (var f in failures) GD.Print($"[live] ✗ {f}");
        GD.Print(failures.Count == 0 ? "[live] 全部通過" : $"[live] ✗ {failures.Count} 個失敗");
        return failures.Count == 0 ? 0 : 1;
    }
}
