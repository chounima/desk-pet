// 多人：客人角色（直播聊天室、Discord 語音裡的人，或從選單手動加的角色）
//
// 主角色照舊負責收集、排球、專注、彈吉他；客人只有動畫與反應，可以點、可以拖。
// 外部來源的事件統一成 PresenceEvent，在這裡變成「出現、講話、聊天、離開」。
using System.Collections.Generic;
using System.Linq;
using DeskPet.Content;
using DeskPet.Game;
using DeskPet.Game.Guests;
using DeskPet.Platform;
using DeskPet.Platform.Live;
using DeskPet.UI;
using Godot;

namespace DeskPet;

public partial class Main
{
    readonly List<Guest> guests = new();
    readonly List<PresenceSource> sources = new();
    readonly List<PresenceSource> liveSources = new();    // 依設定建立的來源（直播模式），重新連線時只換這些
    GuestConfig guestCfg = null!;
    InteractionData interactionData = null!;
    Dictionary<string, Dictionary<string, ClipData>> allClips = null!;
    Dictionary<string, PropData> guestProps = null!;
    List<string> charIds = null!;
    PopupMenu guestMenu = null!;
    Guest? menuGuest;
    int manualCount;
    readonly HashSet<string> selfSpeaking = new();    // 哪些來源說「使用者自己正在講話」（Discord、麥克風）
    readonly Mouth mainMouth = new();
    /// <summary>主角色的動作與表情由誰控制（講話、之後的聲音規則與快捷鍵…）</summary>
    Game.Control.ControlArbiter control = null!;
    readonly View.SpeechWaves mainWaves = new();

    public IReadOnlyList<Guest> Guests => guests;
    public IReadOnlyList<PresenceSource> Sources => sources;

    void SetupGuests(List<string> ids, Dictionary<string, Dictionary<string, ClipData>> clips, Dictionary<string, PropData> props)
    {
        guestCfg = GuestConfig.Load();
        interactionData = InteractionData.Load();
        allClips = clips;
        guestProps = props;
        charIds = ids;

        AddChild(mainWaves);

        // 客人的右鍵選單：揮手、換角色、請他離開
        guestMenu = new PopupMenu();
        AddChild(guestMenu);
        MenuStyle.Apply(guestMenu, DesktopWindow.UiScale);
        guestMenu.IdPressed += id =>
        {
            if (menuGuest is not { Leaving: false } g) return;
            if (id == 1) g.Do("wave");
            else if (id == 2) g.Leave();
            else if (id >= 100 && id - 100 < charIds.Count) SetGuestCharacter(g, charIds[(int)id - 100]);
        };

        var tab = new UI.Hub.GuestsTab(settings, ids.Select(id => rigs[id]).ToList(), id => progress.CharacterUnlocked(id), () => guests, () => sources);
        tab.AddRequested += AddManualGuest;
        tab.ClearRequested += ClearGuests;
        tab.LiveChanged += RestartLiveSources;
        hub.AddGuestsTab(tab);

        if (args.TryGetValue("guests", out var script)) AddSource(new ScriptSource(script));     // 測試用假來源
        if (args.TryGetValue("twitch", out var channel)) AddSource(new TwitchChat(channel));    // 測試用：不存設定
        if (args.TryGetValue("discord", out var client))       // --discord=ClientID 或 ClientID:token
            AddSource(new DiscordRpc(new Save.Settings { DiscordClientId = client.Split(':')[0], DiscordAccessToken = client.Split(':').ElementAtOrDefault(1) ?? "" }));
        if (args.TryGetValue("audio", out var audio))         // --audio=mic+apps；其他名字當成額外的會議軟體（例如 +powershell）
        {
            var parts = audio.Split('+');
            var apps = new Dictionary<string, string>(guestCfg.MeetingApps);
            foreach (var extra in parts.Except(new[] { "mic", "apps" })) apps[extra] = extra;
            AddSource(new AudioWatch(parts.Contains("mic"), parts.Length > (parts.Contains("mic") ? 1 : 0), apps, guestCfg.MicThreshold, guestCfg.AppThreshold));
        }
        RestartLiveSources();
    }

    int? maxGuestsOverride;     // --perf 用
    int MaxGuests => maxGuestsOverride ?? Mathf.Clamp(settings.MaxGuests ?? guestCfg.MaxGuests, 1, 20);

