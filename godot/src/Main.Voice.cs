// 主角色講話：使用者自己的聲音（Discord 裡的自己、麥克風…，見 Signals/IVoiceSource.cs）合起來，主角色跟著動嘴、出現聲波
using DeskPet.Game.Guests;
using DeskPet.Signals;

namespace DeskPet;

public partial class Main
{
    readonly Mouth mainMouth = new();
    readonly View.SpeechWaves mainWaves = new();
    readonly VoiceMixer voice = new();
    /// <summary>主角色的動作與表情由誰控制（講話、之後的聲音規則與快捷鍵…）</summary>
    Game.Control.ControlArbiter control = null!;

    void SetupVoice() => AddChild(mainWaves);

    /// <summary>在 driver.Update 之後、pet.Update 之前呼叫</summary>
    void UpdateMainMouth(float dt)
    {
        var talking = mainMouth.Update(dt, driver, control, voice.Speaking && !performing && !presence.Hidden);
        mainWaves.UpdateWaves(dt, talking, pet, mainMouth.Open);
    }
}
