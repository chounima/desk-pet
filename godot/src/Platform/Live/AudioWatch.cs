// 用 Windows 音量表偵測「有人在講話」：任何會議軟體都能用（Teams、Zoom、LINE…），不需要它們的 API
//
// - 麥克風音量 → 使用者自己在講話（主角色動嘴）
// - 會議軟體播出的聲音 → 對方在講話（每個軟體一個客人，例如「Zoom 通話」）
// 🔒 只讀音量大小（0～1 的峰值），不錄音、不讀聲音內容。分不出是誰在講話。
//
// Godot 的可卸載組件環境不能用 [ComImport] 的 COM interop，所以直接用函式指標呼叫 COM 介面的 vtable。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using DeskPet.Content;
using DeskPet.Signals;

namespace DeskPet.Platform.Live;

public sealed unsafe class AudioWatch : PresenceSource, IVoiceSource
{
    const string Mic = "@mic";
    volatile bool selfSpeaking;
    volatile float selfLevel = -1;

    /// <summary>麥克風有聲音（使用者自己在講話）</summary>
    public bool SelfSpeaking => selfSpeaking;
    /// <summary>麥克風音量峰值 0～1；沒開麥克風偵測是 -1</summary>
    public float SelfLevel => selfLevel;

    const float Hold = 0.4f;             // 音量降下來後再等多久才算講完（秒）

    readonly bool mic, apps;
    readonly float micThreshold, appThreshold;
    readonly Dictionary<string, string> meetingApps;      // { 程式名稱（小寫，不含 .exe）: 顯示名稱 }
    Thread? thread;
    volatile bool running;

    /// <param name="meetingApps">要偵測的會議軟體 { 程式名稱: 顯示名稱 }</param>
    public AudioWatch(bool mic, bool apps, Dictionary<string, string> meetingApps, float micThreshold, float appThreshold)
    {
        this.mic = mic;
        this.apps = apps;
        this.meetingApps = meetingApps.ToDictionary(kv => kv.Key.ToLowerInvariant(), kv => kv.Value);
        this.micThreshold = micThreshold;
        this.appThreshold = appThreshold;
    }

    public override string Id => "audio";

    public override void Start()
    {
        if (!OperatingSystem.IsWindows()) return;
        running = true;
        thread = new Thread(Run) { IsBackground = true, Name = "AudioWatch" };
        thread.Start();
    }

    public override void Stop() => running = false;

    // ---------- 偵測迴圈（每秒 20 次讀音量，每秒重新整理一次裝置和工作階段） ----------

    sealed class Talk
    {
        public bool On;
        public float Quiet;     // 已經安靜多久
    }

    void Run()
    {
        CoInitializeEx(IntPtr.Zero, 0);     // COINIT_MULTITHREADED
        IntPtr enumerator = IntPtr.Zero, micMeter = IntPtr.Zero;
        var sessions = new List<(string App, IntPtr Meter)>();
        var talks = new Dictionary<string, Talk>();
        var present = new HashSet<string>();
        var names = new Dictionary<uint, string>();
        var refresh = 0f;
        try
        {
            var clsid = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
            var iid = new Guid("A95664D2-9614-4F35-A746-DE8DB63617E6");
            Check(CoCreateInstance(&clsid, IntPtr.Zero, ClsctxAll, &iid, &enumerator), "MMDeviceEnumerator");
            Status = I18n.T("live.audioOn");
            while (running)
            {
                if ((refresh -= 0.05f) <= 0)
                {
                    refresh = 1;
                    if (mic)
                    {
                        Release(ref micMeter);
                        micMeter = MicMeter(enumerator);
                    }
                    if (apps)
                    {
                        foreach (var s in sessions) Release(s.Meter);
                        sessions = AppSessions(enumerator, names);
                        // 會議軟體關掉了（沒有聲音工作階段）→ 客人離開
                        var alive = sessions.Select(s => s.App).ToHashSet();
                        foreach (var app in present.Where(a => !alive.Contains(a)).ToList())
                        {
                            present.Remove(app);
                            talks.Remove(app);
                            Emit(new PresenceEvent(Id, PresenceKind.Leave, app));
                        }
                    }
                }
                if (micMeter != IntPtr.Zero)
                {
                    var level = Peak(micMeter);
                    selfLevel = level;
                    Update(talks, Mic, level, micThreshold, null);
                }
                foreach (var group in sessions.GroupBy(s => s.App))
                {
                    var peak = group.Max(s => Peak(s.Meter));
                    Update(talks, group.Key, peak, appThreshold, present);
                }
                Thread.Sleep(50);
            }
        }
        catch (Exception e)
        {
            Status = I18n.T("live.error", e.Message);
        }
        finally
        {
            foreach (var s in sessions) Release(s.Meter);
            Release(ref micMeter);
            Release(ref enumerator);
            foreach (var app in present) Emit(new PresenceEvent(Id, PresenceKind.Leave, app));
            selfSpeaking = false;
            selfLevel = -1;
            CoUninitialize();
        }
    }

