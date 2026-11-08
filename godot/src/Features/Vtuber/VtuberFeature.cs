// 半身 VTuber 模式：在桌面透明視窗的角落放大顯示一個角色的上半身，跟著使用者的聲音動嘴、換表情、做動作，眼睛跟著游標。
// 直播時擷取整個桌面就會一起播出去（不另開視窗）。
//
// - 角色：獨立的 Pet（原地不動的 StillDriver、自己的 ControlArbiter），桌面上的全身寵物照常活動（可以設定藏起來）
// - 聲音：MicCapture → VoiceAnalyzer → VoiceCalibration（自動校正）→ VoiceRules（data/vtuber.json）
// - 快捷鍵：Ctrl+Alt+數字（data/vtuber.json 的 hotkeys），優先於聲音規則
// - 拖曳換位置、滾輪縮放、右鍵開設定分頁
// 測試：--vtuber 直接開；--vtuber-char=tk01 角色；--vtuber-yaw=0 角度；--vtuber-hide-pet 藏桌面角色；--vtuber-shot-full 截整個畫面；--vtuber-voice=quiet:2,talk:3,loud:0.2,high:1 用假的聲音（不開麥克風）；--vtuber-shot=檔名.png 幾秒後截圖並結束
using System.Collections.Generic;
using System.Linq;
using DeskPet.Content;
using DeskPet.Core;
using DeskPet.Core.Voice;
using DeskPet.Game;
using DeskPet.Game.Control;
using DeskPet.Game.Drivers;
using DeskPet.Game.Vtuber;
using DeskPet.Platform;
using DeskPet.Platform.Voice;
using DeskPet.UI.Hub;
using DeskPet.View;
using Godot;

namespace DeskPet.Features.Vtuber;

public sealed class VtuberFeature : IFeature
{
    FeatureHost host = null!;
    VtuberConfig cfg = null!;
    Save.VtuberSettings Settings => host.Settings.Vtuber;

    Control stage = null!;          // 裁切範圍：只顯示上半身
    Pet? pet;
    StillDriver? driver;
    ControlArbiter? control;
    PetAttachments? attach;
    MicCapture mic = null!;
    readonly VoiceCalibration cal = new();
    VoiceRules rules = null!;
    Hotkeys hotkeys = null!;
    VoiceScript? script;
    VtuberTab tab = null!;

    Vector2? dragOffset;
    Vector2 dragStart;
    bool dragMoved, hidden;
    RigData baseRig = null!;        // 半身角色（側面的 rig.json；其他視角在 baseRig.Views）
    float yaw, viewYaw = float.NaN;     // 這一刻的面向角度、目前用的視角的角度
    int facing = 1;
    string? shotPath;
    float shotLeft;

    public string Id => "vtuber";
    public bool Active => pet != null;
    /// <summary>最新的聲音分析結果（Hub 分頁顯示）</summary>
    public VoiceFrame Frame { get; private set; } = VoiceFrame.Silent;
    public VoiceCalibration Calibration => cal;
    public string? LastRule => rules.LastFired;
    public string MicStatus => script != null ? "script" : mic.Running ? AudioServer.InputDevice : "off";

    static double Now() => Time.GetTicksMsec() / 1000.0;

