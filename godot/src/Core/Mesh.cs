// 網格變形（和 preview/js/core/mesh.js 同一套計算，tests/golden/mesh.json 比對）
// 插槽的 mesh：{ cols, rows, bones: [骨骼…] }。在部件範圍（box）鋪 cols × rows 的格子，
// 每個頂點依「到每根骨頭線段的距離」自動算權重，每幀把各骨骼的變換依權重混合 → 關節彎曲時部件跟著彎，不會折出硬角。
using System;
using System.Collections.Generic;
using System.Linq;
using DeskPet.Content;
using Godot;

namespace DeskPet.Core;

public sealed class MeshDeform
{
    const double MinWeight = 0.001;     // 太小的權重丟掉（兩邊一樣）

    readonly record struct Bind(string Bone, Vector2 Local, float W);

    /// <summary>頂點在部件裡的位置（插槽骨骼的座標）</summary>
    public Vector2[] Uv { get; }
    /// <summary>三角形（每三個是一個三角形的頂點索引）</summary>
    public int[] Triangles { get; }
    readonly Bind[][] binds;

    /// <param name="rest">靜止姿勢的世界矩陣</param>
    /// <param name="unit">rootScale（距離換成 Q 版單位算權重）</param>
    public MeshDeform(SlotData slot, MeshData mesh, Dictionary<string, Transform2D> rest, List<BoneData> bones, List<SlotData> slots, float unit = 1)
    {
        var names = mesh.Bones.Where(rest.ContainsKey).ToList();
        int cols = mesh.Cols, rows = mesh.Rows;
        var box = slot.Box;
        var uv = new List<Vector2>();
        for (var r = 0; r <= rows; r++)
            for (var c = 0; c <= cols; c++)
                uv.Add(new Vector2((float)(box[0] + (double)(box[2] - box[0]) * c / cols), (float)(box[1] + (double)(box[3] - box[1]) * r / rows)));
        Uv = uv.ToArray();
        var tris = new List<int>();
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
            {
                var i = r * (cols + 1) + c;
                tris.AddRange(new[] { i, i + 1, i + cols + 1, i + 1, i + cols + 2, i + cols + 1 });
            }
        Triangles = tris.ToArray();

        var segs = names.Select(n => Segment(n, rest, bones, slots)).ToList();
        var inverse = names.ToDictionary(n => n, n => rest[n].AffineInverse());
        binds = Uv.Select(uvp =>
        {
            var p = rest[slot.Bone] * uvp;
            var ws = segs.Select(s => 1 / Math.Pow(DistToSegment(p, s) / unit + 1, 4)).ToArray();
            var sum = ws.Sum();
            ws = ws.Select(w => w / sum < MinWeight ? 0 : w).ToArray();
            sum = ws.Sum();
            return names.Select((n, k) => new Bind(n, inverse[n] * p, (float)(ws[k] / sum))).Where(b => b.W > 0).ToArray();
        }).ToArray();
    }

    /// <summary>這一幀的頂點位置（角色座標，和 world 矩陣同一個空間）</summary>
    public Vector2[] Compute(Dictionary<string, Transform2D> world)
    {
        var result = new Vector2[binds.Length];
        for (var i = 0; i < binds.Length; i++)
        {
            double x = 0, y = 0;
            foreach (var b in binds[i])
            {
                var p = world[b.Bone] * b.Local;
                x += p.X * b.W;
                y += p.Y * b.W;
            }
            result[i] = new Vector2((float)x, (float)y);
        }
        return result;
    }

    // 骨頭線段（靜止姿勢、世界座標）：起點 = 骨骼原點，終點 = 子骨骼原點的平均；沒有子骨骼時用掛在它上面的部件範圍中心
    static (Vector2 a, Vector2 b) Segment(string name, Dictionary<string, Transform2D> rest, List<BoneData> bones, List<SlotData> slots)
    {
        var m = rest[name];
        var children = bones.Where(b => b.Parent == name).ToList();
        Vector2 end;
        if (children.Count > 0) end = new Vector2(children.Average(b => b.X), children.Average(b => b.Y));
        else
        {
            var boxes = slots.Where(s => s.Bone == name).Select(s => s.Box).ToList();
            end = boxes.Count > 0 ? new Vector2(boxes.Average(b => (b[0] + b[2]) / 2), boxes.Average(b => (b[1] + b[3]) / 2)) : Vector2.Zero;
        }
        return (m * Vector2.Zero, m * end);
    }

    static double DistToSegment(Vector2 p, (Vector2 a, Vector2 b) s)
    {
        double dx = s.b.X - s.a.X, dy = s.b.Y - s.a.Y, len2 = dx * dx + dy * dy;
        var t = len2 > 0 ? Math.Clamp(((p.X - s.a.X) * dx + (p.Y - s.a.Y) * dy) / len2, 0, 1) : 0;
        double ex = p.X - s.a.X - t * dx, ey = p.Y - s.a.Y - t * dy;
        return Math.Sqrt(ex * ex + ey * ey);
    }
}
