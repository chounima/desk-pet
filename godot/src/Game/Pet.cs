// 寵物本體：位置、朝向、骨架、動作、表情（移植自 preview/js/game/pet.js）
using System.Collections.Generic;
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
    public float Scale { get; }
    public Animator Animator { get; }
    public RigData Rig { get; private set; } = null!;
    public PetView View { get; private set; } = null!;
    public IReadOnlyDictionary<string, Transform2D> World => world;

    public Pet(Node parent, RigData rig, Dictionary<string, ClipData> clips, Dictionary<string, PropData> props, float scale)
    {
        this.parent = parent;
        this.props = props;
        Scale = scale;
        Animator = new Animator(clips, Tuning.Crossfade);
        Animator.Play("idle");
        SetCharacter(rig);
    }

    public void SetCharacter(RigData rig)
    {
        View?.QueueFree();
        Rig = rig;
        skeleton = new Skeleton(rig.Bones, rig.RootScale, rig.TrackScale);
        world = skeleton.Compute(Animator.Pose);
        View = new PetView(rig, props, Scale);
        parent.AddChild(View);
        parent.MoveChild(View, 0);       // 特效等其他節點畫在角色上面
    }

    public void Play(string name, bool restart = false, Dictionary<string, float>? mods = null) =>
        Animator.Play(name, restart, mods);

    public string Action => Animator.Name;

    /// <summary>目前動作的移動速度（px/s）：依腿長與顯示大小放大</summary>
    public float Speed => Animator.Move * Rig.RootScale * (Scale / Tuning.Scale);

    public void Update(float dt)
    {
        world = skeleton.Compute(Animator.Update(dt));
        View.SetFace(CurrentFace(dt));
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
    public Vector2 HeadTop()
    {
        var p = world["head"] * new Vector2(Rig.HeadTop[0], Rig.HeadTop[1]);
        return new Vector2(X + p.X * Facing * Scale, Y + p.Y * Scale);
    }
}