    public void Init(FeatureHost host)
    {
        this.host = host;
        cfg = VtuberConfig.Load();
        rules = new VoiceRules(cfg);
        hotkeys = new Hotkeys(cfg.Hotkeys.Keys);
        cal.Reset(Settings.NoiseFloor);

        mic = new MicCapture();
        host.Root.AddChild(mic);
        // 畫在桌面角色、特效上面（ZIndex）
        stage = new Control { ClipContents = true, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false, ZIndex = 20 };
        host.Root.AddChild(stage);

        tab = new VtuberTab(host.Settings, host.CharacterIds.Select(id => host.Rigs[id]).ToList(), host.Progress.CharacterUnlocked, this);
        host.Hub.AddTab("vtuber", tab, tab.Refresh, _ => tab.Tick());
        host.Tray.AddFeatureItem(I18n.T("menu.vtuber"), () => host.Hub.Open("vtuber"));

        if (host.Args.TryGetValue("vtuber-voice", out var voice)) script = new VoiceScript(voice);
        if (host.Args.TryGetValue("vtuber-shot", out var shot))
        {
            shotPath = ProjectSettings.GlobalizePath(shot);
            shotLeft = float.Parse(host.Args.GetValueOrDefault("vtuber-shot-at", "3"), System.Globalization.CultureInfo.InvariantCulture);
        }
        if (host.Args.TryGetValue("vtuber-hide-pet", out var hideArg)) Settings.HideDesktopPet = hideArg != "0";      // 測試用（--sandbox 不存設定）；=0 不藏
        if (host.Args.TryGetValue("vtuber-char", out var charArg)) Settings.Character = charArg;
        if (host.Args.TryGetValue("vtuber-yaw", out var yawArg)) Settings.ViewYaw = float.Parse(yawArg, System.Globalization.CultureInfo.InvariantCulture);
        // --sandbox（測試）不依設定自動開，只認 --vtuber
        if (host.Args.ContainsKey("vtuber") || (Settings.Enabled && !host.Sandbox)) Start();
    }

    // ---------- 開關 ----------

    public void SetEnabled(bool on)
    {
        Settings.Enabled = on;
        if (!host.Sandbox) host.Settings.Save();
        if (on) Start(); else Stop();
    }

    void Start()
    {
        Stop();
        var id = host.Rigs.ContainsKey(Settings.Character) ? Settings.Character : host.Pet.Rig.Id;
        baseRig = host.RigOf(id);
        yaw = Settings.ViewYaw;
        var view = ContentLoader.ViewFor(baseRig, yaw);
        (facing, viewYaw) = (view.facing, view.viewYaw);
        pet = new Pet(stage, view.rig, host.Clips, host.Props, 1);
        control = new ControlArbiter(a => driver?.TryStart(a) == true, Now);
        attach = new PetAttachments(stage, pet);
        pet.Gaze.Range = 100000;        // 螢幕上哪裡都看得到
        Layout();
        if (script == null) mic.Start(string.IsNullOrEmpty(Settings.MicDevice) ? null : Settings.MicDevice);
        mic.Floor = cal.Floor;
        host.Voice.Add(mic);            // 桌面上的主角色也跟著動嘴
        ApplyVisibility();
        GD.Print($"[vtuber] 開始：{id}，{stage.Size} at {stage.Position}");
    }

    void Stop()
    {
        if (pet == null) return;
        host.Voice.Remove(mic);
        mic.Stop();
        attach?.Free();
        pet.View.QueueFree();
        pet = null;
        driver = null;
        control = null;
        attach = null;
        stage.Visible = false;
        host.HidePet(Id, false);
        Settings.NoiseFloor = cal.Floor;
    }

    /// <summary>換半身角色（空的 = 和主角色一樣）</summary>
    public void SetCharacter(string id)
    {
        Settings.Character = id;
        if (!host.Sandbox) host.Settings.Save();
        if (Active) Start();
    }

    public void RestartMic()
    {
        if (!Active || script != null) return;
        mic.Start(string.IsNullOrEmpty(Settings.MicDevice) ? null : Settings.MicDevice);
    }

    public void Recalibrate() => cal.Reset();

