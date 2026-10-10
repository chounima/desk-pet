// 「Desk Pet」視窗：收藏、商店、設定、開發者四個分頁（獨立的系統視窗，不受桌寵視窗點擊穿透影響）
using System.Collections.Generic;
using DeskPet.Content;
using DeskPet.Game.Progression;
using DeskPet.Save;
using Godot;

namespace DeskPet.UI.Hub;

public partial class HubWindow : Window
{
    readonly Progress progress;
    readonly TabContainer tabs = new() { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
    readonly Label points;
    float refreshTimer;

    public CollectionTab Collection { get; }
    public ShopTab Shop { get; }
    public SettingsTab Settings { get; }
    public DevTab Dev { get; }
    public MusicTab Music { get; private set; } = null!;

    public HubWindow(Progress progress, Shop shop, Settings settings, List<RigData> characters,
                     Dictionary<string, PropData> props, System.Func<bool> autostart, System.Func<string> currentChar)
    {
        this.progress = progress;
        Title = "Desk Pet";
        Size = new Vector2I(Ui.Px(560), Ui.Px(680));
        MinSize = new Vector2I(Ui.Px(420), Ui.Px(400));
        Visible = false;
        Transparent = false;
        Theme = Ui.MakeTheme();
        CloseRequested += Hide;

        string NameOf(string id) => characters.Find(r => r.Id == id)?.Name ?? id;
        Collection = new CollectionTab(progress, characters);
        Shop = new ShopTab(shop, progress, props, NameOf);
        Settings = new SettingsTab(settings, autostart);
        Dev = new DevTab(progress, currentChar);

        var root = new PanelContainer();
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var margin = new MarginContainer();
        var body = new VBoxContainer();
        points = Ui.Text("", Ui.Accent, 17);
        body.AddChild(points);
        tabs.AddChild(Collection);
        tabs.AddChild(Shop);
        tabs.AddChild(Settings);
        tabs.AddChild(Dev);
        tabs.TabChanged += _ => RefreshAll();
        body.AddChild(tabs);
        margin.AddChild(body);
        root.AddChild(margin);
        AddChild(root);
    }

    /// <summary>音樂分頁（播放器建立後才加進來，放在商店後面）</summary>
    public void AddMusicTab(Audio.Music.MusicPlayer player, Shop shop, Settings settings)
    {
        Music = new MusicTab(player, shop, settings);
        tabs.AddChild(Music);
        tabs.MoveChild(Music, Shop.GetIndex() + 1);
    }

    /// <summary>開發者分頁只在開發者模式顯示</summary>
    public void SetDevMode(bool on) => tabs.SetTabHidden(tabs.GetTabIdxFromControl(Dev), !on);

    /// <summary>tab：collection | shop | music | settings | dev；shopCategory 指定商店分類</summary>
    public void Open(string tab, string? shopCategory = null)
    {
        Control target = tab switch { "shop" => Shop, "music" => (Control?)Music ?? Shop, "settings" => Settings, "dev" => Dev, _ => Collection };
        tabs.CurrentTab = tabs.GetTabIdxFromControl(target);
        if (shopCategory != null) Shop.ShowCategory(shopCategory);
        RefreshAll();
        if (!Visible) PopupCentered();
        GrabFocus();
    }

    public void RefreshAll()
    {
        if (!Visible && tabs.CurrentTab < 0) return;
        points.Text = $"💎 {progress.State.Points}";
        var current = tabs.GetCurrentTabControl();
        if (current == Collection) Collection.Refresh();
        else if (current == Shop) Shop.Refresh();
        else if (current == Music) Music.Refresh();
        else if (current == Settings) Settings.Refresh();
        else if (current == Dev) Dev.Refresh();
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        points.Text = $"💎 {progress.State.Points}";
        // 收藏分頁的進度條每秒更新一次（陪伴時間、打字點數會一直變）
        if ((refreshTimer -= (float)delta) <= 0 && tabs.GetCurrentTabControl() == Collection)
        {
            refreshTimer = 1;
            Collection.Refresh();
        }
    }
}
