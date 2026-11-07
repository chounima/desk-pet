// 次級物理：尾巴、披布、頭髮等骨頭在動畫給的角度上加一個「彈簧＋阻尼」的偏移（移植成 preview/js/core/spring.js，兩邊算法一樣）
//
// 每根物理骨頭有偏移角 θ 與角速度 ω（弧度），每個子步驟（固定 1/120 秒）：
//   α = −stiffness·θ − damping·ω + gravity·cos(a) + (inertia·加速度 + drag·速度) 在骨頭垂直方向的分量 / length
//     （垂直分量 = vₓ·sin(a) − v_y·cos(a)）
// a = 動畫角度＋祖先物理骨頭的偏移＋θ（骨頭朝向，y 向下）；速度、加速度是骨頭起點的（Q 版單位／秒，含身體在畫面上的移動）。
// drag（空氣阻力）：移動時往後拖、拖的角度和速度成正比，停下時彈回並甩過頭；inertia：加速、急停時的甩動。|θ| 不超過 limit。
// 翻面時 θ、ω 鏡像（取負）；瞬間移動（一幀移動太遠、換角色）時重設。
// 純計算：不碰 Godot 節點；用 double 算，和網頁版（JS 的 number）結果一致（共用測試 tests/golden/physics.json）。
using System;
using System.Collections.Generic;
using DeskPet.Content;
using Godot;

namespace DeskPet.Core;

public sealed class Spring
{
    public const double Step = 1.0 / 120;      // 子步驟
    const int MaxSteps = 8;                      // 一幀最多幾個子步驟（卡頓時不要一次算太多）
    const double TeleportDistance = 400;        // 一幀移動超過這麼多 Q 版單位 = 瞬間移動，重設

    sealed class Bone
    {
        public required string Name;
        public required PhysicsParams P;
        public int Parent = -1;                  // 最近的物理祖先在 bones 裡的位置（偏移要加上去）
        public double Theta, Omega;
        public double PrevX, PrevY, VelX, VelY;
        public double Ax, Ay, Vx, Vy;            // 這一幀起點的加速度、速度（子步驟裡當常數）
        public bool HasPrev, HasVel;
    }

    readonly List<Bone> bones = new();
    readonly Dictionary<string, float> offsets = new();
    double accumulator;
    int prevFacing;

    /// <summary>沒有物理骨頭時不用算</summary>
    public bool Active => bones.Count > 0;

    /// <param name="skeleton">骨骼（父骨骼在前）</param>
    /// <param name="physics">rig.json 的 physics：{ 骨骼: 參數 }</param>
    public Spring(List<BoneData> skeleton, IReadOnlyDictionary<string, PhysicsParams>? physics)
    {
        if (physics == null) return;
        var parents = new Dictionary<string, string?>();
        foreach (var b in skeleton) parents[b.Name] = b.Parent;
        foreach (var b in skeleton)
        {
            if (!physics.TryGetValue(b.Name, out var p)) continue;
            string? ancestor = b.Parent;
            while (ancestor != null && !physics.ContainsKey(ancestor)) ancestor = parents.GetValueOrDefault(ancestor);
            bones.Add(new Bone { Name = b.Name, P = p, Parent = ancestor == null ? -1 : bones.FindIndex(x => x.Name == ancestor) });
        }
    }

    /// <summary>瞬間移動、換姿勢時：偏移歸零、速度歷史清掉</summary>
    public void Reset()
    {
        foreach (var b in bones)
        {
            b.Theta = b.Omega = 0;
            b.HasPrev = b.HasVel = false;
        }
        offsets.Clear();
        accumulator = 0;
    }