    float GuestScale() => PetScale(settings.Size) * guestCfg.Scale;

    void AddSource(PresenceSource source)
    {
        sources.Add(source);
        source.Start();
    }

    /// <summary>依設定重新連線直播／會議來源（直播模式關閉時全部斷線，來源帶來的客人也離開）</summary>
    public void RestartLiveSources()
    {
        foreach (var s in liveSources)
        {
            s.Stop();
            sources.Remove(s);
            foreach (var g in guests.Where(g => g.Source == s.Id)) g.Leave();
            if (s.Id == "discord") selfSpeaking.Remove(s.Id);
        }
        liveSources.Clear();
        // --sandbox（測試）不連設定裡的直播來源，只用命令列給的（--twitch、--discord、--audio）
        if (!settings.LiveMode || sandbox) return;
        if (!string.IsNullOrWhiteSpace(settings.TwitchChannel)) liveSources.Add(new TwitchChat(settings.TwitchChannel));
        if (!string.IsNullOrWhiteSpace(settings.DiscordClientId)) liveSources.Add(new DiscordRpc(settings));
        if (settings.MicTalk || settings.MeetingAudio)
            liveSources.Add(new AudioWatch(settings.MicTalk, settings.MeetingAudio, guestCfg.MeetingApps, guestCfg.MicThreshold, guestCfg.AppThreshold));
        foreach (var s in liveSources) AddSource(s);
    }

    /// <summary>手動加一個客人（選單「加入角色」）</summary>
    public void AddManualGuest(string charId)
    {
        manualCount++;
        AddGuest($"manual:{manualCount}", "manual", rigs[charId].Name, charId);
    }

    public void ClearGuests()
    {
        foreach (var g in guests) g.Leave();
    }

    Guest? FindGuest(string key) => guests.FirstOrDefault(g => g.Key == key && !g.Leaving);

    Guest? AddGuest(string key, string source, string name, string? charId = null)
    {
        if (FindGuest(key) is { } existing) return existing;
        var active = guests.Where(g => !g.Leaving).ToList();
        if (active.Count >= MaxGuests)
        {
            // 滿了：聊天室的客人換掉最久沒講話的那個，其他來源就不再加
            var src = sources.FirstOrDefault(s => s.Id == source);
            var oldest = active.Where(g => sources.FirstOrDefault(s => s.Id == g.Source)?.ChatOnly == true)
                               .OrderBy(g => g.LastActive).FirstOrDefault();
            // 剛出現不到 15 秒的不換（聊天很熱絡時才不會一直換人）
            if (src?.ChatOnly != true || oldest == null || Time.GetTicksMsec() / 1000.0 - oldest.LastActive < 15) return null;
            oldest.Leave();
        }
        var id = charId != null && rigs.ContainsKey(charId) ? charId : PickCharacter(key, name);
        var guest = new Guest(this, key, source, string.IsNullOrEmpty(name) ? key : name, RigOf(id), allClips, guestProps,
                              GuestScale(), Area, new Interactions(interactionData), lines, SpawnX());
        guest.SetHidden(presence.Hidden);
        guests.Add(guest);
        GD.Print($"[guest] + {guest.Name} ({key}) as {id}");
        return guest;
    }

    // 登場位置：從幾個候選位置裡挑離其他角色最遠的，才不會擠成一團
    float SpawnX()
    {
        var area = Area();
        var others = guests.Where(g => !g.Leaving).Select(g => g.Pet.X).Append(pet.X).ToList();
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
            if (settings.GuestCharacters.TryGetValue(k, out var chosen) && rigs.ContainsKey(chosen)) return chosen;
        var pool = charIds.Where(id => progress == null || progress.CharacterUnlocked(id)).ToList();
        if (pool.Count == 0) pool = charIds;
        var used = guests.Where(g => !g.Leaving).Select(g => g.Pet.Rig.Id).Append(pet.Rig.Id).ToHashSet();
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
        g.Pet.SetCharacter(RigOf(charId));
        g.SetHidden(presence.Hidden);
        if (g.Source != "manual")
        {
            settings.GuestCharacters[g.Key] = charId;      // 記住這個人要用哪個角色
            settings.Save();
        }
    }

