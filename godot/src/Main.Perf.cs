// 效能量測：每幀處理時間（_Process，不含繪製）的平均與最大值，--dump 時每 2 秒印一次
// --perf=N：開 N 個客人角色，量 10 秒後印出結果並結束（tools/perf.ps1 用來比較升級前後）
using System.Diagnostics;
using System.Linq;
using Godot;

namespace DeskPet;

public partial class Main
{
    const float PerfSeconds = 10, PerfWarmup = 3;

    readonly Stopwatch frameClock = new();
    double frameSum, frameMax;
    int frameCount;
    int perfGuests = -1;
    double perfStart;

    void RecordFrameTime()
    {
        var ms = frameClock.Elapsed.TotalMilliseconds;
        frameSum += ms;
        frameMax = System.Math.Max(frameMax, ms);
        frameCount++;
        if (perfGuests >= 0) UpdatePerf();
    }

    string PerfLine()
    {
        var fps = Performance.GetMonitor(Performance.Monitor.TimeFps);
        return $"frame avg={frameSum / System.Math.Max(1, frameCount):0.000}ms max={frameMax:0.000}ms fps={fps:0} guests={guests.Count(g => !g.Leaving)}";
    }

    void DumpPerf()
    {
        GD.Print($"[perf] {PerfLine()}");
        ResetPerf();
    }

    void ResetPerf()
    {
        frameSum = frameMax = 0;
        frameCount = 0;
    }

    void SetupPerf()
    {
        if (!args.TryGetValue("perf", out var n)) return;
        perfGuests = int.Parse(n);
        maxGuestsOverride = System.Math.Max(perfGuests, 1);       // 不改設定（測試不能動到使用者的設定檔）
        for (var i = 0; i < perfGuests; i++) AddManualGuest(charIds[i % charIds.Count]);
        perfStart = Time.GetTicksMsec() / 1000.0;
    }

    // 前 3 秒（登場、貼圖產生）不算，之後量 10 秒
    void UpdatePerf()
    {
        var elapsed = Time.GetTicksMsec() / 1000.0 - perfStart;
        if (elapsed < PerfWarmup)
        {
            ResetPerf();
            return;
        }
        if (elapsed < PerfWarmup + PerfSeconds) return;
        GD.Print($"[perf] 客人 {perfGuests}：{PerfLine()}");
        perfGuests = -1;
        GetTree().Quit();
    }
}