    /// <summary>
    /// 前進 dt 秒，回傳每根物理骨頭的偏移角（度），加在 Skeleton.Compute 的 offsets。
    /// </summary>
    /// <param name="world">這一幀「只有動畫、還沒加物理」的骨骼世界變換</param>
    /// <param name="bodyX">角色在畫面上的位置（腳底，像素）</param>
    /// <param name="scale">畫面上 1 Q 版單位幾像素</param>
    /// <param name="facing">1 = 朝右，-1 = 朝左</param>
    public IReadOnlyDictionary<string, float> Update(double dt, IReadOnlyDictionary<string, Transform2D> world,
                                                     double bodyX, double bodyY, double scale, int facing)
    {
        if (bones.Count == 0) return offsets;
        if (prevFacing != 0 && facing != prevFacing)
        {
            // 翻面：在角色自己的座標裡左右相反，偏移與角速度取負，速度歷史重來（不然會算出很大的加速度）
            foreach (var b in bones)
            {
                b.Theta = -b.Theta;
                b.Omega = -b.Omega;
                b.HasPrev = b.HasVel = false;
            }
        }
        prevFacing = facing;

        // 每根骨頭起點的加速度（每幀估一次，子步驟裡當常數）
        foreach (var b in bones)
        {
            var o = world[b.Name].Origin;
            // 角色座標（x 朝前）＋身體在畫面上的移動（換算成 Q 版單位、依朝向翻轉）
            var gx = facing * bodyX / scale + o.X;
            var gy = bodyY / scale + o.Y;
            double ax = 0, ay = 0;
            if (b.HasPrev && dt > 0)
            {
                if (Math.Abs(gx - b.PrevX) + Math.Abs(gy - b.PrevY) > TeleportDistance)
                {
                    b.HasVel = false;                          // 瞬間移動：這一幀不算加速度
                }
                else
                {
                    var vx = (gx - b.PrevX) / dt;
                    var vy = (gy - b.PrevY) / dt;
                    if (b.HasVel)
                    {
                        ax = (vx - b.VelX) / dt;
                        ay = (vy - b.VelY) / dt;
                    }
                    (b.VelX, b.VelY, b.HasVel) = (vx, vy, true);
                }
            }
            (b.PrevX, b.PrevY, b.HasPrev) = (gx, gy, true);
            (b.Ax, b.Ay, b.Vx, b.Vy) = (ax, ay, b.HasVel ? b.VelX : 0, b.HasVel ? b.VelY : 0);
        }

        accumulator += dt;
        var steps = 0;
        while (accumulator >= Step && steps < MaxSteps)
        {
            accumulator -= Step;
            steps++;
            foreach (var b in bones)
            {
                var p = b.P;
                var angle = AnimatedAngle(world, b) + b.Theta;
                var fx = p.Inertia * b.Ax + p.Drag * b.Vx;
                var fy = p.Inertia * b.Ay + p.Drag * b.Vy;
                var alpha = -p.Stiffness * b.Theta - p.Damping * b.Omega
                            + p.Gravity * Math.Cos(angle)
                            + (fx * Math.Sin(angle) - fy * Math.Cos(angle)) / Math.Max(1, p.Length);
                b.Omega += alpha * Step;
                b.Theta += b.Omega * Step;
                var limit = p.Limit * Math.PI / 180;
                if (Math.Abs(b.Theta) > limit)
                {
                    b.Theta = Math.Sign(b.Theta) * limit;
                    b.Omega = 0;
                }
            }
        }
        if (steps == MaxSteps) accumulator = 0;     // 卡頓：丟掉來不及算的時間

        offsets.Clear();
        foreach (var b in bones) offsets[b.Name] = (float)(b.Theta * 180 / Math.PI);
        return offsets;
    }

    // 骨頭的朝向（弧度）：動畫的世界角度＋祖先物理骨頭的偏移
    double AnimatedAngle(IReadOnlyDictionary<string, Transform2D> world, Bone b)
    {
        var angle = (double)world[b.Name].Rotation;
        for (var i = b.Parent; i >= 0; i = bones[i].Parent) angle += bones[i].Theta;
        return angle;
    }
}
