// 排球的物理：重力、撞牆／天花板反彈、落地；以及軌跡預測與拋物線發射
using System;
using System.Collections.Generic;
using DeskPet.Platform;
using Godot;

namespace DeskPet.Game.Minigames;

public sealed class VolleyBall
{
    public float X, Y, Vx, Vy, Rot;
    public float R = VolleyConfig.BallRadius;

    public Vector2 Position => new(X, Y);
    public float Speed => new Vector2(Vx, Vy).Length();

    /// <summary>前進一步；回傳這一步是否落地</summary>
    public bool Step(float dt, float width, float ground)
    {
        Vy += VolleyConfig.Gravity * dt;
        X += Vx * dt;
        Y += Vy * dt;
        Rot += Vx * dt / R;

        if (X < R) { X = R; Vx = Math.Abs(Vx) * 0.8f; }
        if (X > width - R) { X = width - R; Vx = -Math.Abs(Vx) * 0.8f; }
        if (Y < R) { Y = R; Vy = Math.Abs(Vy) * 0.6f; }
        if (Y <= ground - R) return false;
        Y = ground - R;
        Vy = Math.Abs(Vy) > 80 * DesktopWindow.UiScale ? -Math.Abs(Vy) * 0.45f : 0;
        Vx *= 0.75f;
        return true;
    }

    public readonly record struct Point(float T, float X, float Y, float Vy);

    /// <summary>預測接下來的軌跡，直到落地。用和 Step 相同的規則，撞牆、撞天花板反彈都算得準</summary>
    public List<Point> Trajectory(float width, float ground)
    {
        const float step = 1f / 120;
        var g = VolleyConfig.Gravity;
        var result = new List<Point>();
        float x = X, y = Y, vx = Vx, vy = Vy;
        for (var t = step; t < 6; t += step)
        {
            vy += g * step;
            x += vx * step;
            y += vy * step;
            if (x < R) { x = R; vx = Math.Abs(vx) * 0.8f; }
            if (x > width - R) { x = width - R; vx = -Math.Abs(vx) * 0.8f; }
            if (y < R) { y = R; vy = Math.Abs(vy) * 0.6f; }
            if (y > ground - R) break;
            result.Add(new Point(t, x, y, vy));
        }
        return result;
    }

    /// <summary>球下落經過高度 y 的時間與 x；不會經過時回傳 null</summary>
    public (float t, float x)? Predict(float y, float width, float ground)
    {
        var prevY = Y;
        foreach (var p in Trajectory(width, ground))
        {
            if (p.Vy > 0 && prevY < y && p.Y >= y) return (p.T, p.X);
            prevY = p.Y;
        }
        return null;
    }

    /// <summary>讓球以拋物線飛到 (tx, ty)，頂點比起點和終點都高 arc</summary>
    public void Launch(float tx, float ty, (float lo, float hi) arc)
    {
        var g = VolleyConfig.Gravity;
        var apexY = Math.Max(R + 10, Math.Min(Y, ty) - (arc.lo + GD.Randf() * (arc.hi - arc.lo)));
        Vy = -Mathf.Sqrt(2 * g * Math.Max(1, Y - apexY));
        var t = -Vy / g + Mathf.Sqrt(2 * Math.Max(1, ty - apexY) / g);
        Vx = Math.Clamp((tx - X) / t, -VolleyConfig.MaxVx, VolleyConfig.MaxVx);
    }
}
