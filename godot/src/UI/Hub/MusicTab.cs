// 音樂分頁：播放清單（內建曲＋自訂資料夾）、播放／停止、音量、隨機、循環、打開音樂資料夾
using DeskPet.Content;
using System;
using DeskPet.Audio.Music;
using DeskPet.Game.Progression;
using DeskPet.Save;
using Godot;

namespace DeskPet.UI.Hub;

public partial class MusicTab : VBoxContainer
{
    readonly MusicPlayer player;
    readonly Shop shop;
    readonly Settings settings;
    bool refreshing;

    /// <summary>要角色開始彈某首（null = 從清單第一首）</summary>
    public event Action<Track?>? PlayRequested;
    public event Action? StopRequested;
    /// <summary>還沒買吉他或曲子：打開商店的分類</summary>
    public event Action<string>? ShopRequested;

    public MusicTab(MusicPlayer player, Shop shop, Settings settings)
    {
        this.player = player;
        this.shop = shop;
        this.settings = settings;
        Name = I18n.T("tab.music");
    }

    public void Refresh()
    {
        refreshing = true;
        Ui.Clear(this);
        player.RefreshTracks(id => shop.Owns($"song:{id}"));

        if (!shop.ActionOwned("guitar"))
        {
            AddChild(Ui.Text(I18n.T("music.needGuitar"), Ui.Muted, wrap: true));
            AddChild(Ui.Button(I18n.T("music.toShop"), () => ShopRequested?.Invoke("action")));
        }

        var status = player.Current is { } cur
            ? I18n.T("music.playing", cur.Title) + (player.Bpm is { } bpm ? I18n.T("music.bpm", bpm) : "")
            : I18n.T("music.idle");
        AddChild(Ui.Text(status, Ui.Accent));
        AddChild(Ui.Row(
            Ui.Button(I18n.T("music.start"), () => PlayRequested?.Invoke(null)),
            Ui.Button(I18n.T("music.stop"), () => StopRequested?.Invoke()),
            Ui.Button(I18n.T("music.folder"), () => { MusicPlayer.EnsureFolder(); OS.ShellOpen(MusicPlayer.Folder); }),
            Ui.Button(I18n.T("music.refresh"), Refresh)));

        var volume = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.05, Value = settings.MusicVolume,
                                   SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(Ui.Px(160), 0) };
        volume.ValueChanged += v => { settings.MusicVolume = (float)v; player.Volume = (float)v; settings.Save(); };
        AddChild(Ui.Row(Ui.Text(I18n.T("music.volume")), volume));
        AddChild(Ui.Row(Check(I18n.T("music.shuffle"), settings.MusicShuffle, on => { settings.MusicShuffle = player.Shuffle = on; }),
                        Check(I18n.T("music.loop"), settings.MusicLoop, on => { settings.MusicLoop = player.Loop = on; })));

        AddChild(Ui.Heading(I18n.T("music.playlist")));
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(list);
        AddChild(scroll);
        foreach (var t in player.Tracks)
        {
            var playing = player.Current?.Id == t.Id;
            var name = Ui.Text($"{(playing ? "▶ " : "")}{(t.Builtin ? "🎵" : "📁")} {t.Title}", playing ? Ui.Accent : null);
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            list.AddChild(Ui.Row(name, Ui.Button(I18n.T("music.playThis"), () => PlayRequested?.Invoke(t))));
        }
        // 還沒買的內建曲
        foreach (var (id, item) in shop.Items("music"))
            if (!shop.Owns(id))
                list.AddChild(Ui.Row(Ui.Text(I18n.T("music.lockedSong", item.Label, item.Price), Ui.Muted)));
        if (player.Tracks.Count == 0) list.AddChild(Ui.Text(I18n.T("music.empty"), Ui.Muted, wrap: true));
        list.AddChild(Ui.Text(I18n.T("music.folderPath", MusicPlayer.Folder), Ui.Muted, 12, wrap: true));
        refreshing = false;
    }

    CheckBox Check(string text, bool on, Action<bool> changed)
    {
        var c = new CheckBox { Text = text, ButtonPressed = on, FocusMode = FocusModeEnum.None };
        c.Toggled += v => { if (refreshing) return; changed(v); settings.Save(); };
        return c;
    }
}
