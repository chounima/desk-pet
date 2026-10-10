// 撥弦合成（Karplus-Strong）：一小段雜訊在延遲線裡反覆平均，聽起來就像撥吉他弦
// 把整首內建曲子（旋律＋每拍刷和弦）預先算成一段 PCM 聲音
using System;
using Godot;

namespace DeskPet.Audio.Music;

public static class PluckRenderer
{
    const int Rate = 22050;

    public static AudioStreamWav Render(Song song)
    {
        var beat = 60f / song.Bpm;
        var total = song.TotalBeats() * beat + 1.5f;            // 最後一個音留點餘音
        var mix = new float[(int)(total * Rate)];
        var rng = new Random(song.Id.GetHashCode());

        // 旋律
        float t = 0;
        foreach (var (freq, beats) in song.Notes())
        {
            if (freq > 0) Pluck(mix, t, freq, Math.Min(beats * beat + 0.6f, 2.5f), 0.55f, 0.996f, rng);
            t += beats * beat;
        }

        // 和弦：每拍往下刷一次（各弦相差 12ms），第一拍重一點
        t = 0;
        foreach (var (freqs, beats) in song.ChordList())
        {
            for (var b = 0; b < (int)Math.Ceiling(beats); b++)
            {
                var strength = b == 0 ? 0.22f : 0.14f;
                for (var s = 0; s < freqs.Length; s++)
                    Pluck(mix, t + b * beat + s * 0.012f, freqs[s], beat * 1.2f, strength, 0.994f, rng);
            }
            t += beats * beat;
        }

        // 正規化到 -1~1 並轉成 16-bit
        var peak = 0.001f;
        foreach (var v in mix) peak = Math.Max(peak, Math.Abs(v));
        var gain = 0.85f / peak;
        var data = new byte[mix.Length * 2];
        for (var i = 0; i < mix.Length; i++)
        {
            var s = (short)(Math.Clamp(mix[i] * gain, -1, 1) * 30000);
            data[i * 2] = (byte)s;
            data[i * 2 + 1] = (byte)(s >> 8);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = false, Data = data };
    }

    /// <param name="decay">每次平均的衰減（越接近 1 餘音越長）</param>
    static void Pluck(float[] mix, float start, float freq, float duration, float volume, float decay, Random rng)
    {
        var period = Math.Max(2, (int)(Rate / freq));
        var line = new float[period];
        for (var i = 0; i < period; i++) line[i] = (float)(rng.NextDouble() * 2 - 1);
        var begin = (int)(start * Rate);
        var length = Math.Min((int)(duration * Rate), mix.Length - begin);
        var idx = 0;
        for (var i = 0; i < length; i++)
        {
            var next = (idx + 1) % period;
            var v = line[idx];
            line[idx] = (v + line[next]) * 0.5f * decay;
            idx = next;
            var fadeOut = i > length - 400 ? (length - i) / 400f : 1;     // 結尾淡出，避免喀聲
            mix[begin + i] += v * volume * fadeOut;
        }
    }
}
