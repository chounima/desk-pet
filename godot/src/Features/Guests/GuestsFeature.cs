// 多人功能模組：客人角色（直播聊天室、Discord 語音裡的人，或從選單手動加的角色）
//
// 主角色照舊負責收集、排球、專注、彈吉他；客人只有動畫與反應，可以點、可以拖、可以按右鍵。
// 外部來源的事件（Signals.PresenceEvent）在這裡變成「出現、講話、聊天、離開」。
// 使用者自己在講話（Discord、麥克風）的來源交給 FeatureHost.Voice，由主角色動嘴。
using System.Collections.Generic;
using System.Linq;
using DeskPet.Content;
using DeskPet.Game;
using DeskPet.Game.Guests;
using DeskPet.Platform;
using DeskPet.Platform.Live;
using DeskPet.Signals;
using DeskPet.UI;
using DeskPet.UI.Hub;
using Godot;

namespace DeskPet.Features.Guests;

public sealed class GuestsFeature : IFeature
{
    sealed class GuestDrag
    {
        public required Guest Guest;
        public Vector2 Start;
        public string Zone = "body";
        public bool Moved;
    }

    FeatureHost host = null!;
    readonly List<Guest> guests = new();
    readonly List<PresenceSource> sources = new();
    readonly List<PresenceSource> liveSources = new();    // 依設定建立的來源（直播模式），重新連線時只換這些
    GuestConfig cfg = null!;
    InteractionData interactionData = null!;
    PopupMenu menu = null!;
    Guest? menuGuest;
    GuestDrag? drag;
    int manualCount;

    public string Id => "guests";
    public IReadOnlyList<Guest> Guests => guests;
    public IReadOnlyList<PresenceSource> Sources => sources;

    /// <summary>--perf 用：暫時改客人上限（不改設定）</summary>
    public int? MaxGuestsOverride { get; set; }

    int MaxGuests => MaxGuestsOverride ?? Mathf.Clamp(host.Settings.MaxGuests ?? cfg.MaxGuests, 1, 20);
    float GuestScale() => host.PetScale() * cfg.Scale;
    static double Now() => Time.GetTicksMsec() / 1000.0;

    public void Init(FeatureHost host)
    {
        this.host = host;
        cfg = GuestConfig.Load();
        interactionData = InteractionData.Load();

        // 客人的右鍵選單：揮手、換角色、請他離開
        menu = new PopupMenu();
        host.Root.AddChild(menu);
        MenuStyle.Apply(menu, DesktopWindow.UiScale);
        menu.IdPressed += id =>
        {
            if (menuGuest is not { Leaving: false } g) return;
            if (id == 1) g.Do("wave");
            else if (id == 2) g.Leave();
            else if (id >= 100 && id - 100 < host.CharacterIds.Count) SetGuestCharacter(g, host.CharacterIds[(int)id - 100]);
        };

        var tab = new GuestsTab(host.Settings, host.CharacterIds.Select(id => host.Rigs[id]).ToList(), host.Progress.CharacterUnlocked, () => guests, () => sources);
        tab.AddRequested += AddManualGuest;
        tab.ClearRequested += ClearGuests;
        tab.LiveChanged += RestartLiveSources;
        host.Hub.AddTab("guests", tab, tab.Refresh, _ => tab.Tick());
        host.Tray.AddFeatureItem(I18n.T("menu.guests"), () => host.Hub.Open("guests"));

        // 測試用的來源（不存設定）
        var args = host.Args;
        if (args.TryGetValue("guests", out var script)) AddSource(new ScriptSource(script));
        if (args.TryGetValue("twitch", out var channel)) AddSource(new TwitchChat(channel));
        if (args.TryGetValue("discord", out var client))       // --discord=ClientID 或 ClientID:token
            AddSource(new DiscordRpc(new Save.Settings { DiscordClientId = client.Split(':')[0], DiscordAccessToken = client.Split(':').ElementAtOrDefault(1) ?? "" }));
        if (args.TryGetValue("audio", out var audio))         // --audio=mic+apps；其他名字當成額外的會議軟體（例如 +powershell）
        {
            var parts = audio.Split('+');
            var apps = new Dictionary<string, string>(cfg.MeetingApps);
            foreach (var extra in parts.Except(new[] { "mic", "apps" })) apps[extra] = extra;
            AddSource(new AudioWatch(parts.Contains("mic"), parts.Length > (parts.Contains("mic") ? 1 : 0), apps, cfg.MicThreshold, cfg.AppThreshold));
        }
        RestartLiveSources();
    }