    // 取景：頭頂到腰（hips），上面留一點空間；角色依高度縮放，放在角落（或設定的位置）
    void Layout()
    {
        if (pet == null) return;
        var rig = pet.Rig;
        var u = DesktopWindow.UiScale;
        var rest = new Skeleton(rig.Bones, rig.RootScale, rig.TrackScale).Compute(new Dictionary<string, float>());
        var top = rest["head"] * new Vector2(rig.HeadTop[0], rig.HeadTop[1]);
        // 下緣：腰（hips），但最多到脖子往下 1.5 個頭高（正常頭身的角色頭才不會太小）
        var neck = rest["head"].Origin.Y;
        var bottom = Mathf.Min(rest.TryGetValue("hips", out var hips) ? hips.Origin.Y : float.MaxValue, neck + 1.5f * (neck - top.Y));
        // 上緣：頭上所有部件（耳朵、頭髮、帽子）的最高點，不只看頭頂
        if (pet.View.VisibleTop("head") is { } visible)
            top = new Vector2(top.X, Mathf.Min(top.Y, (rest["head"] * new Vector2(rig.HeadTop[0], visible)).Y));
        var span = Mathf.Max(20, bottom - top.Y);
        var h = Settings.Height > 0 ? Settings.Height : cfg.Bust.Height * u;
        var w = h * 0.85f;
        var s = h / (span * (1 + cfg.Bust.Margin));
        pet.SetScale(s);
        stage.Size = new Vector2(w, h);
        var area = host.Area();
        var pos = Settings.X >= 0 ? new Vector2(Settings.X, Settings.Y) : new Vector2(area.End.X - w - 24 * u, area.End.Y - h);
        stage.Position = new Vector2(Mathf.Clamp(pos.X, area.Position.X, area.End.X - w), Mathf.Clamp(pos.Y, area.Position.Y, area.End.Y - h));
        pet.Facing = facing;
        var x = w / 2 - top.X * s * facing;
        var y = cfg.Bust.Margin * span * s - top.Y * s;
        driver = new StillDriver(pet, x, y, pet.Action ?? "idle");
        pet.ResetPhysics();
    }

    void ApplyVisibility()
    {
        // 全螢幕自動隱藏可以對半身模式另外關掉（直播全螢幕遊戲時角色才不會消失）；使用者手動隱藏就一起藏
        var hide = host.Presence.ManualHidden || (hidden && !Settings.ShowOnFullscreen);
        stage.Visible = Active && !hide;
        host.HidePet(Id, Active && Settings.HideDesktopPet);
    }

    /// <summary>設定面向角度（Hub 的角度按鈕、拉桿）</summary>
    public void SetViewYaw(float value)
    {
        Settings.ViewYaw = Mathf.PosMod(value, 360);
        SaveSoon();
    }

    public void SetAutoTurn(bool on)
    {
        Settings.AutoTurn = on;
        SaveSoon();
    }

    // 角度：設定的角度（＋跟著游標轉身）→ 最接近的視角；跨過兩個視角的中間多 8° 才換（不會在邊界來回閃）
    void UpdateView(float dt)
    {
        var target = Settings.ViewYaw;
        if (Settings.AutoTurn)
        {
            var area = host.Area();
            var cursor = DesktopWindow.CursorInWindow();
            var center = stage.Position.X + stage.Size.X / 2;
            var side = Mathf.Clamp((cursor.X - center) / (area.Size.X / 2), -1, 1);      // 游標在右邊 = 正
            // 正面時往右轉 = 角度變大（往側面朝右）；背面時相反
            target += side * Settings.TurnRange * (Mathf.Cos(Mathf.DegToRad(Settings.ViewYaw)) >= 0 ? 1 : -1);
        }
        yaw = Mathf.RadToDeg(Mathf.LerpAngle(Mathf.DegToRad(yaw), Mathf.DegToRad(target), Mathf.Min(1, dt * 4)));
        var view = ContentLoader.ViewFor(baseRig, yaw);
        var far = Mathf.Abs(Mathf.RadToDeg(Mathf.AngleDifference(Mathf.DegToRad(viewYaw), Mathf.DegToRad(yaw))));
        if ((view.rig != pet!.Rig || view.facing != facing) && (float.IsNaN(viewYaw) || far > 53))
        {
            (facing, viewYaw) = (view.facing, view.viewYaw);
            pet.SetCharacter(view.rig);
            Layout();
        }
        pet.Gaze.Bias = new Vector2(view.rig == pet.Rig ? view.bias : 0, 0);
    }

