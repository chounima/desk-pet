// 音樂播放器：內建曲子（撥弦合成）＋自訂音樂資料夾（文件\DeskPet\music 裡的 mp3／ogg／wav）
// 播放時每一拍發出 Beat 事件，角色跟著刷弦。內建曲知道 BPM；自訂音樂可在 bpm.json 指定，否則從聲音抓拍點。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

namespace DeskPet.Audio.Music;

public sealed record Track(string Id, string Title, string? SongId, string? FilePath, float? Bpm)
{
    public bool Builtin => SongId != null;
}

public partial class MusicPlayer : Node
{
    const string BusName = "Music";

    readonly Dictionary<string, Song> songs;
    readonly Dictionary<string, AudioStream> cache = new();
    AudioStreamPlayer player = null!;
    BeatDetector detector = null!;
    int lastBeatIndex = -1;
    double clock;
    readonly Random rng = new();

    public List<Track> Tracks { get; } = new();
    public Track? Current { get; private set; }
    public bool Playing => player.Playing;
    public bool Shuffle { get; set; }
    /// <summary>播完整個清單後從頭再來</summary>
    public bool Loop { get; set; } = true;

    /// <summary>目前的 BPM（內建曲或 bpm.json 指定；自訂音樂則用估計值）</summary>
    public float? Bpm => Current?.Bpm ?? detector.Bpm;

    public event Action<Track>? TrackStarted;
    public event Action? Stopped;
    public event Action? Beat;

    public static string Folder => Path.Combine(OS.GetSystemDir(OS.SystemDir.Documents), "DeskPet", "music");

    public MusicPlayer(IEnumerable<Song> songs) => this.songs = songs.ToDictionary(s => s.Id);

    public override void _Ready()
    {
        var bus = AudioServer.GetBusIndex(BusName);
        if (bus < 0)
        {
            AudioServer.AddBus();
            bus = AudioServer.BusCount - 1;
            AudioServer.SetBusName(bus, BusName);
            AudioServer.AddBusEffect(bus, new AudioEffectSpectrumAnalyzer { BufferLength = 0.1f });
        }
        detector = new BeatDetector((AudioEffectSpectrumAnalyzerInstance)AudioServer.GetBusEffectInstance(bus, 0));
        player = new AudioStreamPlayer { Bus = BusName };
        player.Finished += Next;
        AddChild(player);
    }

    /// <summary>音量 0～1</summary>
    public float Volume
    {
        get => Mathf.DbToLinear(player.VolumeDb);
        set => player.VolumeDb = Mathf.LinearToDb(Mathf.Clamp(value, 0.0001f, 1));
    }

    /// <summary>重新整理播放清單：已擁有的內建曲 + 自訂資料夾裡的檔案</summary>
    public void RefreshTracks(Func<string, bool> ownsSong)
    {
        Tracks.Clear();
        foreach (var s in songs.Values.Where(s => ownsSong(s.Id)))
            Tracks.Add(new Track($"song:{s.Id}", s.Title, s.Id, null, s.Bpm));
        foreach (var t in ScanFolder()) Tracks.Add(t);
    }

    static readonly string[] Extensions = { ".mp3", ".ogg", ".wav" };

    static IEnumerable<Track> ScanFolder()
    {
        EnsureFolder();
        var bpms = new Dictionary<string, float>();
        var bpmFile = Path.Combine(Folder, "bpm.json");
        try
        {
            if (File.Exists(bpmFile))
                bpms = JsonSerializer.Deserialize<Dictionary<string, float>>(File.ReadAllText(bpmFile)) ?? new();
        }
        catch (Exception e) { GD.PushWarning($"[DeskPet] bpm.json 格式錯誤：{e.Message}"); }

        return Directory.EnumerateFiles(Folder)
            .Where(f => Extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Select(f => new Track($"file:{Path.GetFileName(f)}", Path.GetFileNameWithoutExtension(f), null, f,
                                   bpms.TryGetValue(Path.GetFileName(f), out var b) ? b : null));
    }

    /// <summary>第一次使用時建立資料夾並放一份說明</summary>
    public static void EnsureFolder()
    {
        Directory.CreateDirectory(Folder);
        var readme = Path.Combine(Folder, "說明.txt");
        if (!File.Exists(readme))
            File.WriteAllText(readme,
                "把 mp3、ogg、wav 音樂檔放在這個資料夾，桌寵彈吉他時就可以選來播放。\r\n" +
                "角色會自動抓拍子刷弦；想要更準，可以建立 bpm.json 指定每首歌的速度，例如：\r\n" +
                "{ \"我的歌.mp3\": 120, \"另一首.ogg\": 96 }\r\n");
    }

    public void Play(Track track)
    {
        var stream = Load(track);
        if (stream == null) { Next(); return; }
        Current = track;
        player.Stream = stream;
        player.Play();
        detector.Reset();
        lastBeatIndex = -1;
        TrackStarted?.Invoke(track);
    }

    public void PlayFirst()
    {
        if (Tracks.Count == 0) return;
        Play(Shuffle ? Tracks[rng.Next(Tracks.Count)] : Tracks[0]);
    }

    public void Stop()
    {
        if (Current == null && !player.Playing) return;
        player.Stop();
        Current = null;
        Stopped?.Invoke();
    }

    void Next()
    {
        if (Tracks.Count == 0 || Current == null) { Stop(); return; }
        var i = Tracks.FindIndex(t => t.Id == Current.Id);
        if (Shuffle && Tracks.Count > 1)
        {
            int j;
            do j = rng.Next(Tracks.Count); while (j == i);
            Play(Tracks[j]);
        }
        else if (i + 1 < Tracks.Count) Play(Tracks[i + 1]);
        else if (Loop) Play(Tracks[0]);
        else Stop();
    }

    AudioStream? Load(Track track)
    {
        if (cache.TryGetValue(track.Id, out var cached)) return cached;
        AudioStream? stream = null;
        try
        {
            if (track.SongId != null) stream = PluckRenderer.Render(songs[track.SongId]);
            else if (track.FilePath != null)
                stream = Path.GetExtension(track.FilePath).ToLowerInvariant() switch
                {
                    ".mp3" => AudioStreamMP3.LoadFromFile(track.FilePath),
                    ".ogg" => AudioStreamOggVorbis.LoadFromFile(track.FilePath),
                    ".wav" => AudioStreamWav.LoadFromFile(track.FilePath),
                    _ => null,
                };
        }
        catch (Exception e) { GD.PushWarning($"[DeskPet] 讀不到音樂 {track.Title}：{e.Message}"); }
        if (stream is AudioStreamMP3 mp3) mp3.Loop = false;
        if (stream is AudioStreamOggVorbis ogg) ogg.Loop = false;
        if (stream != null && track.Builtin) cache[track.Id] = stream;    // 內建曲合成要一點時間，留著重用
        return stream;
    }

    public override void _Process(double delta)
    {
        if (!player.Playing || Current == null) return;
        clock += delta;
        if (Current.Bpm is { } bpm)
        {
            // 知道 BPM：依播放位置算第幾拍
            var pos = player.GetPlaybackPosition() + AudioServer.GetTimeSinceLastMix();
            var index = (int)(pos * bpm / 60);
            if (index != lastBeatIndex) { lastBeatIndex = index; Beat?.Invoke(); }
        }
        else if (detector.Update((float)delta, clock)) Beat?.Invoke();
    }
}
