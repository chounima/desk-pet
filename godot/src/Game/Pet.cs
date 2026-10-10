// 寵物本體：位置、朝向、骨架、動作、表情（移植自 preview/js/game/pet.js）
using System.Collections.Generic;
using System.Linq;
using DeskPet.Content;
using DeskPet.Core;
using DeskPet.View;
using Godot;

namespace DeskPet.Game;

public sealed class Pet
{
    readonly Node parent;
    readonly Dictionary<string, PropData> props;
    Skeleton skeleton = null!;
    Dictionary<string, Transform2D> world = null!;
    float blinkTimer = 2, blinkLeft;

    public float X, Y;
    public int Facing = 1;                 // 1 = 朝右，-1 = 朝左
    public string? FaceOverride;           // 由行為邏輯暫時指定的表情
    public float Scale { get; private set; }
    public Animator Animator { get; }
    public RigData Rig { get; private set; } = null!;
    public PetView View { get; private set; } = null!;
    public IReadOnlyDictionary<string, Transform2D> World => world;
    public Gaze Gaze { get; } = new();
    /// <summary>目前角色體型的動作組</summary>
    public Dictionary<string, ClipData> Clips => Animator.Clips;
    readonly Dictionary<string, Dictionary<string, ClipData>> clipSets;

    /// <param name="clipSets">{ 體型: { 動作名: clip } }，見 ContentLoader.LoadAnimations</param>
    public Pet(Node parent, RigData rig, Dictionary<string, Dictionary<string, ClipData>> clipSets,
               Dictionary<string, PropData> props, float scale)
    {
        this.parent = parent;
        this.props = props;
        this.clipSets = clipSets;
        Scale = scale;
        Animator = new Animator(clipSets["humanoid"], Tuning.Crossfade);
        Animator.Play("idle");
        SetCharacter(rig);
    }

    public void SetCharacter(RigData rig)
    {
        View?.QueueFree();
        Rig = rig;
        // 換體型時換一套動作，並用新的姿勢重播目前的動作
        var clips = clipSets.GetValueOrDefault(rig.Body) ?? clipSets["humanoid"];
        if (clips != Animator.Clips)
        {
            Animator.Clips = clips;
            Animator.Play(Animator.Name, true);
        }
        probeCache.Clear();
        skeleton = new Skeleton(rig.Bones, rig.RootScale, rig.TrackScale);
        world = skeleton.Compute(Animator.Pose);
        View = new PetView(rig, props, Scale);
        View.SetAccessories(accessories);
        parent.AddChild(View);
        parent.MoveChild(View, 0);       // 特效等其他節點畫在角色上面
    }

    /// <summary>改變大小：貼圖依顯示大小產生，所以要重建畫面</summary>
    public void SetScale(float scale)
    {
        if (Mathf.IsEqualApprox(scale, Scale)) return;
        Scale = scale;
        SetCharacter(Rig);
    }

    List<AccessoryData> accessories = new();

    /// <summary>身上的配件；換角色、換大小重建畫面時會重新套用</summary>
    public void SetAccessories(IEnumerable<AccessoryData> list)
    {
        accessories = list.ToList();
        View.SetAccessories(accessories);
        View.SetOutfit(outfit);
    }

    Dictionary<string, OutfitPart>? outfit;

    /// <summary>身上的衣服（自訂部件圖）；換大小重建畫面時會重新套用</summary>
    public void SetOutfit(Dictionary<string, OutfitPart>? parts)
    {
        outfit = parts;
        View.SetOutfit(parts);
    }

    public void Play(string name, bool restart = false, Dictionary<string, float>? mods = null) =>
        Animator.Play(name, restart, mods);

    public string Action => Animator.Name;

    /// <summary>目前動作的移動速度（px/s）：依腿長與顯示大小放大</summary>
    public float Speed => Animator.Move * Rig.RootScale * (Scale / Tuning.Scale);

    public void Update(float dt)
    {
        var pose = Animator.Update(dt);
        Gaze.Update(this, dt);
        pose["head"] = pose.GetValueOrDefault("head") + Gaze.HeadAngle;
        world = skeleton.Compute(pose);
        View.SetFace(CurrentFace(dt));
        View.SetFaceOffset(Gaze.FaceOffset);
        View.SetProps(Animator.Props);
        View.SetAlt(Animator.Alt);
        View.Render(world, new Vector2(X, Y), Facing, Scale);
    }

    // 優先使用 override，其次是動作指定的表情；睜眼表情會自動眨眼
    string CurrentFace(float dt)
    {
        var face = FaceOverride ?? Animator.Face;
        blinkTimer -= dt;
        if (blinkTimer <= 0)
        {
            blinkLeft = 0.12f;
            blinkTimer = 2.5f + GD.Randf() * 3;
        }
        if (blinkLeft > 0)
        {
            blinkLeft -= dt;
            if (face is "idle" or "focus") return "blink";
        }
        return face;
    }

    /// <summary>頭頂在畫面上的位置（特效錨點）</summary>
    public Vector2 HeadTop() => ToScreen(world["head"] * new Vector2(Rig.HeadTop[0], Rig.HeadTop[1]));

    /// <summary>臉的中心在畫面上的位置（視線起點）</summary>
    public Vector2 FaceCenter() => ToScreen(world["head"] * new Vector2(Rig.HeadTop[0], Rig.HeadTop[1]) * 0.5f);

    Vector2 ToScreen(Vector2 p) => new(X + p.X * Facing * Scale, Y + p.Y * Scale);

    /// <summary>畫面座標 → 某根骨骼的局部座標（判斷點到頭的哪個部位）</summary>
    public Vector2 ToBoneLocal(string bone, Vector2 screen) =>
        world[bone].AffineInverse() * new Vector2((screen.X - X) / (Facing * Scale), (screen.Y - Y) / Scale);

    readonly Dictionary<string, Vector2> probeCache = new();

    /// <summary>
    /// 探測：動作 name 播到 t 秒時，某根骨骼上的點相對腳底的畫面位移（X 以面向前方為正）。
    /// 用來預先知道殺球、魚躍時手會在哪裡。mods 同 Play 的軌道縮放。
    /// </summary>
    public Vector2 Probe(string name, float t, string bone, float lx, float ly, Dictionary<string, float>? mods = null)
    {
        var key = $"{Rig.Id}|{Rig.Body}|{name}|{t}|{bone}|{lx}|{ly}|{Scale}|{(mods == null ? "" : string.Join(",", mods))}";
        if (probeCache.TryGetValue(key, out var hit)) return hit;
        var pose = new Dictionary<string, float>();
        foreach (var (ch, keys) in Clips[name].Tracks)
            pose[ch] = Animator.SampleTrack(keys, t) * (mods != null && mods.TryGetValue(ch, out var m) ? m : 1);
        var world = new Skeleton(Rig.Bones, Rig.RootScale, Rig.TrackScale).Compute(pose);
        var p = world[bone] * new Vector2(lx, ly) * Scale;
        probeCache[key] = p;
        return p;
    }

    /// <summary>某根骨骼上的點在畫面上的位置</summary>
    public Vector2 BoneToScreen(string bone, Vector2 local) => ToScreen(world[bone] * local);
}