    public void SetHideDesktopPet(bool on)
    {
        Settings.HideDesktopPet = on;
        if (!host.Sandbox) host.Settings.Save();
        ApplyVisibility();
    }

    public void SetShowOnFullscreen(bool on)
    {
        Settings.ShowOnFullscreen = on;
        if (!host.Sandbox) host.Settings.Save();
        ApplyVisibility();
    }

    // ---------- 每幀 ----------

    public void BeforeBehavior(float dt) => UpdateDrag();

    public void AfterPet(float dt)
    {
        if (pet == null || driver == null || control == null || attach == null) return;
        Frame = script?.Next(dt) ?? mic.Frame;
        cal.Update(Frame);
        mic.Floor = cal.Floor;
        var now = Now();
        if (Settings.VoiceRules) rules.Update(Frame, cal, dt, control, now);
        if (Settings.Hotkeys)
            foreach (var key in hotkeys.Poll())
                VoiceRules.Fire($"hotkey:{key}", cfg.Hotkeys[key], Priority.Hotkey, control);
        var speaking = Frame.Speaking && driver.CanEmote;
        if (speaking)
        {
            driver.Touch();
            control.Request("voice", Channel.Mouth, rules.MouthShape(Frame, cal), Priority.Talking);
        }

        UpdateView(dt);                  // 換視角時會重新取景（驅動者也換新的）
        driver.Update(dt);
        pet.ApplyControl(control);
        control.EndFrame();
        pet.Gaze.Target = DesktopWindow.CursorInWindow() - stage.Position;      // 游標在哪裡都看得到（全域座標）
        pet.Update(dt);
        attach.Update(dt, speaking, speaking && Frame.Db > cal.SpeechDb - cfg.Mouth.OpenDb);

        if (shotPath != null && (shotLeft -= dt) <= 0) SaveShot();
    }

    void SaveShot()
    {
        var image = host.Root.GetViewport().GetTexture().GetImage();
        var rect = new Rect2I((Vector2I)stage.Position, (Vector2I)stage.Size).Intersection(new Rect2I(Vector2I.Zero, image.GetSize()));
        // --vtuber-shot-full：整個畫面（縮小一半），看桌面角色與特效
        if (host.Args.ContainsKey("vtuber-shot-full")) image.Resize(image.GetWidth() / 2, image.GetHeight() / 2);
        else image = image.GetRegion(rect);
        image.SavePng(shotPath!);
        GD.Print($"[vtuber] 截圖 → {shotPath}（{Frame}，規則 {rules.LastFired ?? "-"}）");
        shotPath = null;
        if (host.Args.ContainsKey("vtuber-shot-quit")) host.Root.GetTree().Quit();
    }

    // ---------- 滑鼠：拖曳換位置、滾輪縮放、點一下開心、右鍵開設定 ----------

    bool OnPet(Vector2 cursor) => pet != null && stage.Visible && new Rect2(stage.Position, stage.Size).HasPoint(cursor)
                                  && pet.View.HitTest(cursor - stage.Position);

    public bool WantsMouse(Vector2 cursor) => dragOffset != null || OnPet(cursor);

