// 聲音分析（和 preview/js/core/voice.js 同一套計算，tests/golden/voice.json 比對）
// 每 1/60 秒算一次：音量（dB）、是否在講話（噪音門檻＋保持時間）、音調（Hz，自相關）、母音（頻譜外形 → あいうえお）。
// 先降取樣到約 12 kHz，分析最近 512 個樣本（約 43 ms），每幀計算量很小。樣本只在記憶體裡算，不錄音、不存檔。
using System;
using System.Collections.Generic;
using System.Linq;

namespace DeskPet.Core.Voice;

public readonly record struct VoiceFrame(double Db, bool Speaking, double Pitch, string? Vowel)
{
    public static readonly VoiceFrame Silent = new(-100, false, 0, null);
}

public sealed class VoiceAnalyzer
{
    public const double TargetRate = 12000;    // 降取樣後的取樣率（約）
    public const int Window = 512;             // 分析視窗（降取樣後的樣本數）
    public const int Fps = 60;
    public const double Open = 8;              // 比噪音底線大幾 dB 算在講話
    public const double Hold = 0.25;           // 講話停下後保持幾秒（字和字之間不要閉嘴）
    const double MinPitch = 60, MaxPitch = 500, Voiced = 0.6;

    /// <summary>母音的共振峰（F1, F2，Hz），日語五母音的大概位置</summary>
    public static readonly Dictionary<string, double[]> Vowels = new()
    {
        ["a"] = new double[] { 800, 1200 }, ["i"] = new double[] { 300, 2300 }, ["u"] = new double[] { 350, 1300 },
        ["e"] = new double[] { 500, 1900 }, ["o"] = new double[] { 500, 850 },
    };

    readonly int decimate;
    readonly double rate, hop;
    readonly double[] ring = new double[Window];       // 環狀緩衝區（最新的樣本寫在 head）
    readonly double[] buf = new double[Window];        // 分析時排成一列（最舊 → 最新）
    double acc, sinceHop, holdLeft;
    int accN, head;

    /// <summary>噪音底線（dB，校正後設定）</summary>
    public double Floor { get; set; }
    public VoiceFrame Result { get; private set; } = VoiceFrame.Silent;

    public VoiceAnalyzer(double sampleRate, double floorDb = -50)
    {
        decimate = Math.Max(1, (int)Math.Round(sampleRate / TargetRate, MidpointRounding.AwayFromZero));
        rate = sampleRate / decimate;
        hop = sampleRate / Fps;
        Floor = floorDb;
    }

    /// <summary>餵樣本（任意長度）；每滿 1/60 秒分析一次，回傳最新的結果</summary>
    public VoiceFrame Feed(ReadOnlySpan<float> samples)
    {
        foreach (var v in samples)
        {
            acc += v;
            if (++accN == decimate)
            {
                ring[head] = acc / decimate;
                head = (head + 1) % Window;
                acc = 0;
                accN = 0;
            }
            if (++sinceHop >= hop)
            {
                sinceHop -= hop;
                Analyze();
            }
        }
        return Result;
    }

    void Analyze()
    {
        Array.Copy(ring, head, buf, 0, Window - head);
        Array.Copy(ring, 0, buf, Window - head, head);
        double sum = 0;
        foreach (var v in buf) sum += v * v;
        var db = 10 * Math.Log10(sum / buf.Length + 1e-12);
        var loud = db > Floor + Open;
        holdLeft = loud ? Hold : Math.Max(0, holdLeft - 1.0 / Fps);
        var pitch = loud ? Pitch() : 0;
        Result = new VoiceFrame(db, holdLeft > 0, pitch, pitch > 0 ? Vowel() : null);
    }

    // 音調：正規化自相關，取第一個超過最高峰 0.9 倍的峰，拋物線內插
    double Pitch()
    {
        var x = buf;
        var n = x.Length;
        int minLag = (int)Math.Floor(rate / MaxPitch), maxLag = Math.Min(n - 32, (int)Math.Ceiling(rate / MinPitch));
        var r = new double[maxLag + 2];
        // 能量的前綴和：sq[k] = x[0]² + … + x[k-1]²（每個延遲的 xx、yy 不用重算）
        var sq = new double[n + 1];
        for (var i = 0; i < n; i++) sq[i + 1] = sq[i] + x[i] * x[i];
        double best = 0;
        for (var lag = minLag; lag <= maxLag + 1; lag++)
        {
            double xy = 0;
            for (var i = 0; i + lag < n; i++) xy += x[i] * x[i + lag];
            double xx = sq[n - lag], yy = sq[n] - sq[lag];
            r[lag] = xy / (Math.Sqrt(xx * yy) + 1e-12);
            if (lag <= maxLag) best = Math.Max(best, r[lag]);
        }
        if (best < Voiced) return 0;
        for (var lag = minLag + 1; lag <= maxLag; lag++)
        {
            if (r[lag] >= best * 0.9 && r[lag] >= r[lag - 1] && r[lag] >= r[lag + 1])
            {
                // 第 2 諧波很強時會抓到半個週期：兩倍週期的相關明顯更高就用兩倍（低八度）
                var L = lag;
                for (var d = -1; d <= 1; d++)
                {
                    var l2 = 2 * lag + d;
                    if (l2 <= maxLag && r[l2] > r[L] + 0.03 && r[l2] >= r[l2 - 1] && r[l2] >= r[l2 + 1]) L = l2;
                }
                double a = r[L - 1], b = r[L], c = r[L + 1], den = a - 2 * b + c;
                var shift = den != 0 ? 0.5 * (a - c) / den : 0;
                return rate / (L + shift);
            }
        }
        return 0;
    }

