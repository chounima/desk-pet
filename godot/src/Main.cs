// 進入點：建立桌面視窗、載入資料、建立寵物、全域鍵盤、系統匣，主迴圈
//
// 命令列參數（放在 -- 之後）：
//   --char=sera        起始角色
//   --action=typing    起始動作（並關閉自動行為），方便截圖檢查
//   --autostart=on|off 設定開機自動啟動
//   --say=greet        一開始就顯示某句台詞（截圖用）
//   --look=x,y         固定看某一點而不是游標（截圖用）
//   --test=face*4,Save 模擬點擊部位或特殊按鍵（測試互動規則）
//                      do:climb 強制做某件事、wait 空一拍、menu 叫出右鍵選單、hide:3 隱藏 3 秒、remind 久坐提醒
//   --zones            印出身體各點判定成哪個部位
//   --volley / --volley-bot  直接開始排球對打（bot = 模擬完美玩家自動回球）
using System.Collections.Generic;
using System.Linq;
using DeskPet.Audio;
using DeskPet.Content;
using DeskPet.Game;
using DeskPet.Game.Minigames;
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

    Dictionary<string, RigData> rigs = null!;
    Dictionary<string, string> args = null!;
    string startId = "";

    Pet pet = null!;
    Behavior behavior = null!;
    Fx fx = null!;
    Bubble bubble = null!;
    VolleySession volley = null!;
    Sfx sfx = null!;
    Lines lines = null!;
    Tray tray = null!;
    Settings settings = null!;
    Drag? drag;
    int keyCount;
    float saveTimer = SaveEvery;
    Vector2? lookOverride;     // --look=x,y：固定看某一點（截圖用）
    bool duplicate;            // 重複啟動的實例：什麼都沒初始化，等待結束

    public override void _Ready()
    {
        // --selftest=progress：只跑收集系統的規則測試就結束（不需要單一實例、不開畫面）
        if (OS.GetCmdlineUserArgs().Contains("--selftest=progress"))
        {
            duplicate = true;
            var code = Game.Progression.ProgressSelfTest.Run();
            GetTree().Quit(code);
            return;
        }
        if (!SingleInstance.TryAcquire())
        {
            GD.Print("[DeskPet] 已經有一隻桌寵在執行，結束這個實例");
            // SetProcess(false) 擋不住 Godot 在 _Ready 之後自動開啟的 _Process，所以另外用旗標擋
            duplicate = true;
            SetProcess(false);
            SetProcessInput(false);
            GetTree().Quit();
            return;
        }
        DesktopWindow.Setup();
        settings = Settings.Load();

        daily = DailyConfig.Load();
        var (ids, loadedRigs) = ContentLoader.LoadCharacters();
        rigs = loadedRigs;
        var bodies = rigs.Values.SelectMany(r => (r.Forms?.Values.Select(f => f.Data) ?? new[] { r }).Append(r)).Select(r => r.Body);
        var (_, clips) = ContentLoader.LoadAnimations(bodies);
        var props = ContentLoader.LoadProps();
        args = ParseArgs();
        // 起始角色：命令列 > 上次選的 > 第一個
        startId = args.GetValueOrDefault("char", rigs.ContainsKey(settings.Character) ? settings.Character : ids[0]);
        // 命令列指定的角色（測試用）不寫回設定，才不會蓋掉使用者在系統匣選的角色
        if (!args.ContainsKey("char")) settings.Character = startId;
        dumpState = args.ContainsKey("dump");

        pet = new Pet(this, RigOf(startId), clips, props, PetScale(settings.Size));
        var area = Area();
        pet.X = area.Size.X / 2;
        pet.Y = area.End.Y;
        RefreshWindows();
        behavior = new Behavior(pet, Area, new Interactions(InteractionData.Load()),
                                new Surfaces(Area, () => WindowTracker.Platforms)) { Auto = settings.Auto };
        fx = new Fx();
        AddChild(fx);
        lines = Lines.Load(ids);
        bubble = new Bubble();
        AddChild(bubble);
        sfx = new Sfx { Enabled = settings.Sound };
        AddChild(sfx);
        behavior.Said += key =>
        {
            bubble.Say(lines.Pick(pet.Rig.Id, key));
            sfx.PlayForLine(key);
        };

        VolleySkills.Load();
        volley = new VolleySession { Bot = args.ContainsKey("volley-bot") };
        AddChild(volley);
        volley.Init(pet, behavior, Area, props["volleyball"], bubble, fx, sfx, this);

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
        tray.SetForms(rigs[startId].Forms, FormOf(startId));
        tray.CharacterChosen += id =>
        {
            pet.SetCharacter(RigOf(id));
            tray.SetForms(rigs[id].Forms, FormOf(id));
            if (progress != null) RefreshLocks();
            settings.Character = id;
            settings.Save();
        };
        tray.FormChosen += ChooseForm;
        tray.SizeChosen += size => { pet.SetScale(PetScale(size)); settings.Size = size; settings.Save(); };
        tray.AutoToggled += on => { behavior.Auto = on; settings.Auto = on; settings.Save(); };
        tray.AutostartToggled += Autostart.Set;
        tray.VolleyToggled += volley.SetActive;
        tray.SoundToggled += on => { sfx.Enabled = on; settings.Sound = on; settings.Save(); };
        if (args.ContainsKey("volley") || volley.Bot)
        {
            volley.SetActive(true);
            tray.SetVolley(true);
        }
        tray.QuitRequested += () => GetTree().Quit();
        SetupProgress(ids, props);
        SetupDaily();
        SetupMusic();
        if (args.TryGetValue("hub", out var hubTab)) hub.Open(hubTab);     // --hub=shop：一開始就打開視窗（截圖用）

        GD.Print($"[DeskPet] content={ContentLoader.Root} characters={string.Join(",", ids)} " +
                 $"clips={clips["humanoid"].Count} bodies={string.Join(",", clips.Keys)} form={FormOf(startId)} area={DesktopWindow.Area} uiScale={DesktopWindow.UiScale} start={startId} autostart={Autostart.IsEnabled()}");
    }

    // 型態：命令列（測試用，不存檔）> 這個角色上次選的
    string? FormOf(string id) =>
        id == startId && args.TryGetValue("form", out var f) ? f
        : settings.Forms.TryGetValue(id, out var saved) ? saved : ContentLoader.DefaultForm(rigs[id]);

    RigData RigOf(string id) => ContentLoader.FormRig(rigs[id], FormOf(id));

    void ChooseForm(string form)
    {
        var id = pet.Rig.Id;
        settings.Forms[id] = form;
        settings.Save();
        args.Remove("form");
        pet.SetCharacter(RigOf(id));
        tray.SetForms(rigs[id].Forms, form);
        if (progress != null) RefreshLocks();
        fx.Burst("💨", pet.HeadTop(), Colors.White);
    }

    // 重新掃描桌面上的視窗（角色可以站的平台）
    const float WindowRefresh = 0.25f;
    float windowTimer;

    static void RefreshWindows()
    {
        var u = DesktopWindow.UiScale;
        WindowTracker.Refresh(DesktopWindow.Hwnd, DesktopWindow.Area, minWidth: 160 * u, minTop: 60 * u);
    }

    Rect2 Area() => new(Vector2.Zero, DesktopWindow.Area.Size);

    public override void _Process(double delta)
    {
        if (duplicate) return;
        var dt = Mathf.Min((float)delta, 0.05f);

        var newKeys = 0;
        while (GlobalInput.TryDequeue(out var kind))
        {
            newKeys++;
            behavior.OnKey(kind);
        }
        RunTestScript();
        keyCount += newKeys;
        if (newKeys > 0)
        {
            dailyLife.OnActivity();
            progress.OnKeys(newKeys);
        }
        settings.AddKeys(newKeys);
        tray.SetTodayKeys(settings.TodayKeys);

        saveTimer -= dt;
        if (saveTimer <= 0)
        {
            saveTimer = SaveEvery;
            settings.SaveIfDirty();
        }

        windowTimer -= dt;
        if (windowTimer <= 0)
        {
            windowTimer = WindowRefresh;
            RefreshWindows();
        }

        UpdateDaily(dt);
        UpdateProgress(dt);
        UpdateMusic(dt);
        UpdateDrag();
        pet.Gaze.Target = lookOverride ?? DesktopWindow.CursorInWindow();
        behavior.Update(dt);
        volley.Tick(dt, DesktopWindow.CursorInWindow());
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
        if (step.StartsWith("do:")) behavior.Force(step[3..]);          // do:climb、do:hopDown…
        else if (step == "menu") OpenPetMenu();                          // 叫出右鍵選單（不用真的點）
        else if (step.StartsWith("hide:")) presence.HideFor(float.Parse(step[5..]) / 60f);   // hide:3 = 隱藏 3 秒
        else if (step == "remind") behavior.Remind("stretchRemind", "stretch");
        else if (step == "guitar") StartPerformance(force: true);                   // 開始彈吉他（不檢查有沒有買，測試用）
        else if (step == "wait") { }                                     // 空一拍
        else if (System.Enum.TryParse<GlobalInput.KeyKind>(step, out var kind)) behavior.OnKey(kind);
        else behavior.OnPetClick(step);
        GD.Print($"[test] {step} -> action={pet.Action} face={pet.FaceOverride ?? pet.Animator.Face} surface={behavior.SurfaceInfo}");
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
                 $"t={pet.Animator.LocalTime:0.00} facing={pet.Facing} x={pet.X:0} char={pet.Rig.Id} scale={pet.Scale:0.00} today={settings.TodayKeys} y={pet.Y:0} surface={behavior.SurfaceInfo} windows=[{WindowTracker.Stats}]" + (volley.Active ? $" volley[{volley.DebugInfo}]" : "") + $" hidden={presence.Hidden}" + (performing ? $" music[{music.Current?.Title} bpm={music.Bpm:0} speed={pet.Animator.Speed:0.00}]" : "") + $" sfx[{string.Join(",", sfx.Played.Select(kv => $"{kv.Key}={kv.Value}"))}]");

    // ---------- 滑鼠：點擊與拖曳 ----------

    public override void _Input(InputEvent e)
    {
        if (duplicate) return;
        // 對打中：點球回擊，角色不能點也不能拖
        if (volley.Active)
        {
            if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click && volley.HitBall(click.Position))
                volley.ClickBall();
            return;
        }
        // 彈吉他中：點角色只冒愛心（不能拖），右鍵選單照常
        if (performing && e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } pc && ZoneAt(pc.Position) != null)
        {
            fx.Heart(pet.HeadTop());
            progress.OnInteraction(pet.Rig.Id);
            return;
        }
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb && ZoneAt(mb.Position) is { } zone)
        {
            drag = new Drag { Start = mb.Position, Zone = zone };
            dailyLife.OnActivity();
        }
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } rb && ZoneAt(rb.Position) != null)
            OpenPetMenu();
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
            else
            {
                behavior.OnPetClick(drag.Zone);
                progress.OnInteraction(pet.Rig.Id);
                fx.Heart(pet.HeadTop());
            }
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
        if (presence.Hidden) return;       // 隱藏中一律穿透（OnPresenceChanged 已設好）
        var cursor = DesktopWindow.CursorInWindow();
        var over = drag != null || (volley.Active ? volley.HitBall(cursor) : pet.View.HitTest(cursor));
        DesktopWindow.SetClickThrough(!over);
    }

    public override void _ExitTree()
    {
        if (settings == null) return;    // 重複啟動、還沒初始化就結束的實例
        GlobalInput.Stop();
        settings.Save();
        progressState?.Save();
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
