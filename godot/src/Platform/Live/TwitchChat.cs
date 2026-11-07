// Twitch 聊天室（匿名唯讀，不用登入也不用申請）：有人留言就出現客人，留言顯示在泡泡裡
//
// 連 wss://irc-ws.chat.twitch.tv，用 justinfanXXXX 匿名帳號 JOIN 頻道，讀 PRIVMSG。
// 訂閱、贈訂、揪團（USERNOTICE）當成 🎉 表情反應。
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DeskPet.Content;
using DeskPet.Signals;

namespace DeskPet.Platform.Live;

public sealed class TwitchChat : PresenceSource
{
    static readonly Uri Server = new("wss://irc-ws.chat.twitch.tv:443");

    readonly string channel;
    CancellationTokenSource? cts;

    /// <param name="channel">頻道名稱，也接受 https://www.twitch.tv/xxx 或 #xxx</param>
    public TwitchChat(string channel) => this.channel = Normalize(channel);

    public override string Id => "twitch";
    public override bool ChatOnly => true;

    public static string Normalize(string channel)
    {
        var c = channel.Trim().TrimEnd('/');
        if (c.LastIndexOf('/') is var slash and >= 0) c = c[(slash + 1)..];
        return c.TrimStart('#').ToLowerInvariant();
    }

    public override void Start()
    {
        cts = new CancellationTokenSource();
        var token = cts.Token;
        Task.Run(() => Run(token), token);
    }

    public override void Stop() => cts?.Cancel();

    // 斷線就重連（等待時間 2、4、8…最多 60 秒）
    async Task Run(CancellationToken ct)
    {
        var wait = 2;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                Status = I18n.T("live.connecting");
                using var ws = new ClientWebSocket();
                await ws.ConnectAsync(Server, ct);
                await Send(ws, "CAP REQ :twitch.tv/tags twitch.tv/commands", ct);
                await Send(ws, "PASS SCHMOOPIIE", ct);
                await Send(ws, $"NICK justinfan{Random.Shared.Next(10000, 99999)}", ct);
                await Send(ws, $"JOIN #{channel}", ct);
                wait = 2;
                await Receive(ws, ct);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception e)
            {
                Status = I18n.T("live.error", e.Message);
            }
            if (ct.IsCancellationRequested) return;
            await Task.Delay(TimeSpan.FromSeconds(wait), ct).ContinueWith(_ => { });
            wait = Math.Min(wait * 2, 60);
        }
    }

    static Task Send(ClientWebSocket ws, string line, CancellationToken ct) =>
        ws.SendAsync(Encoding.UTF8.GetBytes(line + "\r\n"), WebSocketMessageType.Text, true, ct);

    async Task Receive(ClientWebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        var pending = new StringBuilder();
        var bytes = new List<byte>();
        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var r = await ws.ReceiveAsync(buffer, ct);
            if (r.MessageType == WebSocketMessageType.Close) return;
            bytes.AddRange(new ArraySegment<byte>(buffer, 0, r.Count));
            if (!r.EndOfMessage) continue;
            pending.Append(Encoding.UTF8.GetString(bytes.ToArray()));
            bytes.Clear();
            var text = pending.ToString();
            var end = text.LastIndexOf("\r\n", StringComparison.Ordinal);
            if (end < 0) continue;
            pending.Clear().Append(text[(end + 2)..]);
            foreach (var line in text[..end].Split("\r\n"))
            {
                if (line.StartsWith("PING")) await Send(ws, "PONG" + line[4..], ct);
                else if (Handle(line) == false) return;       // RECONNECT：伺服器要我們重連
            }
        }
    }

    /// <summary>處理一行 IRC 訊息；回傳 false 表示要重新連線</summary>
    public bool Handle(string line)
    {
        if (Parse(line) is not { } m) return true;
        switch (m.Command)
        {
            case "ROOMSTATE":
                Status = I18n.T("live.connected", "#" + channel);
                break;
            case "PRIVMSG":
                var text = m.Text;
                if (text.StartsWith("\u0001ACTION ") && text.EndsWith('\u0001')) text = text[8..^1];     // /me 動作訊息
                Emit(new PresenceEvent(Id, PresenceKind.Chat, m.UserId, m.Name, text));
                break;
            case "USERNOTICE":
                Emit(new PresenceEvent(Id, PresenceKind.Reaction, m.UserId, m.Name, "🎉"));
                if (m.Text.Length > 0) Emit(new PresenceEvent(Id, PresenceKind.Chat, m.UserId, m.Name, m.Text));
                break;
            case "NOTICE":
                Status = m.Text;
                break;
            case "RECONNECT":
                return false;
        }
        return true;
    }

    public sealed record Message(string Command, string UserId, string Name, string Text);

    // @tags :prefix COMMAND #channel :text
    public static Message? Parse(string line)
    {
        if (line.Length == 0) return null;
        var tags = new Dictionary<string, string>();
        var rest = line;
        if (rest.StartsWith('@'))
        {
            var sp = rest.IndexOf(' ');
            if (sp < 0) return null;
            foreach (var kv in rest[1..sp].Split(';'))
                if (kv.Split('=', 2) is [var k, var v]) tags[k] = Unescape(v);
            rest = rest[(sp + 1)..];
        }
        var login = "";
        if (rest.StartsWith(':'))
        {
            var sp = rest.IndexOf(' ');
            if (sp < 0) return null;
            var prefix = rest[1..sp];
            login = prefix.Split('!')[0];
            rest = rest[(sp + 1)..];
        }
        var textAt = rest.IndexOf(" :", StringComparison.Ordinal);
        var head = textAt >= 0 ? rest[..textAt] : rest;
        var text = textAt >= 0 ? rest[(textAt + 2)..] : "";
        var command = head.Split(' ')[0];
        var name = tags.GetValueOrDefault("display-name") is { Length: > 0 } dn ? dn : tags.GetValueOrDefault("login") ?? login;
        var id = tags.GetValueOrDefault("user-id") is { Length: > 0 } uid ? uid : name.ToLowerInvariant();
        return new Message(command, id, name, text);
    }

    // IRCv3 標籤值的跳脫字元
    static string Unescape(string v) =>
        v.Replace(@"\s", " ").Replace(@"\:", ";").Replace(@"\r", "\r").Replace(@"\n", "\n").Replace(@"\\", @"\");
}
