// 講話中的聲波：從嘴巴前面一圈一圈往外擴散的弧線 ) ) )，讓「正在講話」更明顯
// 主角色和客人共用；講話停了就淡出
using DeskPet.Game;
using Godot;

namespace DeskPet.View;

public partial class SpeechWaves : Node2D
{
    const float Period = 0.7f;        // 一道聲波從嘴邊擴散到最外面的時間
    const int Count = 3;              // 同時有幾道
    const float Spread = 0.75f;       // 弧線的半個張角（弧度，約 43°）
    static readonly Vector2 MouthFront = new(52, -30);     // 頭部骨骼座標：臉的前緣、嘴巴高度

    static readonly Color Fill = new(1, 1, 1, 0.95f), Outline = new(0.14f, 0.09f, 0.11f, 0.85f);

    float time;
    float alpha;          // 開始／停止講話時淡入淡出
    float size = 1;
    int facing = 1;
    float boost;          // 嘴巴張開的瞬間弧線粗一點

    public SpeechWaves() => ZIndex = 1;

    /// <param name="talking">這一幀有沒有在動嘴（Mouth.Update 的回傳值）</param>
    public void UpdateWaves(float dt, bool talking, Pet pet, bool mouthOpen)
    {
        alpha = Mathf.MoveToward(alpha, talking ? 1 : 0, dt * (talking ? 6 : 3));
        Visible = alpha > 0.01f;
        if (!Visible) return;
        time += dt;
        boost = Mathf.MoveToward(boost, mouthOpen ? 1 : 0, dt * 8);
        facing = pet.Facing;
        size = pet.Scale;
        Position = pet.BoneToScreen("head", MouthFront);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var angle = facing > 0 ? 0 : Mathf.Pi;
        for (var i = 0; i < Count; i++)
        {
            // 每道聲波錯開 1/Count 個週期：半徑由小變大，越外面越淡
            var t = (time / Period + i / (float)Count) % 1f;
            var radius = (8 + 26 * t) * size;
            var a = alpha * Mathf.Sin(t * Mathf.Pi);       // 剛出現和快消失時淡
            if (a <= 0.02f) continue;
            var width = (2.2f + 1.2f * boost) * size * (1 - 0.4f * t);
            var from = angle - Spread;
            var to = angle + Spread;
            DrawArc(Vector2.Zero, radius, from, to, 16, Outline with { A = Outline.A * a }, width + 2.2f * size, true);
            DrawArc(Vector2.Zero, radius, from, to, 16, Fill with { A = Fill.A * a }, width, true);
        }
    }
}