    public bool HandleInput(InputEvent e)
    {
        if (e is not InputEventMouseButton { Pressed: true } mb || !OnPet(mb.Position)) return false;
        switch (mb.ButtonIndex)
        {
            case MouseButton.Left:
                dragOffset = mb.Position - stage.Position;
                dragStart = mb.Position;
                dragMoved = false;
                return true;
            case MouseButton.Right:
                host.Hub.Open("vtuber");
                return true;
            case MouseButton.WheelUp or MouseButton.WheelDown:
                var u = DesktopWindow.UiScale;
                var h = stage.Size.Y * (mb.ButtonIndex == MouseButton.WheelUp ? 1.08f : 1 / 1.08f);
                Settings.Height = Mathf.Clamp(h, 160 * u, host.Area().Size.Y);
                // 以腳底（下緣中間）為準縮放，角色不會跑掉
                var bottomCenter = stage.Position + new Vector2(stage.Size.X / 2, stage.Size.Y);
                Settings.X = bottomCenter.X - Settings.Height * 0.85f / 2;
                Settings.Y = bottomCenter.Y - Settings.Height;
                Layout();
                SaveSoon();
                return true;
        }
        return false;
    }

    void UpdateDrag()
    {
        if (dragOffset is not { } offset) return;
        var cursor = DesktopWindow.CursorInWindow();
        if (!DisplayServer.MouseGetButtonState().HasFlag(MouseButtonMask.Left))
        {
            dragOffset = null;
            if (!dragMoved && control != null) VoiceRules.Fire("click", new VoiceReaction { Face = "happy", Seconds = 1.2f }, Priority.Reaction, control);
            else SaveSoon();
            return;
        }
        if (cursor.DistanceTo(dragStart) > 6 * DesktopWindow.UiScale) dragMoved = true;
        if (!dragMoved) return;
        var area = host.Area();
        var pos = cursor - offset;
        stage.Position = new Vector2(Mathf.Clamp(pos.X, area.Position.X, area.End.X - stage.Size.X), Mathf.Clamp(pos.Y, area.Position.Y, area.End.Y - stage.Size.Y));
        Settings.X = stage.Position.X;
        Settings.Y = stage.Position.Y;
    }

    void SaveSoon()
    {
        if (!host.Sandbox) host.Settings.Save();
    }

    public void OnHiddenChanged(bool hidden)
    {
        this.hidden = hidden;
        ApplyVisibility();
    }

    public void OnDisplayChanged() => Layout();

    public string? DumpInfo() => Active
        ? $"view={pet!.Rig.View ?? "side"} parts={pet.Rig.DefaultOutfit?.Count ?? 0}{(facing < 0 ? "(左)" : "")} yaw={yaw:0} mic={MicStatus} db={Frame.Db:0.0} speaking={Frame.Speaking} pitch={Frame.Pitch:0} vowel={Frame.Vowel ?? "-"} floor={cal.Floor:0.0} base={cal.BasePitch:0} rule={rules.LastFired ?? "-"} face={pet!.FaceOverride ?? pet.Animator.Face} mouth={pet.MouthShape ?? "-"} action={pet.Action}"
        : null;

    public void Shutdown()
    {
        Settings.NoiseFloor = cal.Floor;
        Stop();
    }
}

/// <summary>測試用的假聲音：「quiet:2,talk:3,loud:0.2,high:1」依序循環（talk 會輪流換母音）</summary>
sealed class VoiceScript
{
    readonly List<(string kind, float seconds)> steps = new();
    int index;
    float left, t;

    public VoiceScript(string spec)
    {
        foreach (var part in spec.Split(','))
        {
            var kv = part.Split(':');
            steps.Add((kv[0], kv.Length > 1 && float.TryParse(kv[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 1));
        }
        left = steps[0].seconds;
    }

    public VoiceFrame Next(float dt)
    {
        t += dt;
        if ((left -= dt) <= 0)
        {
            index = (index + 1) % steps.Count;
            left = steps[index].seconds;
        }
        var vowel = "aiueo"[(int)(t / 0.15f) % 5].ToString();
        return steps[index].kind switch
        {
            "talk" => new VoiceFrame(-30, true, 220, vowel),
            "loud" => new VoiceFrame(-14, true, 220, "a"),
            "high" => new VoiceFrame(-30, true, 300, vowel),
            "low" => new VoiceFrame(-30, true, 170, vowel),
            _ => new VoiceFrame(-70, false, 0, null),
        };
    }
}
