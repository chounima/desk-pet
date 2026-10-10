// 排球：圖檔轉成貼圖、跟著球旋轉；火焰模式時加上光暈與火花粒子
using DeskPet.Content;
using DeskPet.Game.Minigames;
using DeskPet.Platform;
using Godot;

namespace DeskPet.View;

public partial class BallView : Node2D
{
    const float Oversample = 2;

    Sprite2D ball = null!;
    Sprite2D glow = null!;
    CpuParticles2D flames = null!;
    float radius;

    public void Init(PropData prop, float r)
    {
        radius = r;
        var tex = SvgTextures.Load(prop.Path, r / VolleyConfig.ArtRadius * Oversample);
        ball = new Sprite2D { Texture = tex.Texture, Scale = Vector2.One / Oversample };

        var glowTex = RadialTexture(128, new Color(1, 0.75f, 0.25f, 0.85f), new Color(1, 0.3f, 0, 0));
        glow = new Sprite2D { Texture = glowTex, Scale = Vector2.One * (r * 3.2f / 128), Visible = false };

        // 火花：不跟著球（LocalCoords = false），留下尾巴
        var ramp = new Gradient();
        ramp.SetColor(0, new Color(1, 0.95f, 0.6f, 1));
        ramp.SetColor(1, new Color(1, 0.25f, 0, 0));
        ramp.AddPoint(0.45f, new Color(1, 0.55f, 0.1f, 0.9f));
        var u = DesktopWindow.UiScale;
        flames = new CpuParticles2D
        {
            Amount = 48, Lifetime = 0.45f, LocalCoords = false, Emitting = false,
            Texture = RadialTexture(64, Colors.White, new Color(1, 1, 1, 0)),
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere, EmissionSphereRadius = r * 0.5f,
            Direction = Vector2.Up, Spread = 40, Gravity = new Vector2(0, -260 * u),
            InitialVelocityMin = 10 * u, InitialVelocityMax = 50 * u,
            ScaleAmountMin = r * 0.7f / 64, ScaleAmountMax = r * 1.3f / 64,
            ColorRamp = ramp,
        };

        AddChild(flames);
        AddChild(glow);
        AddChild(ball);
        Visible = false;
    }

    public void Show(VolleyBall b, bool onFire)
    {
        Visible = true;
        Position = b.Position;
        ball.Rotation = b.Rot;
        glow.Visible = onFire;
        flames.Emitting = onFire;
    }

    public void HideBall()
    {
        Visible = false;
        flames.Emitting = false;
    }

    /// <summary>畫面上的點是否點在球上</summary>
    public bool HitTest(Vector2 point) => Visible && point.DistanceTo(Position) <= radius * 1.2f;

    static GradientTexture2D RadialTexture(int size, Color inner, Color outer)
    {
        var g = new Gradient();
        g.SetColor(0, inner);
        g.SetColor(1, outer);
        return new GradientTexture2D
        {
            Gradient = g, Width = size, Height = size,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0),
        };
    }
}
