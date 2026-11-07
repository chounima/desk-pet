// 驅動方式：誰決定角色這一幀在哪、做什麼動作。Pet 只管外觀與姿勢，驅動者換掉就能換玩法
//
// 桌面漫遊（Behavior，預設）、原地不動（StillDriver：錄影、之後的半身模式）……同一時間只有一個在驅動。
// 小遊戲（排球、彈吉他）目前還是用 Behavior.EnterMinigame 暫停漫遊後直接操作 Pet，之後改成驅動者時再搬。
namespace DeskPet.Game.Drivers;

public interface IPetDriver
{
    string Id { get; }

    /// <summary>每幀：移動、選動作（在 Pet.Update 之前）</summary>
    void Update(float dt);

    /// <summary>外部請求做某個動作（經過 ControlArbiter）；做不到回傳 false</summary>
    bool TryStart(string action);

    /// <summary>現在可以改表情、動嘴嗎（站在地上、沒在小遊戲、沒在睡）</summary>
    bool CanEmote { get; }

    /// <summary>有動靜（講話、互動）：不要睡著</summary>
    void Touch();
}
