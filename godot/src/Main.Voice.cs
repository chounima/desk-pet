// 主角色講話：使用者自己的聲音（Discord 裡的自己、麥克風…，見 Signals/IVoiceSource.cs）合起來，主角色跟著動嘴、出現聲波
using DeskPet.Game.Guests;
using DeskPet.Signals;

namespace DeskPet;

public partial class Main
{
    readonly Mouth mainMouth = new();
    bool mainTalking;       // 這一幀主角色有沒有在動嘴（掛件的聲波用）
    readonly VoiceMixer voice = new();
    /// <summary>主角色的動作與表情由誰控制（講話、之後的聲音規則與快捷鍵…）</summary>
    Game.Control.ControlArbiter control = null!;

    /// <summary>在 driver.Update 之後、pet.Update 之前呼叫</summary>
    void UpdateMainMouth(float dt)
    {
        // 講話（麥克風、Discord）動嘴並出聲波；冒台詞泡泡時只動嘴
        var speaking = voice.Speaking && !performing && !presence.Hidden;
        mainTalking = mainMouth.Update(dt, driver, control, speaking || bubble.Talking) && speaking;
    }
}
