// 把角色部件畫成 Sprite2D，每幀套用骨骼變換（移植自 preview/js/view/renderer.js）
// 另外提供像素級點擊判定與外框計算（給點擊穿透區域用）。
using System.Collections.Generic;
using System.Linq;
using DeskPet.Content;
using DeskPet.Core;
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
        public float Z;
        public bool Accessory;
        public string? HitName;           // 配件被點到時算哪個部位（掛在哪根骨骼）
        public string? FaceFile;          // 分層臉的圖層：目前顯示的檔案
        public Vector2 FaceShift;         // 分層臉的圖層：這一刻的位移（瞳孔看游標）
        public float Depth;               // 轉頭視差：位移 = 臉的位移 × Depth
        public MeshDeform? Mesh;          // 網格變形（有的話由 Poly 畫，Sprite 本身透明）
        public Polygon2D? Poly;
        public Vector2[]? Verts;          // 這一幀的頂點（PetView 座標，點擊判定用）
    }

    readonly RigData rig;
    readonly float res;
    readonly List<Item> items = new();
    readonly Dictionary<string, Item> propItems = new();
    Item? faceItem;
    readonly List<Item> faceLayers = new();
    string face = "idle";
    string propsKey = "";

    /// <summary>比較用（--no-mesh）：不用網格變形，部件照舊整張貼在骨頭上</summary>
    public static bool MeshDisabled { get; set; }

    // Godot 需要無參數建構子（引擎重新載入腳本時使用），實際使用請走下面的建構子
    public PetView() { rig = new RigData(); res = 1; }

    public PetView(RigData rig, Dictionary<string, PropData> props, float displayScale)
    {
        this.rig = rig;
        res = displayScale * Oversample;
        var entries = new List<(float z, Item item)>();
        Dictionary<string, Transform2D>? rest = null;       // 靜止姿勢（網格算權重用）

        foreach (var slot in rig.Slots)
        {
            var file = slot.Variants != null ? slot.Variants["idle"] : slot.File!;
            var item = MakeItem(rig.Resolve(file), slot.Box, slot.Bone, null);
            item.Slot = slot;
            item.Depth = slot.Depth ?? (slot.Variants != null ? 1 : 0);
            if (slot.Mesh is { Bones.Count: > 0 } mesh && !MeshDisabled)
            {
                rest ??= new Dictionary<string, Transform2D>(new Skeleton(rig.Bones, rig.RootScale, rig.TrackScale).Compute(new Dictionary<string, float>()));
                item.Mesh = new MeshDeform(slot, mesh, rest, rig.Bones, rig.Slots, rig.RootScale);
                item.Poly = new Polygon2D { TextureFilter = TextureFilterEnum.Linear, Antialiased = false };
                item.Poly.Polygons = new Godot.Collections.Array(Enumerable.Range(0, item.Mesh.Triangles.Length / 3)
                    .Select(i => Variant.From(item.Mesh.Triangles[(i * 3)..(i * 3 + 3)])).ToArray());
                item.Sprite.SelfModulate = new Color(1, 1, 1, 0);       // 圖由網格畫，Sprite 只負責圖層順序與顯示
                item.Sprite.AddChild(item.Poly);
                SyncMeshTexture(item);
            }
            if (slot.Far) item.Sprite.Modulate = FarTint;
            if (slot.Variants != null) faceItem = item;
            item.Z = slot.Z;
            entries.Add((slot.Z, item));
            // 分層臉：每個圖層一張圖，疊在表情插槽的位置；整張臉藏起來
            if (slot.Variants != null && rig.Face is { } layered)
            {
                item.Sprite.Visible = false;
                for (var i = 0; i < layered.Layers.Count; i++)
                {
                    var first = layered.Layers[i].Variants.Values.First();
                    var layer = MakeItem(rig.Resolve(first), layered.Box, slot.Bone, null);
                    layer.Slot = slot;
                    layer.Depth = item.Depth;
                    layer.FaceFile = first;
                    layer.Z = slot.Z + (i + 1) * 0.0001f;
                    faceLayers.Add(layer);
                    entries.Add((layer.Z, layer));
                }
            }
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
            item.Z = p.Z;
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
        var tex = SvgTextures.Load(path, res, box);
        return new Item
        {
            Sprite = new Sprite2D { Texture = tex.Texture, Centered = false, TextureFilter = TextureFilterEnum.Linear },
            Image = tex.Image, Box = box, Bone = bone, Offset = offset,
        };
    }

    void SetImage(Item item, string path, float[] box)
    {
        var tex = SvgTextures.Load(path, res, box);
        item.Sprite.Texture = tex.Texture;
        item.Image = tex.Image;
        item.Box = box;
        if (item.Poly != null) SyncMeshTexture(item);
    }

    // 網格的貼圖與 UV：頂點在部件裡的位置 → 貼圖像素（換衣服、替換圖時圖的範圍可能不一樣）
    void SyncMeshTexture(Item item)
    {
        item.Poly!.Texture = item.Sprite.Texture;
        item.Poly.UV = item.Mesh!.Uv.Select(p => new Vector2((p.X - item.Box[0]) * res, (p.Y - item.Box[1]) * res)).ToArray();
    }

    /// <summary>這個角色有分層臉（用 SetFaceLayers，不用 SetFace）</summary>
    public bool Layered => faceLayers.Count > 0;

    /// <summary>分層臉：每個圖層這一刻的樣子（Game/Face/FaceDriver 算的，順序和 rig.Face.Layers 一樣）</summary>
    public void SetFaceLayers(List<Game.Face.FaceLayerState> states)
    {
        for (var i = 0; i < faceLayers.Count && i < states.Count; i++)
        {
            var (item, st) = (faceLayers[i], states[i]);
            item.Sprite.Visible = st.File != null;
            if (st.File == null) continue;
            if (st.File != item.FaceFile)
            {
                SetImage(item, rig.Resolve(st.File), item.Box);
                item.FaceFile = st.File;
            }
            item.Sprite.SelfModulate = new Color(1, 1, 1, st.Alpha);
            item.FaceShift = new Vector2(st.Dx, st.Dy);
        }
    }

    public void SetFace(string name)
    {
        if (faceItem?.Slot?.Variants is not { } variants || name == face) return;
        var file = variants.TryGetValue(name, out var f) ? f : variants["idle"];
        SetImage(faceItem, rig.Resolve(file), faceItem.Slot.Box);
        face = name;
    }

    Vector2 faceOffset;

    /// <summary>臉部在頭部骨骼座標中的位移（視線用）；其他頭部圖層依 Depth 跟著移（轉頭視差）</summary>
    public void SetFaceOffset(Vector2 offset) => faceOffset = offset;

    /// <summary>配件：掛在角色的錨點上，依錨點的 scale 縮放；換一組就重建</summary>
    public void SetAccessories(IEnumerable<AccessoryData> list)
    {
        foreach (var old in items.Where(i => i.Accessory).ToList())
        {
            old.Sprite.QueueFree();
            items.Remove(old);
        }
        foreach (var acc in list)
        {
            if (!rig.Anchors.TryGetValue(acc.Anchor, out var a)) continue;      // 這個角色沒有這個錨點
            var offset = new Transform2D(new Vector2(a.Scale, 0), new Vector2(0, a.Scale), new Vector2(a.X, a.Y));
            var item = MakeItem(acc.Path, acc.Box, a.Bone, offset);
            item.Z = acc.Z;
            item.Depth = a.Depth;
            item.Accessory = true;
            item.HitName = a.Bone;
            AddChild(item.Sprite);
            items.Add(item);
        }
        // 依圖層順序重新排列
        items.Sort((x, y) => x.Z.CompareTo(y.Z));
        for (var i = 0; i < items.Count; i++) MoveChild(items[i].Sprite, i);
    }

    Dictionary<string, OutfitPart>? outfit;

    /// <summary>部件圖名（parts/torso.svg → torso）</summary>
    static string ArtOf(SlotData slot) => System.IO.Path.GetFileNameWithoutExtension(slot.File ?? "");

    /// <summary>衣服：把同名部件換成自訂圖片（近側、遠側的手腳都會換）；null = 換回原本的</summary>
    public void SetOutfit(Dictionary<string, OutfitPart>? parts)
    {
        outfit = parts;
        foreach (var item in items)
        {
            if (item.Slot?.File == null || item.Slot.Variants != null) continue;
            item.CurrentAlt = null;
            RestoreImage(item);
        }
    }

    /// <summary>部件的基本圖：有穿衣服就用衣服的圖，否則用角色原本的</summary>
    void RestoreImage(Item item)
    {
        var art = ArtOf(item.Slot!);
        if (outfit != null && outfit.TryGetValue(art, out var part)) SetImage(item, part.Path, part.Box);
        else if (rig.DefaultOutfit != null && rig.DefaultOutfit.TryGetValue(art, out var own)) SetImage(item, own.Path, own.Box);
        else SetImage(item, rig.BasePath + item.Slot!.File, item.Slot.Box);
    }

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
            if (want != null) SetImage(item, rig.Resolve(alts[want].File), alts[want].Box);
            else RestoreImage(item);
            item.CurrentAlt = want;
        }
    }

    public void Render(Dictionary<string, Transform2D> world, Vector2 position, int facing, float scale)
    {
        Transform = new Transform2D(new Vector2(facing * scale, 0), new Vector2(0, scale), position);
        foreach (var item in items)
        {
            if (!item.Sprite.Visible) continue;
            if (item.Mesh != null)
            {
                // 網格：頂點直接算在 PetView 座標，Sprite 不用變換
                var shift = faceOffset * item.Depth;
                item.Verts = item.Mesh.Compute(world);
                if (shift != Vector2.Zero) for (var i = 0; i < item.Verts.Length; i++) item.Verts[i] += shift;
                item.Sprite.Transform = Transform2D.Identity;
                item.Poly!.Polygon = item.Verts;
                continue;
            }
            var m = world[item.Bone];
            if (item.Offset is { } offset) m *= offset;
            if (item.Depth != 0 || item.FaceFile != null) m *= new Transform2D(0, faceOffset * item.Depth + item.FaceShift);
            // 貼圖像素 → 部件座標：縮小 res 倍，再移到部件範圍的左上角
            item.Sprite.Transform = m * new Transform2D(new Vector2(1 / res, 0), new Vector2(0, 1 / res),
                                                        new Vector2(item.Box[0], item.Box[1]));
        }
    }

    /// <summary>掛在某根骨骼上的部件，實際畫出來（不透明像素）的最高點（部件座標 y，取景用）；沒有就 null</summary>
    public float? VisibleTop(string bone)
    {
        float? top = null;
        foreach (var item in items.Where(i => i.Bone == bone && i.Offset == null && i.Sprite.Visible && i.Mesh == null))
        {
            var img = item.Image;
            var scale = img.GetHeight() / (item.Box[3] - item.Box[1]);
            for (var y = 0; y < img.GetHeight(); y += 2)
            {
                var hit = false;
                for (var x = 0; x < img.GetWidth() && !hit; x += 3) hit = img.GetPixel(x, y).A > 0.3f;
                if (!hit) continue;
                var v = item.Box[1] + y / scale;
                top = top == null ? v : Mathf.Min(top.Value, v);
                break;
            }
        }
        return top;
    }

    /// <summary>point（父節點座標）是否點在角色不透明的像素上</summary>
    public bool HitTest(Vector2 point) => HitSlot(point) != null;

    /// <summary>point 點到的最上層插槽名稱（道具回傳 "prop"）；沒點到回傳 null</summary>
    public string? HitSlot(Vector2 point)
    {
        if (!IsVisibleInTree()) return null;         // 藏起來的角色點不到
        for (var i = items.Count - 1; i >= 0; i--)
        {
            var item = items[i];
            if (!item.Sprite.Visible) continue;
            if (item.Mesh != null)
            {
                if (MeshPixel(item, Transform.AffineInverse() * point) is { } px && px.A > 0.1f) return item.Slot?.Name ?? "prop";
                continue;
            }
            var local = (Transform * item.Sprite.Transform).AffineInverse() * point;
            int x = (int)local.X, y = (int)local.Y;
            if (x < 0 || y < 0 || x >= item.Image.GetWidth() || y >= item.Image.GetHeight()) continue;
            if (item.Image.GetPixel(x, y).A > 0.1f) return item.Slot?.Name ?? item.HitName ?? "prop";
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
            if (item.Verts != null)
            {
                foreach (var v in item.Verts)
                {
                    var p = Transform * v;
                    rect = first ? new Rect2(p, Vector2.Zero) : rect.Expand(p);
                    first = false;
                }
                continue;
            }
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

    // 網格部件上某一點（PetView 座標）的像素：找到點在哪個三角形，用重心座標換回原圖位置
    static Color? MeshPixel(Item item, Vector2 p)
    {
        if (item.Verts == null) return null;
        var tris = item.Mesh!.Triangles;
        for (var t = 0; t < tris.Length; t += 3)
        {
            Vector2 a = item.Verts[tris[t]], b = item.Verts[tris[t + 1]], c = item.Verts[tris[t + 2]];
            var d = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
            if (Mathf.Abs(d) < 1e-6f) continue;
            var l1 = ((b.Y - c.Y) * (p.X - c.X) + (c.X - b.X) * (p.Y - c.Y)) / d;
            var l2 = ((c.Y - a.Y) * (p.X - c.X) + (a.X - c.X) * (p.Y - c.Y)) / d;
            var l3 = 1 - l1 - l2;
            if (l1 < 0 || l2 < 0 || l3 < 0) continue;
            var uv = item.Mesh.Uv[tris[t]] * l1 + item.Mesh.Uv[tris[t + 1]] * l2 + item.Mesh.Uv[tris[t + 2]] * l3;
            var res = item.Image.GetWidth() / (item.Box[2] - item.Box[0]);
            int x = (int)((uv.X - item.Box[0]) * res), y = (int)((uv.Y - item.Box[1]) * res);
            if (x < 0 || y < 0 || x >= item.Image.GetWidth() || y >= item.Image.GetHeight()) return null;
            return item.Image.GetPixel(x, y);
        }
        return null;
    }
}
