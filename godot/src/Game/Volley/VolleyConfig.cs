// 排球對打參數（對應 preview/js/config.js 的 VOLLEY）
// 長度單位：畫面 px，已乘上 Windows 顯示縮放；Reach / HitHeight / ApproachDist / DiveSlide 是 Q 版單位，會再乘上角色身材
using DeskPet.Platform;

namespace DeskPet.Game.Minigames;

public static class VolleyConfig
{
    static float U => DesktopWindow.UiScale;

    public static float Gravity => 900 * U;            // 球的重力，比真實小，比較好接
    public static float BallRadius => 26 * U;          // 球的顯示半徑
    public const float ArtRadius = 14;                 // 排球圖檔裡的半徑（= tools/artgen/props.py 的 BALL_R）
    public const float Reach = 44;                     // 擊球點在身體前方多遠
    public const float HitHeight = 104;                // 擊球點離地多高
    public static float HitTolerance => 34 * U;        // 球離擊球點多近算接到
    public static float RunSpeed => 420 * U;           // 跑去接球的速度
    public const float ReactDelay = 0.2f;              // 看到球飛來後多久開始跑
    public static (float, float) Arc => (150 * U, 280 * U);      // 回球的拋物線高度
    public static (float, float) LobArc => (200 * U, 400 * U);   // 點球、救球時的高吊球
    public static float MaxVx => 1200 * U;
    public static float CursorRadius => 20 * U;        // 游標擊球的判定半徑
    public static float MinSwipe => 150 * U;           // 游標至少要這麼快才算揮擊
    public static float ServeToss => 620 * U;          // 發球時往上拋的初速
    public static float DigSpeed => 650 * U;           // 球速超過這個值時，游標碰到就算接起
    public const float MissBase = 0.03f, MissPerRally = 0.012f, MissMax = 0.3f;   // 漏接機率（再乘角色的 miss）
    public const float RestartAfter = 2.2f;            // 一回合結束後多久重新發球

    // 殺球：三步助跑（vbApproach）→ 起跳揮臂（vbSpike）
    public const float ApproachTime = 0.6f;            // = vbApproach.json 的 duration
    public const float ApproachDist = 70;
    public static readonly float[] SpikeTimes = { 0.35f, 0.3f, 0.4f, 0.45f };   // 揮臂過程中可擊球的時刻（依偏好排序）
    public static float SpikeSpeed => 1100 * U;        // 殺球球速（再乘角色的 power）

    // 魚躍救球（vbDive）
    public const float DiveContact = 0.35f;
    public const float DiveSlide = 70;
    public const float ContactWindow = 0.08f;          // 擊球時間點前後的容許誤差 (s)

    public const int FireAt = 10;                      // 連續來回幾次後球燒起來
}
