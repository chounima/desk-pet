// 進入點：建立桌面視窗、載入資料、建立寵物、全域鍵盤、系統匣，主迴圈
//
// 命令列參數（放在 -- 之後）：
//   --char=sera        起始角色
//   --action=typing    起始動作（並關閉自動行為），方便截圖檢查
//   --autostart=on|off 設定開機自動啟動
//   --say=greet        一開始就顯示某句台詞（截圖用）
//   --look=x,y         固定看某一點而不是游標（截圖用）
//   --test=face*4,Save 模擬點擊部位或特殊按鍵（測試互動規則）
//   --zones            印出身體各點判定成哪個部位
using System.Collections.Generic;
using System.Linq;
using DeskPet.Content;
using DeskPet.Game;
using DeskPet.Platform;
using DeskPet.Save;
using DeskPet.UI;
using DeskPet.View;
using Godot;

namespace DeskPet;

public partial class Main : Node2D
{
    sealed class Drag
    {
        public Vector2 Start;
        public string Zone = "body";     // 按下時點到的部位
        public bool Moved;
    }

    const float SaveEvery = 30;       // 統計每 30 秒存一次檔

    Pet pet = null!;
    Behavior behavior = null!;
    Fx fx = null!;
    Bubble bubble = null!;
    Lines lines = null!;
    Tray tray = null!;
    Settings settings = null!;
    Drag? drag;
    int keyCount;
    float saveTimer = SaveEvery;
    Vector2? lookOverride;     // --look=x,y：固定看某一點（截圖用）

    public override void _Ready()
    {
        if (!SingleInstance.TryAcquire())
        {
            GD.Print("[DeskPet] 已經有一隻桌寵在執行，結束這個實例");
            SetProcess(false);
            SetProcessInput(false);
            GetTree().Quit();
            return;
        }
        DesktopWindow.Setup();
        settings = Settings.Load();

        var (ids, rigs) = ContentLoader.LoadCharacters();
        var (_, clips) = ContentLoader.LoadAnimations();
        var props = ContentLoader.LoadProps();
        var args = ParseArgs();
        // 起始角色：命令列 > 上次選的 > 第一個
        var startId = args.GetValueOrDefault("char", rigs.ContainsKey(settings.Character) ? settings.Character : ids[0]);
        settings.Character = startId;
        dumpState = args.ContainsKey("dump");

        pet = new Pet(this, rigs[startId], clips, props, PetScale(settings.Size));
        var area = Area();
        pet.X = area.Size.X / 2;
        pet.Y = area.End.Y;
        behavior = new Behavior(pet, Area, new Interactions(InteractionData.Load())) { Auto = settings.Auto };
        fx = new Fx();
        AddChild(fx);
        lines = Lines.Load(ids);
        bubble = new Bubble();
        AddChild(bubble);
        behavior.Said += key => bubble.Say(lines.Pick(pet.Rig.Id, key));

        if (args.TryGetValue("action", out var action))
        {
            behavior.Auto = false;
            behavior.Force(action);
        }
        if (args.TryGetValue("say", out var sayKey)) bubble.Say(lines.Pick(startId, sayKey), 999);
        if (args.TryGetValue("look", out var look) && look.Split(',') is [var lx, var ly])
            lookOverride = new Vector2(float.Parse(lx), float.Parse(ly));
        if (args.TryGetValue("test", out var test))
            foreach (var step in ParseTestSteps(test)) testSteps.Enqueue(step);
        zoneProbe = args.ContainsKey("zones");

        if (args.TryGetValue("autostart", out var auto)) Autostart.Set(auto == "on");

        GlobalInput.Start();

        tray = new Tray();
        AddChild(tray);
        tray.Build(ids.Select(id => rigs[id]).ToList(), settings, Autostart.IsEnabled(), GD.Load<Texture2D>("res://icon.svg"));
        tray.CharacterChosen += id => { pet.SetCharacter(rigs[id]); settings.Character = id; settings.Save(); };
        tray.SizeChosen += size => { pet.SetScale(PetScale(size)); settings.Size = size; settings.Save(); };
        tray.AutoToggled += on => { behavior.Auto = on; settings.Auto = on; settings.Save(); };
        tray.AutostartToggled += Autostart.Set;
        tray.QuitRequested += () => GetTree().Quit();

        GD.Print($"[DeskPet] content={ContentLoader.Root} characters={string.Join(",", ids)} " +
                 $"clips={clips.Count} area={DesktopWindow.Area} uiScale={DesktopWindow.UiScale} start={startId} autostart={Autostart.IsEnabled()}");
    }

    static float PetScale(float size) => Tuning.Scale * DesktopWindow.UiScale * size;

    Rect2 Area() => new(Vector2.Zero, DesktopWindow.Area.Size);