    // ---------- 來源 ----------

    void AddSource(PresenceSource source)
    {
        sources.Add(source);
        if (source is IVoiceSource voice) host.Voice.Add(voice);
        source.Start();
    }

    void RemoveSource(PresenceSource source)
    {
        source.Stop();
        sources.Remove(source);
        if (source is IVoiceSource voice) host.Voice.Remove(voice);
        foreach (var g in guests.Where(g => g.Source == source.Id)) g.Leave();
    }

    /// <summary>依設定重新連線直播／會議來源（直播模式關閉時全部斷線，來源帶來的客人也離開）</summary>
    public void RestartLiveSources()
    {
        foreach (var s in liveSources) RemoveSource(s);
        liveSources.Clear();
        // --sandbox（測試）不連設定裡的直播來源，只用命令列給的（--twitch、--discord、--audio）
        var settings = host.Settings;
        if (!settings.LiveMode || host.Sandbox) return;
        if (!string.IsNullOrWhiteSpace(settings.TwitchChannel)) liveSources.Add(new TwitchChat(settings.TwitchChannel));
        if (!string.IsNullOrWhiteSpace(settings.DiscordClientId)) liveSources.Add(new DiscordRpc(settings));
        if (settings.MicTalk || settings.MeetingAudio)
            liveSources.Add(new AudioWatch(settings.MicTalk, settings.MeetingAudio, cfg.MeetingApps, cfg.MicThreshold, cfg.AppThreshold));
        foreach (var s in liveSources) AddSource(s);
    }

    // ---------- 客人 ----------

    /// <summary>手動加一個客人（「多人」分頁的「加入角色」）</summary>
    public void AddManualGuest(string charId)
    {
        manualCount++;
        AddGuest($"manual:{manualCount}", "manual", host.Rigs[charId].Name, charId);
    }

    public void ClearGuests()
    {
        foreach (var g in guests) g.Leave();
    }

    Guest? FindGuest(string key) => guests.FirstOrDefault(g => g.Key == key && !g.Leaving);

    bool IsChatOnly(string sourceId) => sources.FirstOrDefault(s => s.Id == sourceId)?.ChatOnly == true;

    Guest? AddGuest(string key, string source, string name, string? charId = null)
    {
        if (FindGuest(key) is { } existing) return existing;
        var active = guests.Where(g => !g.Leaving).ToList();
        if (active.Count >= MaxGuests)
        {
            // 滿了：聊天室的客人換掉最久沒講話的那個，其他來源就不再加
            var oldest = active.Where(g => IsChatOnly(g.Source)).OrderBy(g => g.LastActive).FirstOrDefault();
            // 剛出現不到 15 秒的不換（聊天很熱絡時才不會一直換人）
            if (!IsChatOnly(source) || oldest == null || Now() - oldest.LastActive < 15) return null;
            oldest.Leave();
        }
        var id = charId != null && host.Rigs.ContainsKey(charId) ? charId : PickCharacter(key, name);
        var guest = new Guest(host.Root, key, source, string.IsNullOrEmpty(name) ? key : name, host.RigOf(id), host.Clips, host.Props,
                              GuestScale(), host.Area, new Interactions(interactionData), host.Lines, SpawnX());
        guest.SetHidden(host.Presence.Hidden);
        guests.Add(guest);
        GD.Print($"[guest] + {guest.Name} ({key}) as {id}");
        return guest;
    }

    // 登場位置：從幾個候選位置裡挑離其他角色最遠的，才不會擠成一團
    float SpawnX()
    {
        var area = host.Area();
        var others = guests.Where(g => !g.Leaving).Select(g => g.Pet.X).Append(host.Pet.X).ToList();
        var best = area.GetCenter().X;
        var bestGap = -1f;
        for (var i = 0; i < 16; i++)
        {
            var x = area.Position.X + area.Size.X * (0.08f + 0.84f * GD.Randf());
            var gap = others.Min(o => Mathf.Abs(o - x));
            if (gap > bestGap) (best, bestGap) = (x, gap);
        }
        return best;
    }

