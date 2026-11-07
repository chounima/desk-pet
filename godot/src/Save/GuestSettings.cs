// 設定：多人功能這一段（settings.json 的 "Guests"）。2026-11-07 前是攤平放在最上層，讀到時由 Settings.Migrate 搬過來
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DeskPet.Save;

public sealed class GuestSettings
{
    /// <summary>直播／會議模式：連線到下面設定的來源，有人在線上就出現客人角色</summary>
    public bool LiveMode { get; set; }
    /// <summary>Twitch 頻道名稱（匿名讀聊天室，不用登入）</summary>
    public string TwitchChannel { get; set; } = "";
    /// <summary>Discord 應用程式（開發者後台自己建立的）：用本機 RPC 讀語音頻道裡誰在講話</summary>
    public string DiscordClientId { get; set; } = "";
    [JsonIgnore] public string DiscordClientSecret { get; set; } = "";
    /// <summary>Discord 授權後拿到的 token（之後不用每次再按授權）</summary>
    [JsonIgnore] public string DiscordAccessToken { get; set; } = "";
    [JsonIgnore] public string DiscordRefreshToken { get; set; } = "";
    /// <summary>直播模式：麥克風有聲音時主角色動嘴</summary>
    public bool MicTalk { get; set; }
    /// <summary>直播模式：會議軟體（Teams、Zoom…）有聲音時出現「通話」客人</summary>
    public bool MeetingAudio { get; set; }
    /// <summary>同時最多幾個客人（null = data/guests.json 的預設）</summary>
    public int? MaxGuests { get; set; }
    /// <summary>某個人固定用哪個角色 { "twitch:12345" 或名字: 角色 }</summary>
    public Dictionary<string, string> Characters { get; set; } = new();

    // 存檔時三個機密用 DPAPI 加密（Secrets），讀到舊的明文也能用
    [JsonPropertyName("DiscordClientSecret")] public string StoredClientSecret { get => Secrets.Protect(DiscordClientSecret); set => DiscordClientSecret = Secrets.Unprotect(value); }
    [JsonPropertyName("DiscordAccessToken")] public string StoredAccessToken { get => Secrets.Protect(DiscordAccessToken); set => DiscordAccessToken = Secrets.Unprotect(value); }
    [JsonPropertyName("DiscordRefreshToken")] public string StoredRefreshToken { get => Secrets.Protect(DiscordRefreshToken); set => DiscordRefreshToken = Secrets.Unprotect(value); }
}
