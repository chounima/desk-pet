// Discord 本機 RPC：讀「自己目前所在的語音頻道」裡有誰、誰在講話、誰靜音
//
// 透過 Discord 桌面版開的 named pipe（\\.\pipe\discord-ipc-0）溝通，不需要機器人進伺服器，
// 私訊通話、群組通話也可以。需要自己在 Discord 開發者後台建立應用程式（拿 Client ID / Secret），
// 第一次連線時 Discord 會跳出授權視窗（rpc、rpc.voice.read），之後用存下來的 token。
//
// 自己（登入的帳號）不產生客人：SelfSpeaking（IVoiceSource）讓主角色動嘴。
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using DeskPet.Content;
using DeskPet.Signals;
using DeskPet.Save;

namespace DeskPet.Platform.Live;

public sealed class DiscordRpc : PresenceSource, IVoiceSource
{
    static readonly HttpClient Http = new();
    static readonly string[] Scopes = { "rpc", "rpc.voice.read" };
    static readonly string[] VoiceEvents = { "VOICE_STATE_CREATE", "VOICE_STATE_UPDATE", "VOICE_STATE_DELETE", "SPEAKING_START", "SPEAKING_STOP" };

    readonly string clientId, clientSecret;
    string accessToken, refreshToken;
    CancellationTokenSource? cts;
    NamedPipeClientStream? pipe;
    readonly SemaphoreSlim writeLock = new(1, 1);
    readonly ConcurrentDictionary<string, TaskCompletionSource<JsonNode>> waiting = new();
    Channel<JsonNode> events = new();
    string selfId = "";
    volatile bool selfSpeaking;

    /// <summary>登入的帳號自己在講話（主角色動嘴）</summary>
    public bool SelfSpeaking => selfSpeaking;
    public float SelfLevel => -1;
    string? channelId;
    readonly HashSet<string> present = new();

    /// <summary>授權後拿到新的 token：主執行緒取走後存進設定</summary>
    public (string Access, string Refresh)? NewTokens;

    public DiscordRpc(Settings settings)
    {
        clientId = settings.DiscordClientId.Trim();
        clientSecret = settings.DiscordClientSecret.Trim();
        accessToken = settings.DiscordAccessToken;
        refreshToken = settings.DiscordRefreshToken;
    }

    public override string Id => "discord";

    public override void Start()
    {
        cts = new CancellationTokenSource();
        var token = cts.Token;
        Task.Run(() => Run(token), token);
    }

    public override void Stop()
    {
        cts?.Cancel();
        try { pipe?.Dispose(); } catch { }
    }

    // ---------- 連線（Discord 沒開就每 5 秒再試） ----------

    async Task Run(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Session(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (FatalError e)
            {
                Status = e.Message;
                return;                       // 設定錯了（Client ID、Secret）：不要一直重試
            }
            catch (Exception e)
            {
                Status = e is IOException or TimeoutException ? I18n.T("live.discordNotRunning") : I18n.T("live.error", e.Message);
            }
            LeaveAll();
            try { pipe?.Dispose(); } catch { }
            pipe = null;
            await Task.Delay(5000, ct).ContinueWith(_ => { });
        }
    }

    sealed class FatalError(string message) : Exception(message);

    async Task Session(CancellationToken ct)
    {
        pipe = await Connect(ct);
        var ev = events = new Channel<JsonNode>();
        _ = Task.Run(() => ReadLoop(pipe, ev, ct), ct);

        // 握手 → READY（裡面有登入的帳號）
        await Write(0, new JsonObject { ["v"] = 1, ["client_id"] = clientId }, ct);
        var ready = await events.Take(ct);
        if (ready["evt"]?.GetValue<string>() != "READY")
            throw new FatalError(I18n.T("live.discordBadClient", ready["data"]?["message"]?.GetValue<string>() ?? ""));
        selfId = ready["data"]?["user"]?["id"]?.GetValue<string>() ?? "";

        await Authenticate(ct);
        await Command("SUBSCRIBE", null, "VOICE_CHANNEL_SELECT", ct);
        var current = await Command("GET_SELECTED_VOICE_CHANNEL", new JsonObject(), null, ct);
        await Watch(current["data"], ct);

        while (!ct.IsCancellationRequested)
        {
            var msg = await events.Take(ct);
            var evt = msg["evt"]?.GetValue<string>();
            var data = msg["data"];
            switch (evt)
            {
                case "VOICE_CHANNEL_SELECT":
                    // 換了語音頻道（或離開）：重新抓頻道裡的人
                    var id = data?["channel_id"]?.GetValue<string>();
                    if (id == channelId) break;
                    var info = id == null ? null : (await Command("GET_CHANNEL", new JsonObject { ["channel_id"] = id }, null, ct))["data"];
                    await Watch(info, ct);
                    break;
                case "VOICE_STATE_CREATE" or "VOICE_STATE_UPDATE":
                    if (data != null) State(data);
                    break;
                case "VOICE_STATE_DELETE":
                    if (data?["user"]?["id"]?.GetValue<string>() is { } gone) Leave(gone);
                    break;
                case "SPEAKING_START" or "SPEAKING_STOP":
                    if (data?["user_id"]?.GetValue<string>() is not { } who) break;
                    if (who == selfId) selfSpeaking = evt == "SPEAKING_START";
                    else Emit(new PresenceEvent(Id, evt == "SPEAKING_START" ? PresenceKind.Speaking : PresenceKind.Silent, who));
                    break;
            }
        }
    }

