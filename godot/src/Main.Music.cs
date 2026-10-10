// 主程式的彈吉他部分：音樂播放器、角色跟著拍子刷弦、右鍵選單／系統匣／音樂分頁的入口
using DeskPet.Audio.Music;
using Godot;

namespace DeskPet;

public partial class Main
{
    MusicPlayer music = null!;
    bool performing;
    float bpmTimer;

    void SetupMusic()
    {
        music = new MusicPlayer(Song.LoadAll());
        AddChild(music);
        music.Volume = args.ContainsKey("mute") ? 0.0001f : settings.MusicVolume;     // --mute：測試時不發出聲音
        music.Shuffle = settings.MusicShuffle;
        music.Loop = settings.MusicLoop;
        music.Beat += OnBeat;
        music.TrackStarted += _ => { UpdateStrumSpeed(); if (hub.Visible) hub.RefreshAll(); };
        music.Stopped += () => { if (performing) EndPerformance(); };

        hub.AddMusicTab(music, shop, settings);
        hub.Music.PlayRequested += track => StartPerformance(track);
        hub.Music.StopRequested += StopPerformance;
        hub.Music.ShopRequested += cat => hub.Open("shop", cat);
        petMenu.GuitarToggled += () => { if (performing) StopPerformance(); else StartPerformance(); };
        tray.GuitarToggled += () => { if (performing) StopPerformance(); else StartPerformance(); };
    }

    /// <summary>開始彈吉他；track = null 從清單第一首（或隨機）開始</summary>
    /// <param name="force">不檢查有沒有買吉他（測試用）</param>
    void StartPerformance(Track? track = null, bool force = false)
    {
        if (!force && !shop.ActionOwned("guitar")) { hub.Open("shop", "action"); return; }
        music.RefreshTracks(id => shop.Owns($"song:{id}"));
        if (music.Tracks.Count == 0) { behavior.Say("noSongs"); hub.Open("music"); return; }
        if (volley.Active) { volley.SetActive(false); tray.SetVolley(false); }
        if (!performing)
        {
            performing = true;
            progress.AddStat("guitar");
            behavior.EnterMinigame();
            pet.Play("guitar", true);
            behavior.Say("guitar");
            petMenu.Performing = true;
            tray.SetPerforming(true);
        }
        if (track != null) music.Play(track);
        else music.PlayFirst();
    }

    void StopPerformance()
    {
        music.Stop();          // 會觸發 Stopped → EndPerformance
        if (performing) EndPerformance();
    }

    void EndPerformance()
    {
        performing = false;
        pet.Animator.Speed = 1;
        behavior.ExitMinigame();
        behavior.Say("guitarEnd");
        petMenu.Performing = false;
        tray.SetPerforming(false);
        if (hub.Visible) hub.RefreshAll();
    }

    // 每一拍：刷弦動作拉回開頭，剛好在拍點刷下去
    void OnBeat()
    {
        if (!performing) return;
        if (pet.Action != "guitar") pet.Play("guitar", true);
        pet.Animator.Rephase();
    }

    // 動作本身是 120 BPM 一拍刷一下，依歌曲速度調整播放速度（太快或太慢就改成兩拍刷一下）
    void UpdateStrumSpeed()
    {
        var bpm = music.Bpm ?? 110;
        while (bpm > 170) bpm /= 2;
        while (bpm < 70) bpm *= 2;
        pet.Animator.Speed = bpm / 120f;
    }

    void UpdateMusic(float dt)
    {
        if (!performing) return;
        // 對打開始或角色躲起來時停止演奏
        if (volley.Active || presence.Hidden) { StopPerformance(); return; }
        if ((bpmTimer -= dt) <= 0) { bpmTimer = 1; UpdateStrumSpeed(); }     // 自訂音樂的 BPM 是邊聽邊估的
    }
}
