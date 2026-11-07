// --record=資料夾：把某個角色的某個動作錄成一張張 PNG（升級前後比較用，見 tools/record_clip.ps1）
//
// 搭配 --char、--form、--action；--record-frames=12。動作從 0 秒開始，每一步固定前進 1/60 秒（不看實際時間），
// 等距存 N 張，裁切角色附近的範圍。之後有物理時，甩動也會被錄下來（網頁版 preview/record.html 用同樣的方式模擬）。
using System.Collections.Generic;
using System.IO;
using Godot;

namespace DeskPet;

public partial class Main
{
    const float RecordStep = 1 / 60f;
    const int RecordWarmup = 30;      // 開頭等幾幀，讓視窗與貼圖準備好

    string? recordDir;
    int recordFrames = 12, recordStepsPer = 1, recordCaptured, recordWait;
    bool recordPending;

    void SetupRecord()
    {
        if (!args.TryGetValue("record", out var dir)) return;
        recordDir = ProjectSettings.GlobalizePath(dir);
        Directory.CreateDirectory(recordDir);
        if (args.TryGetValue("record-frames", out var n)) recordFrames = Mathf.Max(2, int.Parse(n));
        var action = args.GetValueOrDefault("action", "idle");
        var area = Area();
        pet.Facing = 1;
        SetDriver(new Game.Drivers.StillDriver(pet, area.GetCenter().X, area.End.Y, action) { HoldLastFrame = true });   // 原地不動，只做這個動作
        pet.Animator.Seek(0);
        // N 張平均分布在動作長度內（循環動作的最後一張不會和第一張重複）
        var duration = pet.Animator.Clip.Duration;
        recordStepsPer = Mathf.Max(1, Mathf.RoundToInt(duration / RecordStep / (pet.Animator.Clip.Loop ? recordFrames : recordFrames - 1)));
        recordWait = RecordWarmup;
        GD.Print($"[record] {pet.Rig.Id} {action} {duration:0.00}s → {recordFrames} 張，每 {recordStepsPer} 步一張 → {recordDir}");
    }

    /// <summary>錄影模式時取代一般的每幀更新；回傳 true 表示這一幀已處理</summary>
    bool UpdateRecord()
    {
        if (recordDir == null) return false;
        if (recordWait > 0)
        {
            recordWait--;
            pet.Update(0);
            return true;
        }
        // 上一幀畫好的畫面：存下來
        if (recordPending)
        {
            SaveRecordFrame(recordCaptured++);
            recordPending = false;
            if (recordCaptured >= recordFrames)
            {
                GD.Print($"[record] 完成 {recordCaptured} 張");
                GetTree().Quit();
                return true;
            }
            for (var i = 0; i < recordStepsPer; i++) StepRecord();
        }
        else pet.Update(0);
        recordPending = true;
        return true;
    }

    void StepRecord()
    {
        driver.Update(RecordStep);
        pet.Update(RecordStep);
    }

    void SaveRecordFrame(int index)
    {
        var image = GetViewport().GetTexture().GetImage();
        var h = pet.Rig.Height * pet.Scale;
        var rect = new Rect2I((int)(pet.X - h * 0.9f), (int)(pet.Y - h * 1.2f), (int)(h * 1.8f), (int)(h * 1.25f))
            .Intersection(new Rect2I(Vector2I.Zero, image.GetSize()));
        image.GetRegion(rect).SavePng(Path.Combine(recordDir!, $"frame_{index:00}.png"));
    }
}
