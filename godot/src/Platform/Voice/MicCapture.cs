// 麥克風擷取：AudioStreamMicrophone 播到一個靜音的匯流排，AudioEffectCapture 取樣本交給 VoiceAnalyzer
// 🔒 只在半身模式（或麥克風動嘴）開著時才開麥克風；樣本只在記憶體裡分析，不錄音、不存檔。
// 也是一個 IVoiceSource：主角色、半身角色依它動嘴。
using System;
using DeskPet.Core.Voice;
using DeskPet.Signals;
using Godot;

namespace DeskPet.Platform.Voice;

public sealed partial class MicCapture : Node, IVoiceSource
{
    const string BusName = "DeskPetMic";

    AudioStreamPlayer? player;
    AudioEffectCapture? capture;
    VoiceAnalyzer? analyzer;
    float[] mono = Array.Empty<float>();

    public string Id => "mic-voice";
    public bool SelfSpeaking => Running && Frame.Speaking;
    /// <summary>音量 0～1（-60 dB → 0、0 dB → 1）</summary>
    public float SelfLevel => Running ? Mathf.Clamp((float)(Frame.Db + 60) / 60, 0, 1) : -1;

    public bool Running => player != null;
    /// <summary>最新的分析結果（每 1/60 秒更新）</summary>
    public VoiceFrame Frame { get; private set; } = VoiceFrame.Silent;
    /// <summary>噪音底線（dB），校正後設定</summary>
    public double Floor
    {
        get => floor;
        set { floor = value; if (analyzer != null) analyzer.Floor = value; }
    }
    double floor = -50;

    /// <summary>可以選的麥克風（Godot 的輸入裝置名稱，"Default" = 系統預設）</summary>
    public static string[] Devices() => AudioServer.GetInputDeviceList();

    /// <summary>開麥克風；device = null 用系統預設</summary>
    public void Start(string? device = null)
    {
        Stop();
        if (device != null && Array.IndexOf(Devices(), device) >= 0) AudioServer.InputDevice = device;
        var bus = AudioServer.GetBusIndex(BusName);
        if (bus < 0)
        {
            AudioServer.AddBus();
            bus = AudioServer.BusCount - 1;
            AudioServer.SetBusName(bus, BusName);
            AudioServer.SetBusMute(bus, true);           // 不要把麥克風的聲音播出來
            AudioServer.AddBusEffect(bus, new AudioEffectCapture { BufferLength = 0.2f });
        }
        capture = (AudioEffectCapture)AudioServer.GetBusEffect(bus, 0);
        capture.ClearBuffer();
        analyzer = new VoiceAnalyzer(AudioServer.GetMixRate(), floor);
        player = new AudioStreamPlayer { Stream = new AudioStreamMicrophone(), Bus = BusName };
        AddChild(player);
        player.Play();
        GD.Print($"[mic] 開始：{AudioServer.InputDevice}，{AudioServer.GetMixRate()} Hz");
    }

    public void Stop()
    {
        if (player == null) return;
        player.Stop();
        player.QueueFree();
        player = null;
        analyzer = null;
        Frame = VoiceFrame.Silent;
        GD.Print("[mic] 關閉");
    }

    public override void _Process(double delta)
    {
        if (capture == null || analyzer == null || player == null) return;
        var n = capture.GetFramesAvailable();
        if (n <= 0) return;
        var stereo = capture.GetBuffer(n);
        if (mono.Length < stereo.Length) mono = new float[stereo.Length];
        for (var i = 0; i < stereo.Length; i++) mono[i] = (stereo[i].X + stereo[i].Y) * 0.5f;
        Frame = analyzer.Feed(mono.AsSpan(0, stereo.Length));
    }

    public override void _ExitTree() => Stop();
}
