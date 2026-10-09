// 視線：閒著的時候，頭朝游標微微偏轉、臉往游標那邊移一點（眼睛看起來在盯著游標）
using System.Collections.Generic;
using DeskPet.Platform;
using Godot;

namespace DeskPet.Game;

public sealed class Gaze
{
    /// <summary>會看游標的動作（走路、睡覺、被抓等不看）</summary>
    public static readonly HashSet<string> Lookable = new() { "idle", "sit", "sitEdge", "think", "wave", "peace", "cheer" };

    const float Range = 900;          // 游標離臉多遠以內才看（px，再乘 UiScale）
    const float Follow = 6;           // 轉頭的平滑速度

    float head;
    Vector2 face;

    /// <summary>要看的位置（視窗座標）；null = 不看</summary>
    public Vector2? Target { get; set; }

    /// <summary>加在 head 骨骼上的角度（正 = 低頭）</summary>
    public float HeadAngle => head;

    /// <summary>臉部在頭部骨骼座標中的位移</summary>
    public Vector2 FaceOffset => face;

    public void Update(Pet pet, float dt)
    {
        var u = DesktopWindow.UiScale;
        float targetHead = 0;
        var targetFace = Vector2.Zero;

        if (Target is { } t && Lookable.Contains(pet.Action))
        {
            var eye = pet.FaceCenter();
            var dx = (t.X - eye.X) * pet.Facing;      // 正 = 在前方
            var dy = t.Y - eye.Y;                      // 正 = 在下方
            if (dx > -40 * u && new Vector2(dx, dy).Length() < Range * u)
            {
                targetHead = Mathf.Clamp(Mathf.RadToDeg(Mathf.Atan2(dy, Mathf.Max(Mathf.Abs(dx), 80 * u))) * 0.4f, -12, 15);
                var k = -pet.Rig.HeadTop[1] / 110f;     // 頭畫得越小（正常頭身），臉移動越少
                targetFace = new Vector2(Mathf.Clamp(dx / (300 * u), -1, 1) * 3 * k,
                                         Mathf.Clamp(dy / (300 * u), -1, 1) * 2.5f * k);
            }
        }

        var s = Mathf.Min(1, dt * Follow);
        head += (targetHead - head) * s;
        face += (targetFace - face) * s;
    }
}
