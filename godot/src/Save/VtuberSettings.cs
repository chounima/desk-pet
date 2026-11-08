// 半身 VTuber 模式的設定（settings.json 的 "Vtuber" 區段）
namespace DeskPet.Save;

public sealed class VtuberSettings
{
    /// <summary>開著半身模式（啟動時自動開）</summary>
    public bool Enabled { get; set; }
    /// <summary>半身角色（空的 = 和桌面上的主角色一樣）</summary>
    public string Character { get; set; } = "";
    /// <summary>位置（左上角，視窗座標；-1 = 預設放在右下角）與高度（px）</summary>
    public float X { get; set; } = -1;
    public float Y { get; set; } = -1;
    public float Height { get; set; }
    /// <summary>麥克風（Godot 的輸入裝置名稱，空的 = 系統預設）</summary>
    public string MicDevice { get; set; } = "";
    /// <summary>聲音觸發表情與動作（data/vtuber.json 的 rules）</summary>
    public bool VoiceRules { get; set; } = true;
    /// <summary>快捷鍵表情（Ctrl+Alt+數字）</summary>
    public bool Hotkeys { get; set; } = true;
    /// <summary>半身模式開著時，桌面上的全身寵物先藏起來</summary>
    public bool HideDesktopPet { get; set; }
    /// <summary>全螢幕程式（遊戲）時也顯示（直播全螢幕遊戲用）</summary>
    public bool ShowOnFullscreen { get; set; } = true;
    /// <summary>面向角度：0 正面、90 側面（朝右）、180 背面、270 左側；選最接近的視角（角色要有那個視角的圖）</summary>
    public float ViewYaw { get; set; } = 90;
    /// <summary>跟著游標轉身：游標在左右兩邊時，角度往那邊轉（最多 TurnRange 度）</summary>
    public bool AutoTurn { get; set; }
    public float TurnRange { get; set; } = 60;
    /// <summary>上次校正的噪音底線（dB）</summary>
    public double NoiseFloor { get; set; } = -50;
    /// <summary>噪音底線自動學（false = 使用者用拉桿固定了門檻，NoiseFloor 就是固定值）</summary>
    public bool AutoFloor { get; set; } = true;
}
