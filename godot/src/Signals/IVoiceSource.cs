// 「使用者自己的聲音」：Discord 裡的自己在講話、麥克風有聲音……都實作這個介面，主角色依合起來的結果動嘴
// 之後的聲音分析（第 6 階段：音調、母音）也是一個 IVoiceSource，換上去不用改 Discord 和麥克風的程式
using System.Collections.Generic;
using System.Linq;

namespace DeskPet.Signals;

public interface IVoiceSource
{
    string Id { get; }

    /// <summary>使用者自己現在在講話（背景執行緒寫入，主執行緒讀）</summary>
    bool SelfSpeaking { get; }

    /// <summary>音量 0～1；這個來源量不到就是 -1（例如 Discord 只知道有沒有在講話）</summary>
    float SelfLevel { get; }
}

/// <summary>把多個聲音來源合起來：任何一個說在講話就算在講話，音量取最大</summary>
public sealed class VoiceMixer
{
    readonly List<IVoiceSource> sources = new();

    public void Add(IVoiceSource source)
    {
        if (!sources.Contains(source)) sources.Add(source);
    }

    public void Remove(IVoiceSource source) => sources.Remove(source);

    public bool Speaking => sources.Any(s => s.SelfSpeaking);

    public float Level => sources.Count == 0 ? -1 : sources.Max(s => s.SelfLevel);

    public string Describe() => string.Join(",", sources.Select(s => $"{s.Id}{(s.SelfSpeaking ? "🎤" : "")}{(s.SelfLevel >= 0 ? $"={s.SelfLevel:0.00}" : "")}"));
}