    // 音量超過門檻就算在講話；降下來後等 Hold 秒才算講完（講話中的短暫停頓不會一直閃）
    void Update(Dictionary<string, Talk> talks, string who, float peak, float threshold, HashSet<string>? present)
    {
        if (!talks.TryGetValue(who, out var t)) talks[who] = t = new Talk();
        if (peak >= threshold)
        {
            t.Quiet = 0;
            if (t.On) return;
            t.On = true;
            if (who == Mic)
            {
                selfSpeaking = true;
                return;
            }
            if (present != null && present.Add(who))
                Emit(new PresenceEvent(Id, PresenceKind.Join, who, I18n.T("live.meetingGuest", meetingApps.GetValueOrDefault(who, who))));
            Emit(new PresenceEvent(Id, PresenceKind.Speaking, who));
        }
        else if (t.On && (t.Quiet += 0.05f) >= Hold)
        {
            t.On = false;
            if (who == Mic) selfSpeaking = false;
            else Emit(new PresenceEvent(Id, PresenceKind.Silent, who));
        }
    }

    // 預設的通訊用麥克風（會議軟體通常用這個）的音量表
    static IntPtr MicMeter(IntPtr enumerator)
    {
        IntPtr device;
        // IMMDeviceEnumerator::GetDefaultAudioEndpoint(eCapture, eCommunications)
        if (((delegate* unmanaged[Stdcall]<IntPtr, int, int, IntPtr*, int>)Vtbl(enumerator, 4))(enumerator, 1, 2, &device) < 0) return IntPtr.Zero;
        var meter = Activate(device, MeterIid);
        Release(device);
        return meter;
    }

    // 所有播放裝置上，屬於會議軟體的聲音工作階段的音量表
    List<(string, IntPtr)> AppSessions(IntPtr enumerator, Dictionary<uint, string> names)
    {
        var result = new List<(string, IntPtr)>();
        IntPtr collection;
        // IMMDeviceEnumerator::EnumAudioEndpoints(eRender, DEVICE_STATE_ACTIVE)
        if (((delegate* unmanaged[Stdcall]<IntPtr, int, int, IntPtr*, int>)Vtbl(enumerator, 3))(enumerator, 0, 1, &collection) < 0) return result;
        uint count;
        ((delegate* unmanaged[Stdcall]<IntPtr, uint*, int>)Vtbl(collection, 3))(collection, &count);
        for (uint i = 0; i < count; i++)
        {
            IntPtr device;
            if (((delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int>)Vtbl(collection, 4))(collection, i, &device) < 0) continue;
            var manager = Activate(device, SessionManagerIid);
            Release(device);
            if (manager == IntPtr.Zero) continue;
            IntPtr sessionEnum;
            // IAudioSessionManager2::GetSessionEnumerator
            if (((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)Vtbl(manager, 5))(manager, &sessionEnum) >= 0)
            {
                int n;
                ((delegate* unmanaged[Stdcall]<IntPtr, int*, int>)Vtbl(sessionEnum, 3))(sessionEnum, &n);
                for (var k = 0; k < n; k++)
                {
                    IntPtr control;
                    if (((delegate* unmanaged[Stdcall]<IntPtr, int, IntPtr*, int>)Vtbl(sessionEnum, 4))(sessionEnum, k, &control) < 0) continue;
                    var app = AppOf(control, names);
                    if (app != null && QueryInterface(control, MeterIid) is var meter && meter != IntPtr.Zero) result.Add((app, meter));
                    Release(control);
                }
                Release(sessionEnum);
            }
            Release(manager);
        }
        Release(collection);
        return result;
    }

