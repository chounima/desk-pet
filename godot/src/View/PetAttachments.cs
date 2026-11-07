// 角色的掛件：特效、聲波、對話泡泡、名牌（選配），每幀依角色的錨點擺好位置
//
// 頭上的東西（名牌、泡泡、特效）跟著 nameTag 錨點（沒有就用頭頂 HeadTop），聲波跟著 mouth 錨點（見 SpeechWaves）。
// 角色畫在哪、放多大（桌面全身、之後的半身模式），掛件就跟到哪，不用各自算位置。
using DeskPet.Game;
using DeskPet.Platform;
using Godot;

namespace DeskPet.View;

public sealed class PetAttachments
{
    readonly Pet pet;

    public Fx Fx { get; }
    public SpeechWaves Waves { get; }
    public Bubble Bubble { get; }
    /// <summary>名牌（只有客人有）</summary>
    public NameTag? Tag { get; }

    /// <param name="name">名牌上的名字；null = 不要名牌</param>
    public PetAttachments(Node parent, Pet pet, string? name = null)
    {
        this.pet = pet;
        Fx = new Fx();
        parent.AddChild(Fx);
        Waves = new SpeechWaves();
        parent.AddChild(Waves);
        Bubble = new Bubble();
        parent.AddChild(Bubble);
        if (name != null)
        {
            Tag = new NameTag(name);
            parent.AddChild(Tag);
        }
    }

    /// <summary>頭上的掛件位置：nameTag 錨點，沒有就用頭頂</summary>
    public Vector2 Top => pet.Rig.Anchors.TryGetValue("nameTag", out var a) ? pet.BoneToScreen(a.Bone, new Vector2(a.X, a.Y)) : pet.HeadTop();

    /// <summary>在 Pet.Update 之後呼叫（用這一幀的姿勢）</summary>
    /// <param name="talking">這一幀有沒有在動嘴（Mouth.Update 的回傳值）</param>
    public void Update(float dt, bool talking = false, bool mouthOpen = false)
    {
        var top = Top;
        Fx.UpdateFx(dt, pet.Animator.Fx, top);
        Waves.UpdateWaves(dt, talking, pet, mouthOpen);
        var bubbleAt = top;
        if (Tag != null)
        {
            Tag.Place(top);
            bubbleAt = top - new Vector2(0, Tag.Size.Y + 4 * DesktopWindow.UiScale);     // 名牌在頭上，泡泡再往上
        }
        Bubble.UpdateBubble(dt, bubbleAt);
    }

    public void SetHidden(bool hidden)
    {
        Fx.Visible = !hidden;
        Bubble.Muted = hidden;
        if (Tag != null) Tag.Visible = !hidden;
    }

    /// <summary>淡出（客人離開）：名牌跟著角色一起變透明</summary>
    public void SetAlpha(float a)
    {
        if (Tag != null) Tag.Modulate = new Color(1, 1, 1, a);
    }

    public void Free()
    {
        Fx.QueueFree();
        Waves.QueueFree();
        Bubble.QueueFree();
        Tag?.QueueFree();
    }
}