    void HandlePresence(PresenceSource src, PresenceEvent e)
    {
        // 使用者自己：不產生客人，主角色跟著動嘴
        if (e.UserId == PresenceEvent.SelfId)
        {
            if (e.Kind == PresenceKind.Speaking) selfSpeaking.Add(src.Id);
            else if (e.Kind is PresenceKind.Silent or PresenceKind.Leave or PresenceKind.Mute) selfSpeaking.Remove(src.Id);
            return;
        }
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
                g?.Chat(e.Text, guestCfg.ChatMaxChars, guestCfg.ActionForChat(e.Text));
                break;
            case PresenceKind.Reaction:
                g ??= src.ChatOnly ? AddGuest(e.Key, e.Source, e.Name) : null;
                g?.React(e.Text, guestCfg.ActionForReaction(e.Text));
                break;
        }
        if (dumpState || src is ScriptSource)
            GD.Print($"[presence] {e.Source} {e.Kind} {e.Name}({e.UserId}) {e.Text} -> " +
                     (g == null ? "(無)" : $"{g.Pet.Rig.Id} action={g.Pet.Action} speaking={g.Speaking} muted={g.Muted}"));
    }

    void UpdateGuests(float dt)
    {
        foreach (var src in sources)
        {
            while (src.TryDequeue(out var e)) HandlePresence(src, e);
            if (src is DiscordRpc { NewTokens: { } t } d && liveSources.Contains(src))      // 測試用的連線不存
            {
                d.NewTokens = null;
                settings.DiscordAccessToken = t.Access;
                settings.DiscordRefreshToken = t.Refresh;
                settings.Save();
            }
        }

        var cursor = lookOverride ?? DesktopWindow.CursorInWindow();
        var now = Time.GetTicksMsec() / 1000.0;
        foreach (var g in guests)
        {
            g.Update(dt, cursor);
            // 聊天室的人太久沒講話就離開
            if (!g.Leaving && sources.FirstOrDefault(s => s.Id == g.Source)?.ChatOnly == true &&
                now - g.LastActive > guestCfg.ChatIdleMinutes * 60)
                g.Leave();
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

    /// <summary>主角色講話（Discord 裡的自己、麥克風）：在 behavior.Update 之後、pet.Update 之前呼叫</summary>
    void UpdateMainMouth(float dt)
    {
        var talking = mainMouth.Update(dt, pet, behavior, control, selfSpeaking.Count > 0 && !performing && !presence.Hidden);
        mainWaves.UpdateWaves(dt, talking, pet, mainMouth.Open);
    }

    void SetGuestsHidden(bool hidden)
    {
        foreach (var g in guests) g.SetHidden(hidden);
    }

    void RescaleGuests()
    {
        foreach (var g in guests) g.SetScale(GuestScale());
    }

    /// <summary>游標下的客人（後加的畫在下面，所以先找前面的）</summary>
    Guest? GuestAt(Vector2 point) => guests.FirstOrDefault(g => g.HitTest(point));

    void OpenGuestMenu(Guest g)
    {
        menuGuest = g;
        guestMenu.Clear();
        guestMenu.AddItem(g.Name, 0);
        guestMenu.SetItemDisabled(0, true);
        guestMenu.AddSeparator();
        guestMenu.AddItem(I18n.T("guests.wave"), 1);
        for (var i = 0; i < charIds.Count; i++)
        {
            guestMenu.AddRadioCheckItem(I18n.T("guests.useChar", rigs[charIds[i]].Name), 100 + i);
            guestMenu.SetItemChecked(guestMenu.GetItemIndex(100 + i), g.Pet.Rig.Id == charIds[i]);
        }
        guestMenu.AddSeparator();
        guestMenu.AddItem(I18n.T("guests.dismiss"), 2);
        guestMenu.ResetSize();
        guestMenu.Position = DisplayServer.MouseGetPosition();
        guestMenu.Popup();
    }

    void DumpGuests()
    {
        if (guests.Count == 0 && sources.Count == 0) return;
        GD.Print($"[guests] {string.Join(" ", guests.Select(g => $"{g.Name}={g.Pet.Rig.Id}/{g.Pet.Action}{(g.Speaking ? "/speaking" : "")}{(g.Muted ? "/muted" : "")}{(g.Leaving ? "/leaving" : "")}"))} " +
                 $"sources=[{string.Join(", ", sources.Select(s => $"{s.Id}:{s.Status}"))}]");
    }
}
