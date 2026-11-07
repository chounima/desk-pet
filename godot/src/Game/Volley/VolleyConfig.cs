// 排球對打參數：讀 data/tuning.json 的 volley（網頁預覽 preview/js/config.js 讀同一份）
// 長度單位：畫面 px，這裡再乘上 Windows 顯示縮放；Reach / HitHeight / ApproachDist / DiveSlide 是 Q 版單位，會再乘上角色身材
using DeskPet.Platform;
using System.Text.Json.Nodes;
using static DeskPet.Game.Tuning;

namespace DeskPet.Game.Minigames;

public static class VolleyConfig
{
    static readonly JsonNode V = Data["volley"]!;
    static float U => DesktopWindow.UiScale;
    static float Px(string key) => F(V, key) * U;
    static (float, float) Range(string key) => (Floats(V[key])![0] * U, Floats(V[key])![1] * U);

    public static float Gravity => Px("gravity");              // 球的重力，比真實小，比較好接
    public static float BallRadius => Px("ballRadius");        // 球的顯示半徑
    public static readonly float ArtRadius = F(V, "artRadius"); // 排球圖檔裡的半徑（= tools/artgen/balls.py 的 BALL_R）
    public static readonly float Reach = F(V, "reach");         // 擊球點在身體前方多遠
    public static readonly float HitHeight = F(V, "hitHeight"); // 擊球點離地多高
    public static float HitTolerance => Px("hitTolerance");    // 球離擊球點多近算接到
    public static float RunSpeed => Px("runSpeed");            // 跑去接球的速度
    public static readonly float ReactDelay = F(V, "reactDelay");   // 看到球飛來後多久開始跑
    public static (float, float) Arc => Range("arc");          // 回球的拋物線高度
    public static (float, float) LobArc => Range("lobArc");    // 點球、救球時的高吊球
    public static float MaxVx => Px("maxVx");
    public static float CursorRadius => Px("cursorRadius");    // 游標擊球的判定半徑
    public static float MinSwipe => Px("minSwipe");            // 游標至少要這麼快才算揮擊
    public static float ServeToss => Px("serveToss");          // 發球時往上拋的初速
    public static float DigSpeed => Px("digSpeed");            // 球速超過這個值時，游標碰到就算接起
    public static readonly float MissBase = F(V["miss"]!, "base"), MissPerRally = F(V["miss"]!, "perRally"), MissMax = F(V["miss"]!, "max");   // 漏接機率（再乘角色的 miss）
    public static readonly float RestartAfter = F(V, "restartAfter");   // 一回合結束後多久重新發球

    // 殺球：三步助跑（vbApproach）→ 起跳揮臂（vbSpike）
    public static readonly float ApproachTime = F(V, "approachTime");   // = vbApproach.json 的 duration
    public static readonly float ApproachDist = F(V, "approachDist");
    public static readonly float[] SpikeTimes = Floats(V["spikeTimes"])!;   // 揮臂過程中可擊球的時刻（依偏好排序）
    public static float SpikeSpeed => Px("spikeSpeed");        // 殺球球速（再乘角色的 power）

    // 魚躍救球（vbDive）
    public static readonly float DiveContact = F(V, "diveContact");
    public static readonly float DiveSlide = F(V, "diveSlide");
    public static readonly float ContactWindow = F(V, "contactWindow");  // 擊球時間點前後的容許誤差 (s)

    public static readonly int FireAt = (int)F(V, "fireAt");   // 連續來回幾次後球燒起來
}
