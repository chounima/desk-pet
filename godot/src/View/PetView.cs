// 把角色部件畫成 Sprite2D，每幀套用骨骼變換（移植自 preview/js/view/renderer.js）
// 另外提供像素級點擊判定與外框計算（給點擊穿透區域用）。
using System.Collections.Generic;
using System.Linq;
using DeskPet.Content;
using Godot;

namespace DeskPet.View;

public partial class PetView : Node2D
{
    const float Oversample = 2f;      // 貼圖解析度 = 顯示大小 × 2
    static readonly Color FarTint = new(0.82f, 0.82f, 0.82f);

    sealed class Item
    {
        public Sprite2D Sprite = null!;
        public string Bone = "";
        public Transform2D? Offset;       // 道具相對骨骼的位移
        public float[] Box = null!;
        public Image Image = null!;
        public SlotData? Slot;
        public string? CurrentAlt;
    }

    readonly RigData rig;
    readonly float res;
    readonly List<Item> items = new();
    readonly Dictionary<string, Item> propItems = new();
    Item? faceItem;
    string face = "idle";
    string propsKey = "";

    // Godot 需要無參數建構子（引擎重新載入腳本時使用），實際使用請走下面的建構子
    public PetView() { rig = new RigData(); res = 1; }

    public PetView(RigData rig, Dictionary<string, PropData> props, float displayScale)
    {
        this.rig = rig;
        res = displayScale * Oversample;
        var entries = new List<(float z, Item item)>();

        foreach (var slot in rig.Slots)
        {
            var file = slot.Variants != null ? slot.Variants["idle"] : slot.File!;
            var item = MakeItem(rig.BasePath + file, slot.Box, slot.Bone, null);
            item.Slot = slot;
            if (slot.Far) item.Sprite.Modulate = FarTint;
            if (slot.Variants != null) faceItem = item;
            entries.Add((slot.Z, item));
        }

        // 道具以 Q 版身材設計，依角色腿長等比放大
        var s = rig.RootScale;
        foreach (var (name, p) in props)
        {
            var o = rig.PropOverrides.GetValueOrDefault(name);      // 角色個別的位置／大小修正
            var k = s * (o?.Scale ?? 1);
            var offset = new Transform2D(new Vector2(k, 0), new Vector2(0, k), new Vector2((p.X + (o?.Dx ?? 0)) * s, (p.Y + (o?.Dy ?? 0)) * s));
            var item = MakeItem(p.Path, p.Box, p.Bone, offset);
            item.Sprite.Visible = false;
            propItems[name] = item;
            entries.Add((p.Z, item));
        }

        foreach (var (_, item) in entries.OrderBy(e => e.z))
        {
            AddChild(item.Sprite);
            items.Add(item);
        }
    }

    Item MakeItem(string path, float[] box, string bone, Transform2D? offset)
    {
        var tex = SvgTextures.Load(path, res);
        return new Item
        {
            Sprite = new Sprite2D { Texture = tex.Texture, Centered = false, TextureFilter = TextureFilterEnum.Linear },
            Image = tex.Image, Box = box, Bone = bone, Offset = offset,
        };
    }

    void SetImage(Item item, string path, float[] box)
    {
        var tex = SvgTextures.Load(path, res);
        item.Sprite.Texture = tex.Texture;
        item.Image = tex.Image;
        item.Box = box;
    }

    public void SetFace(string name)
    {
        if (faceItem?.Slot?.Variants is not { } variants || name == face) return;
        var file = variants.TryGetValue(name, out var f) ? f : variants["idle"];
        SetImage(faceItem, rig.BasePath + file, faceItem.Slot.Box);
        face = name;
    }

    Vector2 faceOffset;

    /// <summary>臉部在頭部骨骼座標中的位移（視線用）</summary>
    public void SetFaceOffset(Vector2 offset) => faceOffset = offset;

    public void SetProps(IReadOnlyList<string> list)
    {
        var key = string.Join(",", list);
        if (key == propsKey) return;
        foreach (var (name, item) in propItems) item.Sprite.Visible = list.Contains(name);
        propsKey = key;
    }

    /// <param name="map">{ 插槽名: 替換圖名 }；沒指定或角色沒有該替換圖時用原圖</param>
    public void SetAlt(IReadOnlyDictionary<string, string> map)
    {
        foreach (var item in items)
        {
            if (item.Slot?.Alt is not { } alts) continue;
            var want = map.TryGetValue(item.Slot.Name, out var w) && alts.ContainsKey(w) ? w : null;
            if (want == item.CurrentAlt) continue;
            if (want != null) SetImage(item, rig.BasePath + alts[want].File, alts[want].Box);
            else SetImage(item, rig.BasePath + item.Slot.File, item.Slot.Box);
            item.CurrentAlt = want;
        }
    }

    public void Render(Dictionary<string, Transform2D> world, Vector2 position, int facing, float scale)
    {
        Transform = new Transform2D(new Vector2(facing * scale, 0), new Vector2(0, scale), position);
        foreach (var item in items)
        {
            if (!item.Sprite.Visible) continue;
            var m = world[item.Bone];
            if (item.Offset is { } offset) m *= offset;
            if (item == faceItem) m *= new Transform2D(0, faceOffset);
            // 貼圖像素 → 部件座標：縮小 res 倍，再移到部件範圍的左上角
            item.Sprite.Transform = m * new Transform2D(new Vector2(1 / res, 0), new Vector2(0, 1 / res),
                                                        new Vector2(item.Box[0], item.Box[1]));
        }
    }

    /// <summary>point（父節點座標）是否點在角色不透明的像素上</summary>
    public bool HitTest(Vector2 point) => HitSlot(point) != null;

    /// <summary>point 點到的最上層插槽名稱（道具回傳 "prop"）；沒點到回傳 null</summary>
    public string? HitSlot(Vector2 point)
    {
        for (var i = items.Count - 1; i >= 0; i--)
        {
            var item = items[i];
            if (!item.Sprite.Visible) continue;
            var local = (Transform * item.Sprite.Transform).AffineInverse() * point;
            int x = (int)local.X, y = (int)local.Y;
            if (x < 0 || y < 0 || x >= item.Image.GetWidth() || y >= item.Image.GetHeight()) continue;
            if (item.Image.GetPixel(x, y).A > 0.1f) return item.Slot?.Name ?? "prop";
        }
        return null;
    }

    /// <summary>所有可見部件的外框（父節點座標）</summary>
    public Rect2 Bounds()
    {
        var first = true;
        var rect = new Rect2();
        foreach (var item in items)
        {
            if (!item.Sprite.Visible) continue;
            var m = Transform * item.Sprite.Transform;
            var size = item.Image.GetSize();
            foreach (var c in new[] { Vector2.Zero, new Vector2(size.X, 0), new Vector2(0, size.Y), (Vector2)size })
            {
                var p = m * c;
                rect = first ? new Rect2(p, Vector2.Zero) : rect.Expand(p);
                first = false;
            }
        }
        return rect;
    }
}
