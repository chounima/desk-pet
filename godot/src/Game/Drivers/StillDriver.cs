// 原地不動的驅動者：站在指定位置，只做被請求的動作；不循環的動作做完回到 idle（錄影、之後的半身模式用）
namespace DeskPet.Game.Drivers;

public sealed class StillDriver : IPetDriver
{
    readonly Pet pet;
    readonly float x, y;

    /// <param name="x">站的位置（視窗座標，腳底）</param>
    public StillDriver(Pet pet, float x, float y, string action = "idle")
    {
        this.pet = pet;
        this.x = x;
        this.y = y;
        pet.Play(action, true);
    }

    public string Id => "still";

    /// <summary>不循環的動作做完後要不要停在最後一格（錄影時要，半身模式不要）</summary>
    public bool HoldLastFrame { get; init; }

    public void Update(float dt)
    {
        pet.X = x;
        pet.Y = y;
        pet.FaceOverride = null;
        if (pet.Animator.Finished && !HoldLastFrame) pet.Play(pet.Animator.Clip.Next ?? "idle", true);
    }

    public bool TryStart(string action)
    {
        if (!pet.Clips.ContainsKey(action)) return false;
        pet.Play(action, true);
        return true;
    }

    public bool CanEmote => pet.Action != "sleep";

    public void Touch() { }
}
