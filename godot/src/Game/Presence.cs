// 桌寵要不要出現在畫面上：使用者暫時隱藏（定時回來）、其他程式全螢幕時自動躲起來
using System;

namespace DeskPet.Game;

public sealed class Presence
{
    double hideUntil;               // 手動隱藏到什麼時候（秒，Time.GetTicksMsec / 1000）
    bool fullscreen;

    /// <summary>全螢幕時自動隱藏（系統匣可關）</summary>
    public bool AutoHideFullscreen { get; set; } = true;

    public bool Hidden => ManualHidden || (AutoHideFullscreen && fullscreen);
    public bool ManualHidden => Now() < hideUntil;

    /// <summary>顯示狀態改變：true = 剛隱藏、false = 剛回來</summary>
    public event Action<bool>? Changed;

    static double Now() => Godot.Time.GetTicksMsec() / 1000.0;

    public void HideFor(float minutes) => Apply(() => hideUntil = Now() + minutes * 60);

    public void ShowNow() => Apply(() => hideUntil = 0);

    public void SetFullscreen(bool on) => Apply(() => fullscreen = on);

    /// <summary>每幀呼叫，手動隱藏時間到了要回來</summary>
    public void Update() => Apply(() => { });

    bool lastHidden;

    void Apply(Action change)
    {
        change();
        if (Hidden == lastHidden) return;
        lastHidden = Hidden;
        Changed?.Invoke(Hidden);
    }
}