    // 工作階段屬於哪個會議軟體（不是會議軟體回傳 null）
    string? AppOf(IntPtr control, Dictionary<uint, string> names)
    {
        var control2 = QueryInterface(control, SessionControl2Iid);
        if (control2 == IntPtr.Zero) return null;
        uint pid;
        // IAudioSessionControl2::GetProcessId
        var ok = ((delegate* unmanaged[Stdcall]<IntPtr, uint*, int>)Vtbl(control2, 14))(control2, &pid) >= 0;
        Release(control2);
        if (!ok || pid == 0) return null;
        if (!names.TryGetValue(pid, out var name))
        {
            try { name = Process.GetProcessById((int)pid).ProcessName.ToLowerInvariant(); }
            catch { name = ""; }
            names[pid] = name;
        }
        return meetingApps.ContainsKey(name) ? name : null;
    }

    // ---------- COM：直接呼叫 vtable ----------

    static readonly Guid MeterIid = new("C02216F6-8C67-4B5B-9D00-D008E73E0064");           // IAudioMeterInformation
    static readonly Guid SessionManagerIid = new("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");  // IAudioSessionManager2
    static readonly Guid SessionControl2Iid = new("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"); // IAudioSessionControl2
    const uint ClsctxAll = 0x17;

    static void* Vtbl(IntPtr obj, int slot) => (*(void***)obj)[slot];

    static float Peak(IntPtr meter)
    {
        float peak;
        // IAudioMeterInformation::GetPeakValue
        return ((delegate* unmanaged[Stdcall]<IntPtr, float*, int>)Vtbl(meter, 3))(meter, &peak) >= 0 ? peak : 0;
    }

    // IMMDevice::Activate
    static IntPtr Activate(IntPtr device, Guid iid)
    {
        IntPtr result;
        return ((delegate* unmanaged[Stdcall]<IntPtr, Guid*, uint, IntPtr, IntPtr*, int>)Vtbl(device, 3))(device, &iid, ClsctxAll, IntPtr.Zero, &result) >= 0 ? result : IntPtr.Zero;
    }

    static IntPtr QueryInterface(IntPtr obj, Guid iid)
    {
        IntPtr result;
        return ((delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int>)Vtbl(obj, 0))(obj, &iid, &result) >= 0 ? result : IntPtr.Zero;
    }

    static void Release(IntPtr obj)
    {
        if (obj != IntPtr.Zero) ((delegate* unmanaged[Stdcall]<IntPtr, uint>)Vtbl(obj, 2))(obj);
    }

    static void Release(ref IntPtr obj)
    {
        Release(obj);
        obj = IntPtr.Zero;
    }

    static void Check(int hr, string what)
    {
        if (hr < 0) throw new InvalidOperationException($"{what} 0x{hr:X8}");
    }

    [DllImport("ole32.dll")] static extern int CoInitializeEx(IntPtr reserved, uint coInit);
    [DllImport("ole32.dll")] static extern void CoUninitialize();
    [DllImport("ole32.dll")] static extern int CoCreateInstance(Guid* clsid, IntPtr outer, uint context, Guid* iid, IntPtr* result);
}
