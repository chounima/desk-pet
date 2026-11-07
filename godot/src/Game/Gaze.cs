// 視線：閒著的時候看游標。三層速度不同：眼睛（瞳孔）最快、頭其次、身體最慢且只跟一點點。
// 游標很久沒動就看回前方；看游標時偶爾自己瞄別的地方一下（不會一直死盯）。
using System.Collections.Generic;
using DeskPet.Platform;
using Godot;

namespace DeskPet.Game;

public sealed class Gaze
{
    static Content.FaceConfig.GazeConfig Cfg => Content.FaceConfig.Load().Gaze;     // data/face.json

    /// <summary>會看游標的動作（走路、睡覺、被抓等不看）</summary>
    public static HashSet<string> Lookable => Cfg.LookActions;

    float head, body, still, glanceIn = 6, glanceLeft;
    Vector2 face, look, glance;
    Vector2? lastTarget;
    float? range;

    /// <summary>游標離臉多遠以內才看（px，再乘 UiScale）；半身模式設很大，螢幕上哪裡都看得到。沒設 = data/face.json 的 range</summary>
    public float Range { get => range ?? Cfg.Range; set => range = value; }

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

    /// <summary>額外的轉向（-1～1，半身模式的角度：在兩個視角之間時臉往那邊偏）</summary>
    public Vector2 Bias { get; set; }

    public void Update(Pet pet, float dt)
    {
        var c = Cfg;
        var u = DesktopWindow.UiScale;
        float targetHead = 0;
        Vector2 targetFace = Vector2.Zero, targetLook = Vector2.Zero;
        var k = -pet.Rig.HeadTop[1] / 110f;     // 頭畫得越小（正常頭身），臉移動越少

        // 游標很久沒動：看回前方
        if (Target is { } moved && lastTarget is { } last && moved.DistanceTo(last) < 2 * u) still += dt;
        else still = 0;
        lastTarget = Target;

        if (Target is { } t && still < c.StillLimit && Lookable.Contains(pet.Action))
        {
            var eye = pet.FaceCenter();
            var dx = (t.X - eye.X) * pet.Facing;      // 正 = 在前方
            var dy = t.Y - eye.Y;                      // 正 = 在下方
            if (dx > -40 * u && new Vector2(dx, dy).Length() < Range * u)
            {
                targetHead = Mathf.Clamp(Mathf.RadToDeg(Mathf.Atan2(dy, Mathf.Max(Mathf.Abs(dx), 80 * u))) * c.HeadRatio, c.HeadMin, c.HeadMax);
                targetLook = new Vector2(Mathf.Clamp(dx / (c.LookDistance * u), -1, 1), Mathf.Clamp(dy / (c.LookDistance * u), -1, 1));
            }
            // 偶爾瞄別的地方一下：眼睛為主，頭只動一點
            if ((glanceIn -= dt) <= 0 && glanceLeft <= 0)
            {
                glanceLeft = (float)GD.RandRange(c.GlanceTimeMin, c.GlanceTimeMax);
                glance = new Vector2(GD.Randf() < 0.5f ? -0.8f : 0.8f, GD.Randf() - 0.5f);
            }
            if (glanceLeft > 0)
            {
                targetLook = glance;
                targetHead *= 0.5f;
                if ((glanceLeft -= dt) <= 0) glanceIn = (float)GD.RandRange(c.GlanceMin, c.GlanceMax);
            }
        }
        var biased = (targetLook + Bias).Clamp(new Vector2(-1, -1), new Vector2(1, 1));
        targetFace = new Vector2(biased.X * c.FaceShiftX * k, biased.Y * c.FaceShiftY * k);

        var s = Mathf.Min(1, dt * c.HeadFollow);
        head += (targetHead - head) * s;
        face += (targetFace - face) * s;
        look += (targetLook - look) * Mathf.Min(1, dt * c.EyeFollow);      // 眼睛比頭快
        body += (targetHead * c.BodyRatio - body) * Mathf.Min(1, dt * c.BodyFollow);
    }
}