    // 角色：設定裡指定的（依 key 或名字）> 依 key 固定挑一個（同一個人每次都是同一隻），盡量不和別人重複
    string PickCharacter(string key, string name)
    {
        foreach (var k in new[] { key, name })
            if (host.Settings.GuestCharacters.TryGetValue(k, out var chosen) && host.Rigs.ContainsKey(chosen)) return chosen;
        var ids = host.CharacterIds;
        var pool = ids.Where(host.Progress.CharacterUnlocked).ToList();
        if (pool.Count == 0) pool = ids.ToList();
        var used = guests.Where(g => !g.Leaving).Select(g => g.Pet.Rig.Id).Append(host.Pet.Rig.Id).ToHashSet();
        var start = (int)(StableHash(key) % (uint)pool.Count);
        for (var i = 0; i < pool.Count; i++)
            if (!used.Contains(pool[(start + i) % pool.Count])) return pool[(start + i) % pool.Count];
        return pool[start];
    }

    // string.GetHashCode 每次啟動都不一樣，自己算一個固定的（FNV-1a）
    static uint StableHash(string s)
    {
        var h = 2166136261u;
        foreach (var c in s) h = (h ^ c) * 16777619u;
        return h;
    }

    void SetGuestCharacter(Guest g, string charId)
    {
        g.Pet.SetCharacter(host.RigOf(charId));
        g.SetHidden(host.Presence.Hidden);
        if (g.Source != "manual")
        {
            host.Settings.GuestCharacters[g.Key] = charId;      // 記住這個人要用哪個角色
            host.Settings.Save();
        }
    }

    void HandlePresence(PresenceSource src, PresenceEvent e)
    {
        var g = FindGuest(e.Key);
        switch (e.Kind)
        {
            case PresenceKind.Join:
                g ??= AddGuest(e.Key, e.Source, e.Name, string.IsNullOrEmpty(e.Text) ? null : e.Text);
                g?.Rename(e.Name);
                break;
            case PresenceKind.Leave:
                g?.Leave();
                break;
            case PresenceKind.Speaking:
                (g ?? (src.ChatOnly ? null : AddGuest(e.Key, e.Source, e.Name)))?.SetSpeaking(true);
                break;
            case PresenceKind.Silent:
                g?.SetSpeaking(false);
                break;
            case PresenceKind.Mute or PresenceKind.Unmute:
                g?.SetMuted(e.Kind == PresenceKind.Mute);
                break;
            case PresenceKind.Chat:
                g ??= AddGuest(e.Key, e.Source, e.Name);
                g?.Rename(e.Name);
                g?.Chat(e.Text, cfg.ChatMaxChars, cfg.ForChat(e.Text, e.Key, Now()));
                break;
            case PresenceKind.Reaction:
                g ??= src.ChatOnly ? AddGuest(e.Key, e.Source, e.Name) : null;
                g?.React(e.Text, cfg.ForReaction(e.Text, e.Key, Now()));
                break;
        }
        if (host.Dump || src is ScriptSource)
            GD.Print($"[presence] {e.Source} {e.Kind} {e.Name}({e.UserId}) {e.Text} -> " +
                     (g == null ? "(無)" : $"{g.Pet.Rig.Id} action={g.Pet.Action} speaking={g.Speaking} muted={g.Muted}"));
    }

    // ---------- 每幀 ----------

    public void BeforeBehavior(float dt) => UpdateDrag();

