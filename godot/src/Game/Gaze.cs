// 視線：閒著的時候看游標。三層速度不同：眼睛（瞳孔）最快、頭其次、身體最慢且只跟一點點。
// 游標很久沒動就看回前方；看游標時偶爾自己瞄別的地方一下（不會一直死盯）。
using System.Collections.Generic;
using DeskPet.Platform;
using Godot;

namespace DeskPet.Game;

public sealed class Gaze
{
    /// <summary>會看游標的動作（走路、睡覺、被抓等不看）</summary>
    public static readonly HashSet<string> Lookable = new() { "idle", "sit", "sitEdge", "think", "wave", "peace", "cheer" };

    const float Follow = 6;           // 轉頭的平滑速度
    const float BodyFollow = 2;       // 身體跟著轉的平滑速度（比頭慢）
    const float StillLimit = 8;       // 游標幾秒沒動就看回前方

    float head, body, still, glanceIn = 6, glanceLeft;
    Vector2 face, look, glance;
    Vector2? lastTarget;

    /// <summary>游標離臉多遠以內才看（px，再乘 UiScale）；半身模式設很大，螢幕上哪裡都看得到</summary>
    public float Range { get; set; } = 900;

    /// <summary>要看的位置（視窗座標）；null = 不看</summary>
    public Vector2? Target { get; set; }

    /// <summary>加在 head 骨骼上的角度（正 = 低頭）</summary>
    public float HeadAngle => head;

    /// <summary>加在 torso 骨骼上的角度（身體微微跟著轉）</summary>
    public float BodyAngle => body;

    /// <summary>臉部在頭部骨骼座標中的位移</summary>
    public Vector2 FaceOffset => face;

    /// <summary>視線方向 -1～1（右 = 前方、下為正），分層臉的瞳孔用</summary>
    public Vector2 Look => look;

    public void Update(Pet pet, float dt)
    {
        var u = DesktopWindow.UiScale;
        float targetHead = 0;
        Vector2 targetFace = Vector2.Zero, targetLook = Vector2.Zero;

        // 游標很久沒動：看回前方
        if (Target is { } moved && lastTarget is { } last && moved.DistanceTo(last) < 2 * u) still += dt;
        else still = 0;
        lastTarget = Target;

        if (Target is { } t && still < StillLimit && Lookable.Contains(pet.Action))
        {
            var eye = pet.FaceCenter();
            var dx = (t.X - eye.X) * pet.Facing;      // 正 = 在前方
            var dy = t.Y - eye.Y;                      // 正 = 在下方
            if (dx > -40 * u && new Vector2(dx, dy).Length() < Range * u)
            {
                targetHead = Mathf.Clamp(Mathf.RadToDeg(Mathf.Atan2(dy, Mathf.Max(Mathf.Abs(dx), 80 * u))) * 0.4f, -12, 15);
                var k = -pet.Rig.HeadTop[1] / 110f;     // 頭畫得越小（正常頭身），臉移動越少
                targetLook = new Vector2(Mathf.Clamp(dx / (300 * u), -1, 1), Mathf.Clamp(dy / (300 * u), -1, 1));
                targetFace = new Vector2(targetLook.X * 3 * k, targetLook.Y * 2.5f * k);
            }
            // 偶爾瞄別的地方一下：眼睛為主，頭只動一點
            if ((glanceIn -= dt) <= 0 && glanceLeft <= 0)
            {
                glanceLeft = 0.5f + GD.Randf() * 0.5f;
                glance = new Vector2(GD.Randf() < 0.5f ? -0.8f : 0.8f, GD.Randf() - 0.5f);
            }
            if (glanceLeft > 0)
            {
                targetLook = glance;
                targetHead *= 0.5f;
                if ((glanceLeft -= dt) <= 0) glanceIn = 6 + GD.Randf() * 8;
            }
        }

        var s = Mathf.Min(1, dt * Follow);
        head += (targetHead - head) * s;
        face += (targetFace - face) * s;
        look += (targetLook - look) * Mathf.Min(1, dt * Follow * 2);      // 眼睛比頭快
        body += (targetHead * 0.3f - body) * Mathf.Min(1, dt * BodyFollow);
    }
}