    static async Task<NamedPipeClientStream> Connect(CancellationToken ct)
    {
        // 測試用：DESKPET_DISCORD_PIPE 指定假伺服器的 pipe（真的 Discord 開著時也能測，見 tools/test_discord.ps1）
        var forced = Environment.GetEnvironmentVariable("DESKPET_DISCORD_PIPE");
        var names = string.IsNullOrEmpty(forced) ? Enumerable.Range(0, 10).Select(i => $"discord-ipc-{i}") : new[] { forced };
        foreach (var name in names)
        {
            var p = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                await p.ConnectAsync(300, ct);
                return p;
            }
            catch (TimeoutException) { p.Dispose(); }
            catch (IOException) { p.Dispose(); }
        }
        throw new TimeoutException();
    }

    // ---------- 授權 ----------

    async Task Authenticate(CancellationToken ct)
    {
        if (accessToken.Length > 0 && await TryAuthenticate(accessToken, ct)) return;
        if (refreshToken.Length > 0 && await Exchange(new() { ["grant_type"] = "refresh_token", ["refresh_token"] = refreshToken }, ct)
            && await TryAuthenticate(accessToken, ct)) return;

        // 第一次：Discord 跳出授權視窗，使用者按「授權」後拿到 code，再換成 token
        if (clientSecret.Length == 0) throw new FatalError(I18n.T("live.discordNeedSecret"));
        Status = I18n.T("live.discordAuthorize");
        // RPC 授權不能帶 redirect_uri，Discord 會用開發者後台 Redirects 的第一個；後台一個都沒設會出現 Missing "redirect_uri"
        var auth = await Command("AUTHORIZE", new JsonObject
        {
            ["client_id"] = clientId,
            ["scopes"] = new JsonArray(Scopes.Select(s => (JsonNode)s!).ToArray()),
        }, null, ct, TimeSpan.FromMinutes(5));
        if (auth["evt"]?.GetValue<string>() == "ERROR")
            throw new FatalError(I18n.T("live.discordDenied", auth["data"]?["message"]?.GetValue<string>() ?? ""));
        var code = auth["data"]?["code"]?.GetValue<string>() ?? "";
        // 換 token：先不帶 redirect_uri，不行再帶後台設定的 http://localhost
        if (!await Exchange(new() { ["grant_type"] = "authorization_code", ["code"] = code }, ct))
            await Exchange(new() { ["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = "http://localhost" }, ct);
        if (accessToken.Length == 0 || !await TryAuthenticate(accessToken, ct))
            throw new FatalError(I18n.T("live.discordTokenFailed"));
    }

    async Task<bool> TryAuthenticate(string token, CancellationToken ct)
    {
        var r = await Command("AUTHENTICATE", new JsonObject { ["access_token"] = token }, null, ct);
        return r["evt"]?.GetValue<string>() != "ERROR";
    }

    // 用 code 或 refresh token 換 access token；成功就交給主執行緒存起來
    async Task<bool> Exchange(Dictionary<string, string> form, CancellationToken ct)
    {
        form["client_id"] = clientId;
        form["client_secret"] = clientSecret;
        using var res = await Http.PostAsync("https://discord.com/api/oauth2/token", new FormUrlEncodedContent(form), ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            Godot.GD.Print($"[discord] token {(int)res.StatusCode} {body}");
            return false;
        }
        var json = JsonNode.Parse(body)!;
        accessToken = json["access_token"]?.GetValue<string>() ?? "";
        refreshToken = json["refresh_token"]?.GetValue<string>() ?? refreshToken;
        NewTokens = (accessToken, refreshToken);
        return accessToken.Length > 0;
    }

    // ---------- 語音頻道 ----------

    // 開始看某個頻道（null = 不在語音頻道）：退訂舊頻道、訂閱新頻道、頻道裡的人全部出現
    async Task Watch(JsonNode? channel, CancellationToken ct)
    {
        if (channelId != null)
            foreach (var e in VoiceEvents)
                await Command("UNSUBSCRIBE", new JsonObject { ["channel_id"] = channelId }, e, ct);
        LeaveAll();
        channelId = channel?["id"]?.GetValue<string>();
        if (channelId == null)
        {
            Status = I18n.T("live.discordNoChannel");
            return;
        }
        foreach (var e in VoiceEvents)
            await Command("SUBSCRIBE", new JsonObject { ["channel_id"] = channelId }, e, ct);
        foreach (var s in channel?["voice_states"]?.AsArray() ?? new JsonArray())
            if (s != null) State(s);
        Status = I18n.T("live.connected", "🔊 " + (channel?["name"]?.GetValue<string>() is { Length: > 0 } n ? n : I18n.T("live.discordCall")));
    }

    // 語音狀態：{ nick, mute, voice_state: { self_mute, self_deaf, mute, deaf }, user: { id, username, global_name } }
    void State(JsonNode s)
    {
        var id = s["user"]?["id"]?.GetValue<string>();
        if (id == null) return;
        var name = s["nick"]?.GetValue<string>() is { Length: > 0 } nick ? nick
                 : s["user"]?["global_name"]?.GetValue<string>() is { Length: > 0 } g ? g
                 : s["user"]?["username"]?.GetValue<string>() ?? id;
        var vs = s["voice_state"];
        bool Flag(string k) => vs?[k]?.GetValue<bool>() == true;
        var muted = Flag("self_mute") || Flag("mute") || Flag("self_deaf") || Flag("deaf") || s["mute"]?.GetValue<bool>() == true;
        if (id == selfId)
        {
            if (muted) selfSpeaking = false;      // 自己不產生客人
            return;
        }
        if (present.Add(id)) Emit(new PresenceEvent(Id, PresenceKind.Join, id, name));
        Emit(new PresenceEvent(Id, muted ? PresenceKind.Mute : PresenceKind.Unmute, id, name));
    }

    void Leave(string id)
    {
        if (present.Remove(id)) Emit(new PresenceEvent(Id, PresenceKind.Leave, id));
    }

    void LeaveAll()
    {
        selfSpeaking = false;
        foreach (var id in present.ToList()) Leave(id);
    }

    // ---------- 收發封包：[op int32][長度 int32][JSON] ----------

    async Task<JsonNode> Command(string cmd, JsonObject? args, string? evt, CancellationToken ct, TimeSpan? timeout = null)
    {
        var nonce = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<JsonNode>(TaskCreationOptions.RunContinuationsAsynchronously);
        waiting[nonce] = tcs;
        var payload = new JsonObject { ["cmd"] = cmd, ["nonce"] = nonce, ["args"] = args ?? new JsonObject() };
        if (evt != null) payload["evt"] = evt;
        await Write(1, payload, ct);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(timeout ?? TimeSpan.FromSeconds(15));
        using (limit.Token.Register(() => tcs.TrySetCanceled()))
        {
            try { return await tcs.Task; }
            finally { waiting.TryRemove(nonce, out _); }
        }
    }

    async Task Write(int op, JsonNode payload, CancellationToken ct)
    {
        var body = Encoding.UTF8.GetBytes(payload.ToJsonString());
        var frame = new byte[8 + body.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame, op);
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(4), body.Length);
        body.CopyTo(frame, 8);
        await writeLock.WaitAsync(ct);
        try { await pipe!.WriteAsync(frame, ct); await pipe.FlushAsync(ct); }
        finally { writeLock.Release(); }
    }

    async Task ReadLoop(NamedPipeClientStream p, Channel<JsonNode> ev, CancellationToken ct)
    {
        var header = new byte[8];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await p.ReadExactlyAsync(header, ct);
                var op = BinaryPrimitives.ReadInt32LittleEndian(header);
                var body = new byte[BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4))];
                await p.ReadExactlyAsync(body, ct);
                var msg = JsonNode.Parse(body)!;
                // Discord 主動關閉連線：通常是 Client ID 不對
                if (op == 2) throw new FatalError(I18n.T("live.discordBadClient", msg["message"]?.GetValue<string>() ?? ""));
                if (op == 3) { await Write(4, msg, ct); continue; }                                   // ping → pong
                // 有 nonce 的是指令回應，其他是事件
                if (msg["nonce"]?.GetValue<string>() is { } n && waiting.TryRemove(n, out var tcs)) tcs.TrySetResult(msg);
                else ev.Add(msg);
            }
        }
        catch (Exception e)
        {
            ev.Fail(e);
            foreach (var tcs in waiting.Values) tcs.TrySetException(e);
        }
    }

    /// <summary>簡單的非同步佇列：讀取執行緒放進來，Session 依序取出；連線斷了 Take 會丟出例外</summary>
    sealed class Channel<T>
    {
        readonly ConcurrentQueue<T> items = new();
        readonly SemaphoreSlim count = new(0);
        volatile Exception? error;

        public void Add(T item) { items.Enqueue(item); count.Release(); }
        public void Fail(Exception e) { error = e; count.Release(); }

        public async Task<T> Take(CancellationToken ct)
        {
            await count.WaitAsync(ct);
            if (items.TryDequeue(out var item)) return item;
            throw error ?? new IOException("closed");
        }
    }
}
