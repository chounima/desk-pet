// 講話時的嘴型：張嘴、閉嘴交替（用驚訝和開心的表情代替），主角色和客人共用
using Godot;

namespace DeskPet.Game.Guests;

public sealed class Mouth
{
    float timer;
    bool open;

    /// <summary>在 Behavior.Update 之後、Pet.Update 之前呼叫；回傳這一幀有沒有在動嘴</summary>
    public bool Update(float dt, Pet pet, Behavior behavior, bool talking)
    {
        if (!talking || behavior.Current != Behavior.State.Ground || behavior.Suspended || pet.Action == "sleep") return false;
        behavior.Touch();       // 講話中不會睡著
        timer -= dt;
        if (timer <= 0)
        {
            open = !open;
            timer = open ? 0.1f + GD.Randf() * 0.12f : 0.08f + GD.Randf() * 0.1f;
        }
        pet.FaceOverride = open ? "surprised" : "happy";
        return true;
    }
}
