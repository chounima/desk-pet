// 程式合成的音效：不需要音檔、沒有版權問題。之後要換正式音效，只要改成載入音檔即可。
using System;
using System.Collections.Generic;
using Godot;

namespace DeskPet.Audio;

public static class Synth
{
    const int Rate = 22050;
    static readonly Random Rng = new(1234);

    /// <summary>所有音效（名稱 → 聲音）</summary>
    public static Dictionary<string, AudioStreamWav> BuildAll() => new()
    {
        ["pop"] = Make(0.09f, Sweep(520, 980, 0.09f, Sine), t => Decay(t, 35)),
        ["boing"] = Make(0.32f, Vibrato(t => 220 + 380 * t / 0.32f, 18, 25), t => Decay(t, 6) * 0.8f),
        ["thud"] = Make(0.15f, Mix(Sweep(130, 55, 0.14f, Sine), 1f, Noise, 0.4f), t => Decay(t, 25)),
        ["squeak"] = Make(0.15f, Sweep(950, 1350, 0.15f, Triangle), t => Mathf.Sin(Mathf.Pi * t / 0.15f) * 0.6f),
        ["fall"] = Make(0.45f, Sweep(900, 250, 0.45f, Sine), t => (1 - t / 0.45f) * 0.5f),
        ["chime"] = Make(0.5f, Chord(880, 1320, 1760), t => Decay(t, 7) * 0.5f),
        ["bonk"] = Make(0.12f, Sweep(320, 200, 0.12f, Sine), t => Decay(t, 25)),
        ["bump"] = Make(0.08f, Mix(Sweep(260, 180, 0.08f, Sine), 1f, Noise, 0.5f), t => Decay(t, 30)),
        ["spike"] = Make(0.2f, Mix(Sweep(140, 70, 0.2f, Sine), 1f, Noise, 0.8f), t => Decay(t, 18)),
        ["whoosh"] = Make(0.3f, LowNoise(0.15f), t => Mathf.Sin(Mathf.Pi * t / 0.3f) * 0.7f),
    };

    // ---------- 組合 ----------

    static AudioStreamWav Make(float duration, Func<float, float> wave, Func<float, float> envelope)
    {
        var n = (int)(duration * Rate);
        var data = new byte[n * 2];
        for (var i = 0; i < n; i++)
        {
            var t = (float)i / Rate;
            var attack = Math.Min(1, t * 400);         // 2.5ms 淡入，避免爆音
            var v = Math.Clamp(wave(t) * envelope(t) * attack, -1, 1);
            var s = (short)(v * 30000);
            data[i * 2] = (byte)s;
            data[i * 2 + 1] = (byte)(s >> 8);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = false, Data = data };
    }

    static float Decay(float t, float k) => Mathf.Exp(-t * k);

    // ---------- 波形 ----------

    static float Sine(float phase) => Mathf.Sin(phase * Mathf.Tau);
    static float Triangle(float phase) => 4 * Math.Abs(phase - Mathf.Floor(phase + 0.5f)) - 1;
    static float Noise(float _) => (float)(Rng.NextDouble() * 2 - 1);

    /// <summary>頻率從 f0 線性滑到 f1（相位累加，滑音才不會有雜音）</summary>
    static Func<float, float> Sweep(float f0, float f1, float duration, Func<float, float> shape) =>
        Oscillator(t => f0 + (f1 - f0) * Math.Min(1, t / duration), shape);

    static Func<float, float> Vibrato(Func<float, float> freq, float rate, float depth) =>
        Oscillator(t => freq(t) + depth * Mathf.Sin(Mathf.Tau * rate * t), Sine);

    static Func<float, float> Oscillator(Func<float, float> freq, Func<float, float> shape)
    {
        double phase = 0;
        return t =>
        {
            phase += freq(t) / Rate;
            return shape((float)(phase % 1));
        };
    }

    static Func<float, float> Chord(params float[] freqs) => t =>
    {
        float v = 0, w = 1;
        foreach (var f in freqs) { v += Mathf.Sin(Mathf.Tau * f * t) * w; w *= 0.6f; }
        return v / 2;
    };

    static Func<float, float> Mix(Func<float, float> a, float wa, Func<float, float> b, float wb) =>
        t => a(t) * wa + b(t) * wb;

    /// <summary>低通濾過的雜音（風聲）</summary>
    static Func<float, float> LowNoise(float smoothing)
    {
        float y = 0;
        return t => y += (Noise(t) - y) * smoothing;
    }
}
