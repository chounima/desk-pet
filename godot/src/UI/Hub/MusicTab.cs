// 音樂分頁：播放清單（內建曲＋自訂資料夾）、播放／停止、音量、隨機、循環、打開音樂資料夾
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
        Name = "🎵 音樂";
    }

    public void Refresh()
    {
        refreshing = true;
        Ui.Clear(this);
        player.RefreshTracks(id => shop.Owns($"song:{id}"));

        if (!shop.ActionOwned("guitar"))
        {
            AddChild(Ui.Text("🔒 要先在商店買「彈吉他」才能演奏", Ui.Muted, wrap: true));
            AddChild(Ui.Button("🛒 去商店", () => ShopRequested?.Invoke("action")));
        }

        var status = player.Current is { } cur
            ? $"🎸 正在彈：{cur.Title}" + (player.Bpm is { } bpm ? $"（{bpm:0} BPM）" : "")
            : "沒有在演奏";
        AddChild(Ui.Text(status, Ui.Accent));
        AddChild(Ui.Row(
            Ui.Button("▶ 開始彈", () => PlayRequested?.Invoke(null)),
            Ui.Button("⏹ 停止", () => StopRequested?.Invoke()),
            Ui.Button("📂 打開音樂資料夾", () => { MusicPlayer.EnsureFolder(); OS.ShellOpen(MusicPlayer.Folder); }),
            Ui.Button("🔄 重新整理", Refresh)));

        var volume = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.05, Value = settings.MusicVolume,
                                   SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(Ui.Px(160), 0) };
        volume.ValueChanged += v => { settings.MusicVolume = (float)v; player.Volume = (float)v; settings.Save(); };
        AddChild(Ui.Row(Ui.Text("🔊 音量"), volume));
        AddChild(Ui.Row(Check("🔀 隨機播放", settings.MusicShuffle, on => { settings.MusicShuffle = player.Shuffle = on; }),
                        Check("🔁 播完從頭", settings.MusicLoop, on => { settings.MusicLoop = player.Loop = on; })));

        AddChild(Ui.Heading("播放清單"));
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(list);
        AddChild(scroll);
        foreach (var t in player.Tracks)
        {
            var playing = player.Current?.Id == t.Id;
            var name = Ui.Text($"{(playing ? "▶ " : "")}{(t.Builtin ? "🎵" : "📁")} {t.Title}", playing ? Ui.Accent : null);
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            list.AddChild(Ui.Row(name, Ui.Button("彈這首", () => PlayRequested?.Invoke(t))));
        }
        // 還沒買的內建曲
        foreach (var (id, item) in shop.Items("music"))
            if (!shop.Owns(id))
                list.AddChild(Ui.Row(Ui.Text($"🔒 🎵 {item.Label}（商店 💎 {item.Price}）", Ui.Muted)));
        if (player.Tracks.Count == 0) list.AddChild(Ui.Text("清單是空的：去商店買曲子，或把音樂檔放進音樂資料夾", Ui.Muted, wrap: true));
        list.AddChild(Ui.Text($"自訂音樂資料夾：{MusicPlayer.Folder}", Ui.Muted, 12, wrap: true));
        refreshing = false;
    }

    CheckBox Check(string text, bool on, Action<bool> changed)
    {
        var c = new CheckBox { Text = text, ButtonPressed = on, FocusMode = FocusModeEnum.None };
        c.Toggled += v => { if (refreshing) return; changed(v); settings.Save(); };
        return c;
    }
}
