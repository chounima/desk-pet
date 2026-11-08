// 聲音校正：環境噪音底線、使用者平常講話的音量與音調，自動慢慢跟著調整（換人、換麥克風也能用）
// 規則一律用「比平常大幾 dB」「比平常高幾個半音」判斷。按「重新校正」會清掉重來。
using System;
using System.Collections.Generic;
using System.Linq;

namespace DeskPet.Core.Voice;

public sealed class VoiceCalibration
{
    const int NoiseFrames = 180;        // 最近 3 秒沒講話的音量
    const int SpeechFrames = 600;       // 最近 10 秒講話時的音量、音調
    const double FloorMargin = 3;       // 噪音底線 = 安靜時音量的 90% 分位 + 這個

    readonly Queue<double> noise = new(), speechDb = new(), pitches = new();

    /// <summary>噪音底線（dB），交給 VoiceAnalyzer.Floor</summary>
    public double Floor { get; private set; }
    /// <summary>平常講話的音量（dB，中位數）；還沒聽到講話時 = 底線 + 20</summary>
    public double SpeechDb => speechDb.Count >= 30 ? Median(speechDb) : Floor + 20;
    /// <summary>平常的音調（Hz，中位數）；還沒聽到 = 0（音調規則先不動）</summary>
    public double BasePitch => pitches.Count >= 30 ? Median(pitches) : 0;

    /// <summary>手動固定底線（使用者拉了門檻拉桿）：不再自動調整，直到 Reset</summary>
    public bool Manual { get; private set; }

    public VoiceCalibration(double floor = -50) => Floor = floor;

    public void SetManual(double floor)
    {
        Manual = true;
        Floor = Math.Clamp(floor, -90, -10);
    }

    public void Reset(double floor = -50)
    {
        noise.Clear();
        speechDb.Clear();
        pitches.Clear();
        Floor = floor;
        Manual = false;
    }

    public void Update(VoiceFrame f)
    {
        if (!f.Speaking)
        {
            Push(noise, f.Db, NoiseFrames);
            if (noise.Count >= 60 && !Manual)
            {
                var sorted = noise.OrderBy(v => v).ToList();
                Floor = Math.Clamp(sorted[(int)(sorted.Count * 0.9)] + FloorMargin, -75, -20);
            }
        }
        else
        {
            Push(speechDb, f.Db, SpeechFrames);
            if (f.Pitch > 0) Push(pitches, f.Pitch, SpeechFrames);
        }
    }

    static void Push(Queue<double> q, double v, int max)
    {
        q.Enqueue(v);
        while (q.Count > max) q.Dequeue();
    }

    static double Median(IEnumerable<double> values)
    {
        var s = values.OrderBy(v => v).ToList();
        return s[s.Count / 2];
    }
}
