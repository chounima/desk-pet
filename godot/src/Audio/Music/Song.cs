// 內建曲子（data/music/*.json）：旋律與和弦的文字格式解析
// 旋律 token：音名＋八度（C4、F#4、Bb3、R=休止），冒號後是拍數，例如 G4:2；和弦 token：C:4、G7:2
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using DeskPet.Content;

namespace DeskPet.Audio.Music;

public sealed class Song
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public float Bpm { get; set; } = 100;
    public string Note { get; set; } = "";
    public string Melody { get; set; } = "";
    public string Chords { get; set; } = "";

    static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    sealed class Index { public List<string> Songs { get; set; } = new(); }

    public static List<Song> LoadAll()
    {
        var dir = $"{ContentLoader.Root}/data/music";
        var index = JsonSerializer.Deserialize<Index>(ContentLoader.ReadText($"{dir}/index.json"), Options) ?? new();
        var songs = new List<Song>();
        foreach (var id in index.Songs)
        {
            var song = JsonSerializer.Deserialize<Song>(ContentLoader.ReadText($"{dir}/{id}.json"), Options)!;
            song.Id = id;
            song.Title = I18n.Name($"song.{id}", song.Title);
            songs.Add(song);
        }
        return songs;
    }

    /// <summary>(頻率 Hz，休止為 0；拍數)</summary>
    public IEnumerable<(float Freq, float Beats)> Notes()
    {
        foreach (var token in Melody.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var (name, beats) = Split(token);
            yield return (name == "R" ? 0 : Frequency(name), beats);
        }
    }

    /// <summary>(和弦組成音的頻率，拍數)</summary>
    public IEnumerable<(float[] Freqs, float Beats)> ChordList()
    {
        foreach (var token in Chords.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var (name, beats) = Split(token);
            yield return (ChordTones(name), beats);
        }
    }

    public float TotalBeats()
    {
        float sum = 0;
        foreach (var (_, b) in Notes()) sum += b;
        return sum;
    }

    static (string Name, float Beats) Split(string token)
    {
        var parts = token.Split(':');
        return (parts[0], parts.Length > 1 ? float.Parse(parts[1], CultureInfo.InvariantCulture) : 1);
    }

    static readonly Dictionary<string, int> Semitone = new()
    {
        ["C"] = 0, ["D"] = 2, ["E"] = 4, ["F"] = 5, ["G"] = 7, ["A"] = 9, ["B"] = 11,
    };

    /// <summary>音名（C4、F#4、Bb3）→ 頻率，A4 = 440Hz</summary>
    public static float Frequency(string note)
    {
        var semi = Semitone[note[..1]];
        var i = 1;
        if (note[i] == '#') { semi++; i++; }
        else if (note[i] == 'b') { semi--; i++; }
        var octave = int.Parse(note[i..]);
        var midi = (octave + 1) * 12 + semi;
        return 440f * MathF.Pow(2, (midi - 69) / 12f);
    }

    /// <summary>和弦（C、Am、G7、F#m…）→ 吉他低音區的組成音</summary>
    static float[] ChordTones(string chord)
    {
        var minor = chord.EndsWith("m");
        var seventh = chord.EndsWith("7");
        var root = chord.TrimEnd('m', '7');
        var rootMidi = 48 + Semitone[root[..1]] + (root.Length > 1 ? (root[1] == '#' ? 1 : -1) : 0);   // C3 附近
        if (rootMidi > 52) rootMidi -= 12;           // 太高的根音降一個八度，聽起來比較像吉他
        var intervals = new List<int> { 0, minor ? 3 : 4, 7, 12 };
        if (seventh) intervals[3] = 10;
        return intervals.ConvertAll(n => 440f * MathF.Pow(2, (rootMidi + n - 69) / 12f)).ToArray();
    }
}
