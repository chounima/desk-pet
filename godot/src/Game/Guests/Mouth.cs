// 講話時的嘴型：閉、半開、全開交替，透過 arbiter 的 Mouth 管道（分層臉換嘴巴圖層；只有整張臉的角色換成驚訝／開心），主角色和客人共用
using Godot;

namespace DeskPet.Game.Guests;

public sealed class Mouth
{
    float timer;
    string shape = "closed";

    /// <summary>這一刻嘴巴是張開的（聲波跟著變粗）</summary>
    public bool Open => shape != "closed";

    /// <summary>在 Behavior.Update 之後、Pet.Update 之前呼叫；嘴型透過 arbiter 請求（Mouth 管道、講話優先順序）。回傳這一幀有沒有在動嘴</summary>
    public bool Update(float dt, Drivers.IPetDriver driver, Control.ControlArbiter control, bool talking)
    {
        if (!talking || !driver.CanEmote) return false;
        driver.Touch();         // 講話中不會睡著
        timer -= dt;
        if (timer <= 0)
        {
            shape = shape != "closed" ? "closed" : GD.Randf() < 0.4f ? "half" : "open";
            timer = shape != "closed" ? 0.1f + GD.Randf() * 0.12f : 0.08f + GD.Randf() * 0.1f;
        }
        control.Request("talk", Control.Channel.Mouth, shape, Control.Priority.Talking);
        return true;
    }
}