    public void AfterPet(float dt)
    {
        foreach (var src in sources)
        {
            while (src.TryDequeue(out var e)) HandlePresence(src, e);
            if (src is DiscordRpc { NewTokens: { } t } d && liveSources.Contains(src))      // 測試用的連線不存
            {
                d.NewTokens = null;
                host.Settings.DiscordAccessToken = t.Access;
                host.Settings.DiscordRefreshToken = t.Refresh;
                host.Settings.Save();
            }
        }

        var cursor = DesktopWindow.CursorInWindow();
        var now = Now();
        foreach (var g in guests)
        {
            g.Update(dt, cursor);
            // 聊天室的人太久沒講話就離開
            if (!g.Leaving && IsChatOnly(g.Source) && now - g.LastActive > cfg.ChatIdleMinutes * 60) g.Leave();
        }
        for (var i = guests.Count - 1; i >= 0; i--)
        {
            if (!guests[i].Gone) continue;
            if (drag?.Guest == guests[i]) drag = null;
            GD.Print($"[guest] - {guests[i].Name}");
            guests[i].Free();
            guests.RemoveAt(i);
        }
    }

    // ---------- 滑鼠：點、拖、右鍵 ----------

    /// <summary>游標下的客人（後加的畫在下面，所以先找前面的）</summary>
    Guest? GuestAt(Vector2 point) => guests.FirstOrDefault(g => g.HitTest(point));

    public bool WantsMouse(Vector2 cursor) => drag != null || GuestAt(cursor) != null;

    public bool HandleInput(InputEvent e)
    {
        if (e is not InputEventMouseButton { Pressed: true } mb || GuestAt(mb.Position) is not { } g) return false;
        if (mb.ButtonIndex == MouseButton.Left && Interactions.ZoneOnPet(g.Pet, mb.Position) is { } zone)
        {
            drag = new GuestDrag { Guest = g, Start = mb.Position, Zone = zone };
            return true;
        }
        if (mb.ButtonIndex == MouseButton.Right)
        {
            OpenMenu(g);
            return true;
        }
        return false;
    }

    // 拖曳中游標可能離開可點擊區域，所以用全域查詢游標位置與按鍵狀態（同主角色）
    void UpdateDrag()
    {
        if (drag == null) return;
        var cursor = DesktopWindow.CursorInWindow();
        var b = drag.Guest.Behavior;
        if (!DesktopWindow.LeftButtonDown())
        {
            if (drag.Moved) b.Release();
            else
            {
                b.OnPetClick(drag.Zone);
                host.Fx.Heart(drag.Guest.Pet.HeadTop());
            }
            drag = null;
            return;
        }
        if (!drag.Moved && cursor.DistanceTo(drag.Start) > 5 * DesktopWindow.UiScale)
        {
            drag.Moved = true;
            b.Grab(cursor);
        }
        if (drag.Moved) b.MoveHeld(cursor);
    }

    void OpenMenu(Guest g)
    {
        menuGuest = g;
        menu.Clear();
        menu.AddItem(g.Name, 0);
        menu.SetItemDisabled(0, true);
        menu.AddSeparator();
        menu.AddItem(I18n.T("guests.wave"), 1);
        var ids = host.CharacterIds;
        for (var i = 0; i < ids.Count; i++)
        {
            menu.AddRadioCheckItem(I18n.T("guests.useChar", host.Rigs[ids[i]].Name), 100 + i);
            menu.SetItemChecked(menu.GetItemIndex(100 + i), g.Pet.Rig.Id == ids[i]);
        }
        menu.AddSeparator();
        menu.AddItem(I18n.T("guests.dismiss"), 2);
        menu.ResetSize();
        menu.Position = DisplayServer.MouseGetPosition();
        menu.Popup();
    }

    // ---------- 其他掛勾 ----------

    public void OnHiddenChanged(bool hidden)
    {
        if (hidden) drag = null;
        foreach (var g in guests) g.SetHidden(hidden);
    }

    public void OnDisplayChanged()
    {
        foreach (var g in guests) g.SetScale(GuestScale());
    }

    public string? DumpInfo() => guests.Count == 0 && sources.Count == 0 ? null :
        $"{string.Join(" ", guests.Select(g => $"{g.Name}={g.Pet.Rig.Id}/{g.Pet.Action}{(g.Speaking ? "/speaking" : "")}{(g.Muted ? "/muted" : "")}{(g.Leaving ? "/leaving" : "")}"))} " +
        $"sources=[{string.Join(", ", sources.Select(s => $"{s.Id}:{s.Status}"))}]";

    public void Shutdown()
    {
        foreach (var s in sources) s.Stop();
    }
}
