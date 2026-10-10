// 從正在播放的聲音抓拍點（自訂音樂沒有 BPM 資訊時用）：
// 低音區的能量突然比最近平均高很多 → 一拍；用最近幾拍的間隔估計 BPM
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DeskPet.Audio.Music;

public sealed class BeatDetector
{
    readonly AudioEffectSpectrumAnalyzerInstance analyzer;
    float average;
    double lastBeat = -1;
    readonly Queue<double> intervals = new();

    public BeatDetector(AudioEffectSpectrumAnalyzerInstance analyzer) => this.analyzer = analyzer;

    /// <summary>估計的 BPM（還不夠資料時為 null）</summary>
    public float? Bpm { get; private set; }

    public void Reset()
    {
        average = 0;
        lastBeat = -1;
        intervals.Clear();
        Bpm = null;
    }

    /// <summary>每幀呼叫；這一幀是一拍時回傳 true</summary>
    public bool Update(float dt, double now)
    {
        var energy = analyzer.GetMagnitudeForFrequencyRange(30, 160).Length();
        average = average == 0 ? energy : Mathf.Lerp(average, energy, Math.Min(1, dt * 3));
        // 至少相隔 0.28 秒（約 210 BPM 以內），避免同一個鼓點算兩次
        if (energy < 0.004f || energy < average * 1.45f || now - lastBeat < 0.28) return false;
        if (lastBeat > 0)
        {
            var gap = now - lastBeat;
            if (gap < 1.5)
            {
                intervals.Enqueue(gap);
                while (intervals.Count > 8) intervals.Dequeue();
                if (intervals.Count >= 4)
                {
                    var median = intervals.OrderBy(x => x).ElementAt(intervals.Count / 2);
                    Bpm = (float)(60 / median);
                }
            }
        }
        lastBeat = now;
        return true;
    }
}
