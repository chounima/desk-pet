// 站立面：地面（工作區底邊）或某個視窗的頂邊
using System;
using System.Collections.Generic;
using DeskPet.Platform;
using Godot;

namespace DeskPet.Game;

public sealed class Surfaces
{
    readonly Func<Rect2> area;
    readonly Func<IReadOnlyList<WindowPlatform>> platforms;

    public Surfaces(Func<Rect2> area, Func<IReadOnlyList<WindowPlatform>> platforms)
    {
        this.area = area;
        this.platforms = platforms;
    }

    /// <summary>目前站的視窗平台；null = 站在地面</summary>
    public WindowPlatform? Current { get; private set; }

    public float GroundY => area().End.Y;
    public float Y => Current?.Y ?? GroundY;

    /// <summary>目前站立面的左右範圍</summary>
    public (float lo, float hi) Span => Current is { } p ? (p.X0, p.X1) : (area().Position.X, area().End.X);

    public IReadOnlyList<WindowPlatform> All => platforms();

    public void Stand(WindowPlatform? platform) => Current = platform;

    /// <summary>
    /// 跟著腳下的視窗更新位置：視窗移動了就一起移動（dx）。
    /// 視窗關閉、最小化，或腳下那段被別的視窗擋住時回傳 false（該掉下去了）。
    /// </summary>
    public bool Follow(float x, out float dx)
    {
        dx = 0;
        if (Current is not { } cur) return true;
        foreach (var p in platforms())
        {
            if (p.Hwnd != cur.Hwnd) continue;
            var d = p.WinX - cur.WinX;
            if (!p.Contains(x + d, -2)) continue;
            Current = p;
            dx = d;
            return true;
        }
        Current = null;
        return false;
    }

    /// <summary>從 y0 往下移動到 y1 的途中，在 x 處碰到的最高平台；沒碰到回傳 null</summary>
    public WindowPlatform? LandingBetween(float x, float y0, float y1)
    {
        WindowPlatform? best = null;
        foreach (var p in platforms())
            if (p.Y >= y0 && p.Y <= y1 && p.Contains(x, 4) && (best == null || p.Y < best.Value.Y))
                best = p;
        return best;
    }
}