    public override void _Process(double delta)
    {
        var dt = Mathf.Min((float)delta, 0.05f);

        var newKeys = 0;
        while (GlobalInput.TryDequeue(out var kind))
        {
            newKeys++;
            behavior.OnKey(kind);
        }
        RunTestScript();
        keyCount += newKeys;
        settings.AddKeys(newKeys);
        tray.SetTodayKeys(settings.TodayKeys);

        saveTimer -= dt;
        if (saveTimer <= 0)
        {
            saveTimer = SaveEvery;
            settings.SaveIfDirty();
        }

        UpdateDrag();
        pet.Gaze.Target = lookOverride ?? DesktopWindow.CursorInWindow();
        behavior.Update(dt);
        pet.Update(dt);
        fx.UpdateFx(dt, pet.Animator.Fx, pet.HeadTop());
        bubble.UpdateBubble(dt, pet.HeadTop());
        UpdateClickThrough();
        if (dumpState && Engine.GetProcessFrames() % 120 == 0) DumpState();
    }

    // ---------- 測試用 ----------

    // --test=face*4,Backspace*6,Save：開始後每 0.2 秒模擬一次點擊部位或特殊按鍵
    readonly Queue<string> testSteps = new();

    void RunTestScript()
    {
        var frame = Engine.GetProcessFrames();
        if (frame == 60 && zoneProbe) ProbeZones();
        if (testSteps.Count == 0 || frame < 120 || frame % 12 != 0) return;
        var step = testSteps.Dequeue();
        if (System.Enum.TryParse<GlobalInput.KeyKind>(step, out var kind)) behavior.OnKey(kind);
        else behavior.OnPetClick(step);
        GD.Print($"[test] {step} -> action={pet.Action} face={pet.FaceOverride ?? pet.Animator.Face}");
    }

    static IEnumerable<string> ParseTestSteps(string spec) =>
        spec.Split(',').SelectMany(s => s.Split('*') is [var name, var n] ? Enumerable.Repeat(name, int.Parse(n)) : new[] { s });

    // --zones：印出身體各點判定成哪個部位
    bool zoneProbe;

    void ProbeZones()
    {
        var top = pet.Rig.HeadTop;
        var points = new Dictionary<string, Vector2>
        {
            ["頭頂"] = pet.BoneToScreen("head", new Vector2(top[0], top[1] * 0.8f)),
            ["臉"] = pet.BoneToScreen("head", new Vector2(top[0], top[1] * 0.3f)),
            ["胸口"] = pet.BoneToScreen("torso", new Vector2(0, -30 * pet.Rig.RootScale)),
            ["手"] = pet.BoneToScreen("handNear", new Vector2(0, 8)),
            ["腳"] = pet.BoneToScreen("footNear", new Vector2(4, 2)),
            ["尾巴"] = pet.BoneToScreen("tail", new Vector2(-30, 60)),
        };
        foreach (var (name, p) in points)
            GD.Print($"[zone] {name} -> {pet.View.HitSlot(p) ?? "(沒點到)"} / {ZoneAt(p) ?? "-"}");
    }

    // --dump：每 2 秒印出狀態，除錯用
    bool dumpState;

    void DumpState() =>
        GD.Print($"[state] keys={keyCount} input={GlobalInput.Running} action={pet.Action} " +
                 $"t={pet.Animator.LocalTime:0.00} facing={pet.Facing} x={pet.X:0} char={pet.Rig.Id} scale={pet.Scale:0.00} today={settings.TodayKeys}");

    // ---------- 滑鼠：點擊與拖曳 ----------

    public override void _Input(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb && ZoneAt(mb.Position) is { } zone)
            drag = new Drag { Start = mb.Position, Zone = zone };
    }

    /// <summary>畫面上某點點到角色的哪個部位；沒點到角色回傳 null</summary>
    string? ZoneAt(Vector2 point)
    {
        if (pet.View.HitSlot(point) is not { } slot) return null;
        return Interactions.ZoneOf(slot, pet.ToBoneLocal("head", point).Y, pet.Rig.HeadTop[1]);
    }

    // 拖曳中游標可能離開可點擊區域，所以改用全域查詢游標位置與按鍵狀態
    void UpdateDrag()
    {
        if (drag == null) return;
        var cursor = DesktopWindow.CursorInWindow();
        if (!DesktopWindow.LeftButtonDown())
        {
            if (drag.Moved) behavior.Release();
            else behavior.OnPetClick(drag.Zone);
            drag = null;
            return;
        }
        if (!drag.Moved && cursor.DistanceTo(drag.Start) > 5 * DesktopWindow.UiScale)
        {
            drag.Moved = true;
            behavior.Grab(cursor);
        }
        if (drag.Moved) behavior.MoveHeld(cursor);
    }

    // 游標在角色不透明的像素上（或正在拖曳）才接收滑鼠，其他時候點擊穿透到桌面
    void UpdateClickThrough()
    {
        var over = drag != null || pet.View.HitTest(DesktopWindow.CursorInWindow());
        DesktopWindow.SetClickThrough(!over);
    }

    public override void _ExitTree()
    {
        if (settings == null) return;    // 重複啟動、還沒初始化就結束的實例
        GlobalInput.Stop();
        settings.Save();
        SingleInstance.Release();
    }

    static Dictionary<string, string> ParseArgs()
    {
        var result = new Dictionary<string, string>();
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            var kv = arg.TrimStart('-').Split('=', 2);
            result[kv[0]] = kv.Length > 1 ? kv[1] : "true";
        }
        return result;
    }
}