    // 母音範本：200～3000 Hz 每 100 Hz 一個頻帶，共振峰位置的高斯包絡（開根號、正規化）
    static readonly double[] BandCenters = Enumerable.Range(0, 29).Select(i => 200.0 + i * 100).ToArray();
    static readonly Dictionary<string, double[]> Templates = Vowels.ToDictionary(kv => kv.Key, kv =>
    {
        var t = BandCenters.Select(fc => Math.Sqrt(kv.Value.Sum(F => Math.Exp(-Math.Pow((fc - F) / 200, 2))))).ToArray();
        var norm = Math.Sqrt(t.Sum(x => x * x));
        return t.Select(x => x / norm).ToArray();
    });

    // 母音：頻譜外形和五個母音的範本比，取最像的（餘弦相似度）
    string? Vowel()
    {
        var n = buf.Length;
        var re = new double[n];
        var im = new double[n];
        for (var i = 0; i < n; i++) re[i] = buf[i] * (0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (n - 1)));
        Fft(re, im);
        var bands = BandCenters.Select(fc =>
        {
            double p = 0;
            for (var k = (int)Math.Ceiling((fc - 75) * n / rate); k <= (int)Math.Floor((fc + 75) * n / rate); k++) p += re[k] * re[k] + im[k] * im[k];
            return Math.Sqrt(p);
        }).ToArray();
        var norm = Math.Sqrt(bands.Sum(v => v * v));
        if (norm <= 0) return null;
        string? best = null;
        double bestScore = -1;
        foreach (var (v, t) in Templates)
        {
            double score = 0;
            for (var i = 0; i < bands.Length; i++) score += bands[i] * t[i];
            score /= norm;
            if (score > bestScore) { bestScore = score; best = v; }
        }
        return best;
    }

    // 原地 FFT（長度是 2 的次方）
    static void Fft(double[] re, double[] im)
    {
        var n = re.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            var bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); }
        }
        for (var len = 2; len <= n; len <<= 1)
        {
            var ang = -2 * Math.PI / len;
            for (var i = 0; i < n; i += len)
                for (var k = 0; k < len / 2; k++)
                {
                    double wr = Math.Cos(ang * k), wi = Math.Sin(ang * k);
                    int a = i + k, b = a + len / 2;
                    double xr = re[b] * wr - im[b] * wi, xi = re[b] * wi + im[b] * wr;
                    re[b] = re[a] - xr; im[b] = im[a] - xi;
                    re[a] += xr; im[a] += xi;
                }
        }
    }

    /// <summary>「比平常高／低幾個半音」：baseline = 使用者平常的音調（校正）</summary>
    public static double Semitones(double pitch, double baseline) => pitch > 0 && baseline > 0 ? 12 * Math.Log2(pitch / baseline) : 0;

    /// <summary>測試用合成音（tests/golden/voice.json）：silence、tone（純音）、vowel（依共振峰加權的諧波）</summary>
    public static float[] Synth(string kind, double f0, double amp, double[] formants, double seconds, int sampleRate)
    {
        var n = (int)Math.Round(seconds * sampleRate, MidpointRounding.AwayFromZero);
        var output = new float[n];
        if (kind == "silence") return output;
        var harmonics = new List<(double f, double g)>();
        for (var h = 1; h * f0 < 3500; h++)
        {
            var f = h * f0;
            var g = kind == "tone" ? (h == 1 ? 1 : 0) : formants.Sum(F => Math.Exp(-Math.Pow((f - F) / 120, 2))) + 0.02;
            if (g > 0) harmonics.Add((f, g));
        }
        var norm = amp / Math.Sqrt(harmonics.Sum(x => x.g * x.g / 2));
        for (var i = 0; i < n; i++)
        {
            double s = 0;
            foreach (var (f, g) in harmonics) s += g * Math.Sin(2 * Math.PI * f * i / sampleRate);
            output[i] = (float)(s * norm);
        }
        return output;
    }
}
