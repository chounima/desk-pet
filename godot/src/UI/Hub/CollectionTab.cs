// 收藏分頁：今日點數進度、每個角色的好感度與解鎖狀態
using System;
using System.Collections.Generic;
using System.Linq;
using DeskPet.Content;
using DeskPet.Game.Progression;
using Godot;

namespace DeskPet.UI.Hub;

public partial class CollectionTab : ScrollContainer
{
    readonly Progress progress;
    readonly List<RigData> characters;
    readonly VBoxContainer list = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };

    /// <summary>成就（Main 建立後設定）</summary>
    public Achievements? Achievements { get; set; }

    public CollectionTab(Progress progress, List<RigData> characters)
    {
        this.progress = progress;
        this.characters = characters;
        Name = I18n.T("tab.collection");
        HorizontalScrollMode = ScrollMode.Disabled;
        AddChild(list);
    }

    string NameOf(string id) => characters.FirstOrDefault(r => r.Id == id)?.Name ?? id;

    public void Refresh()
    {
        // 重建內容後捲動位置會歸零，記下來再設回去
        var scroll = ScrollVertical;
        Callable.From(() => ScrollVertical = scroll).CallDeferred();
        Ui.Clear(list);
        var s = progress.State;
        var P = progress.Cfg.Points;
        list.AddChild(Ui.Text(I18n.T("collection.streak", s.Login.Streak)));
        list.AddChild(Ui.Heading(I18n.T("collection.today")));
        foreach (var (label, kind, max) in new[] { (I18n.T("collection.typing"), "typing", P.Typing.DailyMax), (I18n.T("collection.interaction"), "interaction", P.Interaction.DailyMax), (I18n.T("collection.volley"), "volley", P.Volley.DailyMax) })
        {
            var v = s.Day.Pts.GetValueOrDefault(kind);
            list.AddChild(ProgressRow(label, (float)v / Math.Max(1, max), $"{v} / {max}"));
        }

        list.AddChild(Ui.Heading(I18n.T("collection.characters")));
        foreach (var rig in characters) list.AddChild(CharacterCard(rig));

        if (Achievements is { } ach)
        {
            list.AddChild(Ui.Heading(I18n.T("collection.achievements", ach.All.Count(a => ach.IsUnlocked(a.Id)), ach.All.Count)));
            foreach (var a in ach.All.OrderBy(a => ach.IsUnlocked(a.Id) ? 0 : 1))
            {
                var done = ach.IsUnlocked(a.Id);
                var (card, body) = Ui.Card(highlight: done, dim: !done);
                body.AddChild(Ui.Row(Ui.Text(done ? $"🏆 {a.Name}" : $"🔒 {a.Name}"), Ui.Text(a.Desc, Ui.Muted, 13)));
                var v = Math.Min(ach.Value(a.Stat), a.Goal);
                body.AddChild(ProgressRow(done ? I18n.T("collection.done") : I18n.T("collection.progress"), (float)v / Math.Max(1, a.Goal), $"{v:N0} / {a.Goal:N0}"));
                list.AddChild(card);
            }
        }
    }

    static HBoxContainer ProgressRow(string label, float ratio, string value)
    {
        var name = Ui.Text(label);
        name.CustomMinimumSize = new Vector2(Ui.Px(80), 0);
        var num = Ui.Text(value, Ui.Muted);
        num.CustomMinimumSize = new Vector2(Ui.Px(80), 0);
        num.HorizontalAlignment = HorizontalAlignment.Right;
        return Ui.Row(name, Ui.Bar(ratio), num);
    }

    Control CharacterCard(RigData rig)
    {
        var unlocked = progress.CharacterUnlocked(rig.Id);
        var (card, body) = Ui.Card(dim: !unlocked);
        if (!unlocked)
        {
            body.AddChild(Ui.Text(I18n.T("collection.unknown")));
            body.AddChild(Ui.Text($"🔒 {progress.LockHint($"character:{rig.Id}", NameOf)}", Ui.Muted, wrap: true));
            return card;
        }
        body.AddChild(Ui.Row(Ui.Text(rig.Name), Ui.Text(rig.Description, Ui.Muted, 13)));
        var lv = progress.Level(rig.Id);
        var ratio = lv.To is { } to ? (float)(lv.Xp - lv.From) / Math.Max(1, to - lv.From) : 1;
        body.AddChild(ProgressRow($"❤ Lv{lv.Level}", ratio, lv.To is { } next ? $"{lv.Xp} / {next}" : "MAX"));
        if (progress.State.Best.TryGetValue(rig.Id, out var best) && best > 0)
            body.AddChild(Ui.Text(I18n.T("collection.bestRally", best), Ui.Muted, 13));

        if (rig.Forms is { Count: > 1 } forms)
        {
            var row = new HBoxContainer();
            var first = forms.Keys.First();
            foreach (var (name, f) in forms)
            {
                var ok = progress.FormUnlocked(rig.Id, name, first);
                var tag = Ui.Text(ok ? $"✓ {f.Label}" : $"🔒 {f.Label}", ok ? null : Ui.Muted, 13);
                if (!ok) tag.TooltipText = progress.LockHint($"form:{rig.Id}/{name}", NameOf) ?? "";
                tag.MouseFilter = MouseFilterEnum.Pass;
                row.AddChild(tag);
            }
            body.AddChild(row);
        }
        return card;
    }
}
