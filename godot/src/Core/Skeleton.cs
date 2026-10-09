// 骨架：依姿勢（各骨骼角度與位移）算出每根骨骼的世界變換（移植自 preview/js/core/skeleton.js）
using System.Collections.Generic;
using DeskPet.Content;
using Godot;

namespace DeskPet.Core;

public sealed class Skeleton
{
    readonly List<BoneData> bones;
    readonly float moveScale;
    readonly Dictionary<string, float> angleScale;

    public Dictionary<string, Transform2D> World { get; } = new();

    /// <param name="moveScale">動作位移以 Q 版身材撰寫，依角色腿長放大</param>
    /// <param name="angleScale">個別骨骼的角度倍率</param>
    public Skeleton(List<BoneData> bones, float moveScale, Dictionary<string, float> angleScale)
    {
        this.bones = bones;
        this.moveScale = moveScale;
        this.angleScale = angleScale;
    }

    /// <param name="pose">{ 骨骼名: 角度, "骨骼名.x": 位移, "骨骼名.y": 位移 }</param>
    public Dictionary<string, Transform2D> Compute(IReadOnlyDictionary<string, float> pose)
    {
        foreach (var b in bones)
        {
            var tx = b.X + Get(pose, b.Name + ".x") * moveScale;
            var ty = b.Y + Get(pose, b.Name + ".y") * moveScale;
            var angle = Get(pose, b.Name) * (angleScale.TryGetValue(b.Name, out var s) ? s : 1);
            var local = new Transform2D(Mathf.DegToRad(angle), new Vector2(tx, ty));
            World[b.Name] = b.Parent is null ? local : World[b.Parent] * local;
        }
        return World;
    }

    static float Get(IReadOnlyDictionary<string, float> pose, string key) =>
        pose.TryGetValue(key, out var v) ? v : 0;
}
