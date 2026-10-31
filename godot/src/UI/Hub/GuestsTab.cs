// 多人分頁：直播／會議模式的連線設定（Twitch、Discord）、手動加入角色、目前在場的客人
using System;
using System.Collections.Generic;
using System.Linq;
using DeskPet.Content;
using DeskPet.Game.Guests;
using DeskPet.Save;
using Godot;

namespace DeskPet.UI.Hub;

public partial class GuestsTab : ScrollContainer
{
    /// <summary>手動加入某個角色</summary>
    public event Action<string>? AddRequested;
    public event Action? ClearRequested;
    /// <summary>連線設定改了（直播模式開關、套用）：重新連線</summary>
    public event Action? LiveChanged;

    readonly Settings settings;
    readonly List<RigData> characters;
    readonly Func<string, bool> unlocked;
    readonly Func<IReadOnlyList<Guest>> guests;
    readonly Func<IReadOnlyList<PresenceSource>> sources;
    readonly VBoxContainer body = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
    VBoxContainer status = null!, present = null!;
    string shown = "";

    public GuestsTab(Settings settings, List<RigData> characters, Func<string, bool> unlocked,
                     Func<IReadOnlyList<Guest>> guests, Func<IReadOnlyList<PresenceSource>> sources)
    {
        this.settings = settings;
        this.characters = characters;
        this.unlocked = unlocked;
        this.guests = guests;
        this.sources = sources;
        Name = I18n.T("tab.guests");
        HorizontalScrollMode = ScrollMode.Disabled;
        AddChild(body);
    }

    public void Refresh()
    {
        Ui.Clear(body);

        // 直播／會議模式
        body.AddChild(Ui.Heading(I18n.T("guests.live")));
        var live = new CheckBox { Text = I18n.T("guests.live"), ButtonPressed = settings.LiveMode, FocusMode = FocusModeEnum.None };
        live.Toggled += on => { settings.LiveMode = on; settings.Save(); LiveChanged?.Invoke(); };
        body.AddChild(live);
        body.AddChild(Ui.Text(I18n.T("guests.liveHint"), Ui.Muted, 13, wrap: true));
        status = new VBoxContainer();
        body.AddChild(status);

        // Twitch
        body.AddChild(Ui.Heading(I18n.T("guests.twitch")));
        var twitch = Field(settings.TwitchChannel, "twitch.tv/…");
        body.AddChild(twitch);
        body.AddChild(Ui.Text(I18n.T("guests.twitchHint"), Ui.Muted, 13, wrap: true));
        body.AddChild(Ui.Text(I18n.T("guests.chatTip"), Ui.Muted, 13, wrap: true));

        // Discord
        body.AddChild(Ui.Heading(I18n.T("guests.discord")));
        var clientId = Field(settings.DiscordClientId, I18n.T("guests.clientId"));
        var secret = Field(settings.DiscordClientSecret, I18n.T("guests.clientSecret"));
        secret.Secret = true;
        body.AddChild(clientId);
        body.AddChild(secret);
        body.AddChild(Ui.Text(I18n.T("guests.discordHint"), Ui.Muted, 13, wrap: true));

        // 其他會議軟體：偵測音量
        body.AddChild(Ui.Heading(I18n.T("guests.audio")));
        var micTalk = new CheckBox { Text = I18n.T("guests.micTalk"), ButtonPressed = settings.MicTalk, FocusMode = FocusModeEnum.None,
                                     AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var meeting = new CheckBox { Text = I18n.T("guests.meetingAudio"), ButtonPressed = settings.MeetingAudio, FocusMode = FocusModeEnum.None,
                                     AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddChild(micTalk);
        body.AddChild(meeting);
        body.AddChild(Ui.Text(I18n.T("guests.audioHint"), Ui.Muted, 13, wrap: true));

        body.AddChild(Ui.Row(
            Ui.Button(I18n.T("guests.apply"), () =>
            {
                settings.TwitchChannel = twitch.Text.Trim();
                // 換了應用程式，舊的授權就不能用了
                if (clientId.Text.Trim() != settings.DiscordClientId) settings.DiscordAccessToken = settings.DiscordRefreshToken = "";
                settings.DiscordClientId = clientId.Text.Trim();
                settings.DiscordClientSecret = secret.Text.Trim();
                settings.MicTalk = micTalk.ButtonPressed;
                settings.MeetingAudio = meeting.ButtonPressed;
                settings.Save();
                LiveChanged?.Invoke();
            }),
            Ui.Button(I18n.T("guests.resetDiscord"), () =>
            {
                settings.DiscordAccessToken = settings.DiscordRefreshToken = "";
                settings.Save();
                LiveChanged?.Invoke();
            })));

        // 手動加入
        body.AddChild(Ui.Heading(I18n.T("guests.manual")));
        var flow = new HFlowContainer();
        foreach (var c in characters.Where(c => unlocked(c.Id)))
        {
            var id = c.Id;
            flow.AddChild(Ui.Button(I18n.T("guests.add", c.Name), () => AddRequested?.Invoke(id)));
        }
        body.AddChild(flow);
        var max = new SpinBox { MinValue = 1, MaxValue = 20, Value = settings.MaxGuests ?? 8, FocusMode = FocusModeEnum.None };
        max.ValueChanged += v => { settings.MaxGuests = (int)v; settings.Save(); };
        body.AddChild(Ui.Row(Ui.Text(I18n.T("guests.max")), max));

        present = new VBoxContainer();
        body.AddChild(present);
        body.AddChild(Ui.Button(I18n.T("guests.clear"), () => ClearRequested?.Invoke()));
        shown = "";
        Tick();
    }

    static LineEdit Field(string text, string placeholder) =>
        new() { Text = text, PlaceholderText = placeholder, SizeFlagsHorizontal = SizeFlags.ExpandFill };

    /// <summary>每秒更新連線狀態和在場名單（不重建輸入框，打字到一半不會被清掉）</summary>
    public void Tick()
    {
        if (status == null || !IsInstanceValid(status)) return;
        var src = sources().Select(s => I18n.T("guests.status", s.Id, s.Status)).ToList();
        var who = guests().Where(g => !g.Leaving)
                          .Select(g => $"{(g.Speaking ? "🎤 " : g.Muted ? "🔇 " : "· ")}{g.Name}  —  {g.Pet.Rig.Name}").ToList();
        var key = string.Join("\n", src) + "|" + string.Join("\n", who);
        if (key == shown) return;
        shown = key;
        Ui.Clear(status);
        foreach (var s in src) status.AddChild(Ui.Text(s, Ui.Accent, 13, wrap: true));
        Ui.Clear(present);
        present.AddChild(Ui.Heading(I18n.T("guests.current", who.Count)));
        if (who.Count == 0) present.AddChild(Ui.Text(I18n.T("guests.none"), Ui.Muted, 13));
        foreach (var w in who) present.AddChild(Ui.Text(w, null, 14));
    }
}
